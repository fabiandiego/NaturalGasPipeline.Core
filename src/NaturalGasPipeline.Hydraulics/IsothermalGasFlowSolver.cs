using NaturalGasPipeline.Core.Flow;
using UnitsNet;
using NaturalGasPipeline.Core.Pipeline;
using NaturalGasPipeline.Thermodynamics;
using NaturalGasPipeline.Core.Common;

namespace NaturalGasPipeline.Hydraulics;

/// <summary>
/// Steady, isothermal, single-phase gas flow in one pipe segment with a
/// uniform slope. This is the General Flow Equation written in SI with the
/// Darcy friction factor:
///
///   P1² − e^s · P2² = f · G² · a · L / D · (e^s − 1) / s
///
///   G = ṁ / A                 mass flux, kg/(m²·s)
///   a = Z·R·T / M             (≈ squared isothermal sound speed), m²/s²
///   s = 2·g·Δz / a            elevation parameter (s → 0: (e^s − 1)/s → 1)
///
/// Derived by integrating dp/dx = −ρ·g·sinθ − f·ρ·v²/(2D) with ρ = P/a.
///
/// Assumptions (none of them silent; each is stated here):
/// - isothermal flow at the given temperature;
/// - Z, μ and therefore Re and f evaluated at the average pressure
///   P_avg = 2/3·(P1 + P2 − P1·P2/(P1 + P2)) and held constant;
/// - kinetic-energy (acceleration) term neglected;
/// - constant slope over the segment (split long profiles into segments).
///
/// Verified against direct numerical integration of the differential
/// equation with local Z(p): 0.02 % of ΔP for a flat 50 km line and 0.35 %
/// with 500 m of climb (see IsothermalGasFlowSolverTests).
/// </summary>
public sealed class IsothermalGasFlowSolver
{
    private const double R = PhysicalConstants.MolarGasConstant;
    private const double StandardGravity = PhysicalConstants.StandardGravity;

    // API RP 14E erosional velocity constant for continuous service (ft/s·(lb/ft³)^0.5)
    private const double ErosionalConstantContinuous = 100.0;

    private readonly MolarMass _molarMass;
    private readonly ICompressibilityModel _compressibility;
    private readonly IViscosityModel _viscosity;
    private readonly IFrictionFactorModel _friction;
    private readonly double _relativeTolerance;
    private readonly int _maxIterations;

    /// <param name="molarMass">Molar mass of the gas.</param>
    /// <param name="compressibilityModel">Z(P, T) model for this gas.</param>
    /// <param name="viscosityModel">μ(T, ρ, M) model for this gas.</param>
    /// <param name="frictionModel">
    /// Darcy friction factor model. Default: Colebrook-White. Use
    /// <see cref="ConstantDarcyFrictionModel"/> or
    /// <see cref="ConstantTransmissionFactorModel"/> for a user-specified value.
    /// </param>
    /// <param name="relativeTolerance">Convergence tolerance on pressure (relative).</param>
    /// <param name="maxIterations">Iteration limit; exceeding it throws.</param>
    public IsothermalGasFlowSolver(
        MolarMass molarMass,
        ICompressibilityModel compressibilityModel,
        IViscosityModel viscosityModel,
        IFrictionFactorModel? frictionModel = null,
        double relativeTolerance = 1e-10,
        int maxIterations = 200)
    {
        ArgumentNullException.ThrowIfNull(compressibilityModel);
        ArgumentNullException.ThrowIfNull(viscosityModel);

        double m = molarMass.KilogramsPerMole;

        if (!double.IsFinite(m) || m <= 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(molarMass), m, "Molar mass must be finite and greater than zero.");
        }

        if (!double.IsFinite(relativeTolerance) || relativeTolerance <= 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(relativeTolerance), relativeTolerance, "Tolerance must be finite and greater than zero.");
        }

        if (maxIterations < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxIterations), maxIterations, "At least one iteration is required.");
        }

        _molarMass = molarMass;
        _compressibility = compressibilityModel;
        _viscosity = viscosityModel;
        _friction = frictionModel ?? new ColebrookWhiteModel();
        _relativeTolerance = relativeTolerance;
        _maxIterations = maxIterations;
    }

    /// <summary>
    /// Convenience constructor for a characterised gas mixture.
    /// </summary>
    public static IsothermalGasFlowSolver ForMixture(
        GasMixture mixture,
        CompressibilityModelType compressibilityModelType,
        IViscosityModel viscosityModel,
        IFrictionFactorModel? frictionModel = null,
        BinaryInteractionDatabase? interactionDatabase = null)
    {
        ArgumentNullException.ThrowIfNull(mixture);

        mixture.ValidateComposition();

        return new IsothermalGasFlowSolver(
            mixture.CalculateMolarMass(),
            CompressibilityModelFactory.Create(compressibilityModelType, mixture, interactionDatabase),
            viscosityModel,
            frictionModel);
    }

    /// <summary>Given P1 and ṁ, find P2.</summary>
    public GasFlowSolution SolveOutletPressure(
        PipeSegment segment,
        Temperature temperature,
        Pressure inletPressure,
        MassFlow massFlow)
    {
        ArgumentNullException.ThrowIfNull(segment);
        double t = RequireTemperature(temperature);
        double p1 = RequirePressure(inletPressure, nameof(inletPressure));
        double m = RequireMassFlow(massFlow);

        double p2 = 0.9 * p1;

        for (int iteration = 1; iteration <= _maxIterations; iteration++)
        {
            AverageState avg = EvaluateAverageState(segment, t, p1, p2, m);

            double p2Squared =
                (p1 * p1 - avg.FrictionTerm(m)) / System.Math.Exp(avg.S);

            if (p2Squared <= 0.0)
            {
                throw new FlowCapacityExceededException(
                    $"No physical outlet pressure: the requested mass flow " +
                    $"{m} kg/s exceeds the capacity of the segment for P1 = {p1} Pa.");
            }

            double p2New = System.Math.Sqrt(p2Squared);

            if (System.Math.Abs(p2New - p2) <= _relativeTolerance * p1)
            {
                return BuildSolution(segment, t, p1, p2New, m, iteration);
            }

            p2 = p2New;
        }

        throw NotConverged(nameof(SolveOutletPressure));
    }

    /// <summary>Given P2 and ṁ, find P1.</summary>
    public GasFlowSolution SolveInletPressure(
        PipeSegment segment,
        Temperature temperature,
        Pressure outletPressure,
        MassFlow massFlow)
    {
        ArgumentNullException.ThrowIfNull(segment);
        double t = RequireTemperature(temperature);
        double p2 = RequirePressure(outletPressure, nameof(outletPressure));
        double m = RequireMassFlow(massFlow);

        double p1 = 1.1 * p2;

        for (int iteration = 1; iteration <= _maxIterations; iteration++)
        {
            AverageState avg = EvaluateAverageState(segment, t, p1, p2, m);

            double p1New =
                System.Math.Sqrt(
                    System.Math.Exp(avg.S) * p2 * p2
                    + avg.FrictionTerm(m));

            if (System.Math.Abs(p1New - p1) <= _relativeTolerance * p2)
            {
                return BuildSolution(segment, t, p1New, p2, m, iteration);
            }

            p1 = p1New;
        }

        throw NotConverged(nameof(SolveInletPressure));
    }

    /// <summary>Given P1 and P2, find ṁ.</summary>
    public GasFlowSolution SolveMassFlow(
        PipeSegment segment,
        Temperature temperature,
        Pressure inletPressure,
        Pressure outletPressure)
    {
        ArgumentNullException.ThrowIfNull(segment);
        double t = RequireTemperature(temperature);
        double p1 = RequirePressure(inletPressure, nameof(inletPressure));
        double p2 = RequirePressure(outletPressure, nameof(outletPressure));

        // Z, ρ and μ depend only on P_avg, which is fixed here; only f depends on ṁ.
        AverageState avg = EvaluateAverageState(segment, t, p1, p2, massFlowForReynolds: null);

        double drivingTerm = p1 * p1 - System.Math.Exp(avg.S) * p2 * p2;

        if (drivingTerm <= 0.0)
        {
            throw new InvalidOperationException(
                "No forward flow: P1² − e^s·P2² ≤ 0 (outlet pressure plus elevation " +
                "head is not lower than the inlet pressure).");
        }

        double d = segment.InternalDiameter.Meters;
        double area = System.Math.PI * d * d / 4.0;
        double factor = avg.A * segment.Length.Meters / d * ElevationFactor(avg.S);

        double relativeRoughness =
            FrictionFactorModelExtensions.RelativeRoughness(
                segment.InternalDiameter,
                segment.AbsoluteRoughness);

        double f = 0.01;
        double m = 0.0;

        for (int iteration = 1; iteration <= _maxIterations; iteration++)
        {
            double massFlux = System.Math.Sqrt(drivingTerm / (f * factor));
            m = massFlux * area;

            double reynolds = 4.0 * m / (System.Math.PI * d * avg.Viscosity);

            double fNew = _friction.CalculateDarcyFrictionFactor(reynolds, relativeRoughness);

            if (System.Math.Abs(fNew - f) <= _relativeTolerance * fNew)
            {
                return BuildSolution(segment, t, p1, p2, m, iteration);
            }

            f = fNew;
        }

        throw NotConverged(nameof(SolveMassFlow));
    }

    // ------------------------------------------------------------------

    /// <summary>Gas properties and friction at the average pressure.</summary>
    private readonly record struct AverageState(
        double PressureAverage,
        double Z,
        double Density,
        double Viscosity,
        double Reynolds,
        double F,
        double A,
        double S,
        double Diameter,
        double Length)
    {
        /// <summary>f·G²·a·L/D·(e^s − 1)/s</summary>
        public double FrictionTerm(double massFlow)
        {
            double area = System.Math.PI * Diameter * Diameter / 4.0;
            double g = massFlow / area;

            return F * g * g * A * Length / Diameter * ElevationFactor(S);
        }
    }

    private AverageState EvaluateAverageState(
        PipeSegment segment,
        double t,
        double p1,
        double p2,
        double? massFlowForReynolds)
    {
        double pAvg = AveragePressure(p1, p2);
        double m = _molarMass.KilogramsPerMole;

        double z = CompressibilityAt(pAvg, t);
        double a = z * R * t / m;
        double rho = pAvg / a;

        double mu = _viscosity.Calculate(
            Temperature.FromKelvins(t),
            UnitsNet.Density.FromKilogramsPerCubicMeter(rho),
            _molarMass).PascalSeconds;

        if (!double.IsFinite(mu) || mu <= 0.0)
        {
            throw new InvalidOperationException(
                $"Viscosity model returned a non-physical value {mu} Pa·s.");
        }

        double d = segment.InternalDiameter.Meters;

        // Without a mass flow (SolveMassFlow), Re and f are not defined yet:
        // they are left as NaN and computed by the caller's own iteration.
        double reynolds = massFlowForReynolds is double mf
            ? 4.0 * mf / (System.Math.PI * d * mu)
            : double.NaN;

        double f = double.IsNaN(reynolds)
            ? double.NaN
            : _friction.CalculateDarcyFrictionFactor(
                reynolds,
                segment.InternalDiameter,
                segment.AbsoluteRoughness);

        double s = 2.0 * StandardGravity * segment.ElevationChange.Meters / a;

        return new AverageState(pAvg, z, rho, mu, reynolds, f, a, s, d, segment.Length.Meters);
    }

    private GasFlowSolution BuildSolution(
        PipeSegment segment,
        double t,
        double p1,
        double p2,
        double m,
        int iterations)
    {
        AverageState avg = EvaluateAverageState(segment, t, p1, p2, m);

        double d = segment.InternalDiameter.Meters;
        double area = System.Math.PI * d * d / 4.0;
        double molarMass = _molarMass.KilogramsPerMole;

        double rho1 = p1 * molarMass / (CompressibilityAt(p1, t) * R * t);
        double rho2 = p2 * molarMass / (CompressibilityAt(p2, t) * R * t);

        double v1 = m / (rho1 * area);
        double v2 = m / (rho2 * area);

        var warnings = new List<string>();

        double relativeRoughness =
            FrictionFactorModelExtensions.RelativeRoughness(
                segment.InternalDiameter,
                segment.AbsoluteRoughness);

        if (!_friction.IsWithinValidatedRange(avg.Reynolds, relativeRoughness))
        {
            warnings.Add(
                $"Friction model {_friction.Name} is used outside its validated range " +
                $"(Re = {avg.Reynolds:E3}, ε/D = {relativeRoughness:E3}).");
        }

        if (_compressibility is ICompressibilityCorrelation correlation)
        {
            var temperature = Temperature.FromKelvins(t);

            foreach ((string label, double p) in new[] { ("P1", p1), ("P2", p2), ("P_avg", avg.PressureAverage) })
            {
                if (!correlation.IsWithinValidatedRange(Pressure.FromPascals(p), temperature))
                {
                    warnings.Add(
                        $"Compressibility model {correlation.Name} is used outside its validated range " +
                        $"at {label} = {p / 1.0e3:F1} kPa, T = {t:F2} K.");
                }
            }
        }

        if (avg.Reynolds < 4000.0)
        {
            warnings.Add(
                $"Reynolds number {avg.Reynolds:F0} is below 4000: " +
                "flow is not fully turbulent, unusual for a transmission line.");
        }

        // API RP 14E: Ve [ft/s] = C / sqrt(ρ [lb/ft³]), C = 100 for continuous service.
        double maxVelocity = System.Math.Max(System.Math.Abs(v1), System.Math.Abs(v2));
        double minDensity = System.Math.Min(rho1, rho2);
        double erosional =
            ErosionalConstantContinuous
            / System.Math.Sqrt(minDensity * 0.062427960576145)
            * 0.3048;

        if (maxVelocity > erosional)
        {
            warnings.Add(
                $"Maximum velocity {maxVelocity:F1} m/s exceeds the API RP 14E " +
                $"erosional velocity {erosional:F1} m/s (C = 100, continuous service).");
        }

        if (p2 < 0.5 * p1)
        {
            warnings.Add(
                $"Pressure drop is {(1.0 - p2 / p1) * 100.0:F0} % of the inlet pressure: " +
                "properties evaluated at P_avg are a coarse approximation; " +
                "consider splitting the segment.");
        }

        return new GasFlowSolution
        {
            InletPressure = Pressure.FromPascals(p1),
            OutletPressure = Pressure.FromPascals(p2),
            MassFlow = MassFlow.FromKilogramsPerSecond(m),
            AveragePressure = Pressure.FromPascals(avg.PressureAverage),
            AverageCompressibilityFactor = avg.Z,
            AverageDensity = Density.FromKilogramsPerCubicMeter(avg.Density),
            AverageViscosity = DynamicViscosity.FromPascalSeconds(avg.Viscosity),
            ReynoldsNumber = avg.Reynolds,
            DarcyFrictionFactor = avg.F,
            FrictionModel = _friction.Name,
            InletVelocity = Speed.FromMetersPerSecond(v1),
            OutletVelocity = Speed.FromMetersPerSecond(v2),
            ElevationParameter = avg.S,
            Iterations = iterations,
            Warnings = warnings
        };
    }

    private double CompressibilityAt(double pressurePascals, double temperatureKelvins)
    {
        double z = _compressibility.Calculate(
            Pressure.FromPascals(pressurePascals),
            Temperature.FromKelvins(temperatureKelvins));

        if (!double.IsFinite(z) || z <= 0.0)
        {
            throw new InvalidOperationException(
                $"Compressibility model returned a non-physical Z = {z}.");
        }

        return z;
    }

    /// <summary>P_avg = 2/3 · (P1 + P2 − P1·P2 / (P1 + P2))</summary>
    public static double AveragePressure(double p1, double p2) =>
        2.0 / 3.0 * (p1 + p2 - p1 * p2 / (p1 + p2));

    /// <summary>(e^s − 1) / s, with its series for small |s| to avoid 0/0.</summary>
    public static double ElevationFactor(double s) =>
        System.Math.Abs(s) < 1e-4
            ? 1.0 + s / 2.0 + s * s / 6.0 + s * s * s / 24.0
            : (System.Math.Exp(s) - 1.0) / s;

    private static double RequireTemperature(Temperature temperature)
    {
        double t = temperature.Kelvins;

        if (!double.IsFinite(t) || t <= 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(temperature), t, "Temperature must be finite and greater than 0 K.");
        }

        return t;
    }

    private static double RequirePressure(Pressure pressure, string name)
    {
        double p = pressure.Pascals;

        if (!double.IsFinite(p) || p <= 0.0)
        {
            throw new ArgumentOutOfRangeException(
                name, p, "Absolute pressure must be finite and greater than zero.");
        }

        return p;
    }

    private static double RequireMassFlow(MassFlow massFlow)
    {
        double m = massFlow.KilogramsPerSecond;

        if (!double.IsFinite(m) || m <= 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(massFlow), m, "Mass flow must be finite and greater than zero (flow from inlet to outlet).");
        }

        return m;
    }

    private InvalidOperationException NotConverged(string method) =>
        new($"{method} did not converge in {_maxIterations} iterations.");
}
