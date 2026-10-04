using NaturalGasPipeline.Core.Common;
using NaturalGasPipeline.Core.Flow;
using NaturalGasPipeline.Core.Numerics;
using NaturalGasPipeline.Core.Pipeline;
using NaturalGasPipeline.Hydraulics;
using NaturalGasPipeline.Thermodynamics;
using UnitsNet;

namespace NaturalGasPipeline.Correlations;

/// <summary>
/// Solves any <see cref="IGasFlowEquation"/> for the unknown of the design
/// problem: standard flow Q, outlet pressure P2, inlet pressure P1 or
/// internal diameter D.
///
/// Common treatment for every equation (all explicit):
/// - Z at P_avg = 2/3·(P1 + P2 − P1·P2/(P1 + P2)) and the flowing temperature;
/// - elevation: s = 2·g·Δz·M / (Z·R·T) with M = G·M_air (SI, exact constants;
///   the field form 0.0375·G·ΔH/(T·Z) is the same with rounded constants),
///   Le = L·(e^s − 1)/s, pressure term N = P1² − e^s·P2²;
/// - P1, P2: fixed-point iteration on Z, s and μ (they depend on P_avg);
///   D: Brent's method (N is independent of D, the required N falls with D).
/// Standard flows given at other base conditions are converted through the
/// mass flow, never by a volume ratio.
/// </summary>
public sealed class GasFlowEquationSolver
{
    private const double R = PhysicalConstants.MolarGasConstant;

    private readonly IGasFlowEquation _equation;
    private readonly FlowEquationGas _gas;
    private readonly StandardFlowConditions _base;
    private readonly double _relativeTolerance;
    private readonly int _maxIterations;

    public GasFlowEquationSolver(
        IGasFlowEquation equation,
        FlowEquationGas gas,
        StandardFlowConditions baseConditions,
        double relativeTolerance = 1e-10,
        int maxIterations = 200)
    {
        _equation = equation ?? throw new ArgumentNullException(nameof(equation));
        _gas = gas ?? throw new ArgumentNullException(nameof(gas));
        ArgumentNullException.ThrowIfNull(baseConditions);
        baseConditions.Validate();

        if (!double.IsFinite(relativeTolerance) || relativeTolerance <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(relativeTolerance), relativeTolerance, "Tolerance must be positive.");
        }

        if (maxIterations < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxIterations), maxIterations, "At least one iteration is required.");
        }

        _base = baseConditions;
        _relativeTolerance = relativeTolerance;
        _maxIterations = maxIterations;
    }

    /// <summary>Q from P1, P2 and the pipe.</summary>
    public FlowEquationSolution SolveFlow(PipeSegment segment, Temperature temperature, Pressure inletPressure, Pressure outletPressure)
    {
        ArgumentNullException.ThrowIfNull(segment);
        double t = RequireTemperature(temperature);
        double p1 = RequirePressure(inletPressure, nameof(inletPressure));
        double p2 = RequirePressure(outletPressure, nameof(outletPressure));

        Geometry g = Geometry.Of(segment);
        State state = Evaluate(g, t, p1, p2);
        double n = PressureTerm(p1, p2, state.S);

        if (n <= 0.0)
        {
            throw new InvalidOperationException(
                $"No forward flow: P1² − e^s·P2² = {n:E3} Pa² ≤ 0 (outlet pressure too high for the elevation change).");
        }

        FlowEquationEvaluation evaluation = _equation.CalculateFlow(n, state.Terms);

        return Build(g, t, p1, p2, state, evaluation, iterations: 1);
    }

    /// <summary>P2 from P1, Q and the pipe.</summary>
    public FlowEquationSolution SolveOutletPressure(PipeSegment segment, Temperature temperature, Pressure inletPressure, StandardVolumetricFlow standardFlow)
    {
        ArgumentNullException.ThrowIfNull(segment);
        double t = RequireTemperature(temperature);
        double p1 = RequirePressure(inletPressure, nameof(inletPressure));
        VolumeFlow q = ToBaseConditions(standardFlow);

        Geometry g = Geometry.Of(segment);
        double p2 = 0.9 * p1;

        for (int i = 1; i <= _maxIterations; i++)
        {
            State state = Evaluate(g, t, p1, p2);
            FlowEquationEvaluation evaluation = _equation.CalculatePressureTerm(q, state.Terms);

            double p2Squared = (p1 * p1 - evaluation.PressureTermPascalsSquared) / System.Math.Exp(state.S);

            if (p2Squared <= 0.0)
            {
                throw new InvalidOperationException(
                    "The flow exceeds the capacity of the segment for this inlet pressure (P2² ≤ 0).");
            }

            double updated = System.Math.Sqrt(p2Squared);

            if (System.Math.Abs(updated - p2) <= _relativeTolerance * p1)
            {
                State final = Evaluate(g, t, p1, updated);
                return Build(g, t, p1, updated, final, _equation.CalculatePressureTerm(q, final.Terms), i);
            }

            p2 = updated;
        }

        throw new InvalidOperationException($"Outlet pressure did not converge in {_maxIterations} iterations.");
    }

    /// <summary>P1 from P2, Q and the pipe.</summary>
    public FlowEquationSolution SolveInletPressure(PipeSegment segment, Temperature temperature, Pressure outletPressure, StandardVolumetricFlow standardFlow)
    {
        ArgumentNullException.ThrowIfNull(segment);
        double t = RequireTemperature(temperature);
        double p2 = RequirePressure(outletPressure, nameof(outletPressure));
        VolumeFlow q = ToBaseConditions(standardFlow);

        Geometry g = Geometry.Of(segment);
        double p1 = 1.1 * p2;

        for (int i = 1; i <= _maxIterations; i++)
        {
            State state = Evaluate(g, t, p1, p2);
            FlowEquationEvaluation evaluation = _equation.CalculatePressureTerm(q, state.Terms);

            double updated = System.Math.Sqrt(System.Math.Exp(state.S) * p2 * p2 + evaluation.PressureTermPascalsSquared);

            if (System.Math.Abs(updated - p1) <= _relativeTolerance * updated)
            {
                State final = Evaluate(g, t, updated, p2);
                return Build(g, t, updated, p2, final, _equation.CalculatePressureTerm(q, final.Terms), i);
            }

            p1 = updated;
        }

        throw new InvalidOperationException($"Inlet pressure did not converge in {_maxIterations} iterations.");
    }

    /// <summary>
    /// Internal diameter that carries Q between P1 and P2 (design / sizing).
    /// Searched between max(1 mm, 10·ε) and 10 m.
    /// </summary>
    public FlowEquationSolution SolveInternalDiameter(
        Length length,
        Length absoluteRoughness,
        Length elevationChange,
        Temperature temperature,
        Pressure inletPressure,
        Pressure outletPressure,
        StandardVolumetricFlow standardFlow)
    {
        double t = RequireTemperature(temperature);
        double p1 = RequirePressure(inletPressure, nameof(inletPressure));
        double p2 = RequirePressure(outletPressure, nameof(outletPressure));
        VolumeFlow q = ToBaseConditions(standardFlow);

        // Validates L, ε, Δz with a placeholder diameter; D does not enter Z, s or μ.
        var probe = new Geometry(Length.FromMeters(1.0), length, absoluteRoughness, elevationChange);
        _ = new PipeSegment(probe.Diameter, length, absoluteRoughness, elevationChange);

        State state = Evaluate(probe, t, p1, p2);
        double available = PressureTerm(p1, p2, state.S);

        if (available <= 0.0)
        {
            throw new InvalidOperationException(
                $"No forward flow: P1² − e^s·P2² = {available:E3} Pa² ≤ 0.");
        }

        double Residual(double d)
        {
            FlowEquationTerms terms = state.Terms with { InternalDiameter = Length.FromMeters(d) };
            return System.Math.Log(_equation.CalculatePressureTerm(q, terms).PressureTermPascalsSquared / available);
        }

        double lower = System.Math.Max(1e-3, 10.0 * absoluteRoughness.Meters);
        double diameter = BrentRootSolver.FindRoot(Residual, lower, 10.0, 1e-12);

        var geometry = new Geometry(Length.FromMeters(diameter), length, absoluteRoughness, elevationChange);
        State final = Evaluate(geometry, t, p1, p2);

        return Build(geometry, t, p1, p2, final, _equation.CalculatePressureTerm(q, final.Terms), iterations: 1);
    }

    // ------------------------------------------------------------ internals

    private readonly record struct Geometry(Length Diameter, Length Length, Length Roughness, Length ElevationChange)
    {
        public static Geometry Of(PipeSegment s) =>
            new(s.InternalDiameter, s.Length, s.AbsoluteRoughness, s.ElevationChange);
    }

    private sealed record State(double PAvg, double Z, double S, FlowEquationTerms Terms);

    private State Evaluate(Geometry g, double t, double p1, double p2)
    {
        double pAvg = IsothermalGasFlowSolver.AveragePressure(p1, p2);
        var temperature = Temperature.FromKelvins(t);

        double z = _gas.Compressibility.Calculate(Pressure.FromPascals(pAvg), temperature);

        if (!double.IsFinite(z) || z <= 0.0)
        {
            throw new InvalidOperationException($"The compressibility model returned Z = {z} at P_avg = {pAvg} Pa.");
        }

        double m = _gas.MolarMass.KilogramsPerMole;
        double s = 2.0 * PhysicalConstants.StandardGravity * g.ElevationChange.Meters * m / (z * R * t);
        double le = g.Length.Meters * IsothermalGasFlowSolver.ElevationFactor(s);

        DynamicViscosity? mu = _gas.Viscosity?.Calculate(
            temperature,
            Density.FromKilogramsPerCubicMeter(pAvg * m / (z * R * t)),
            _gas.MolarMass);

        var terms = new FlowEquationTerms
        {
            InternalDiameter = g.Diameter,
            AbsoluteRoughness = g.Roughness,
            EquivalentLength = Length.FromMeters(le),
            FlowingTemperature = temperature,
            BaseConditions = _base,
            SpecificGravity = _gas.SpecificGravity,
            MolarMass = _gas.MolarMass,
            CompressibilityFactor = z,
            ElevationParameter = s,
            Viscosity = mu
        };

        return new State(pAvg, z, s, terms);
    }

    private static double PressureTerm(double p1, double p2, double s) => p1 * p1 - System.Math.Exp(s) * p2 * p2;

    private FlowEquationSolution Build(
        Geometry g, double t, double p1, double p2, State state, FlowEquationEvaluation evaluation, int iterations)
    {
        var flow = new StandardVolumetricFlow(evaluation.StandardFlow, _base);
        var warnings = new List<string>(_equation.CheckApplicability(state.Terms, evaluation));

        if (_gas.Compressibility is ICompressibilityCorrelation correlation)
        {
            var temperature = Temperature.FromKelvins(t);

            foreach ((string label, double p) in new[] { ("P1", p1), ("P2", p2), ("P_avg", state.PAvg) })
            {
                if (!correlation.IsWithinValidatedRange(Pressure.FromPascals(p), temperature))
                {
                    warnings.Add(
                        $"Compressibility model {correlation.Name} is used outside its validated range " +
                        $"at {label} = {p / 1.0e3:F1} kPa, T = {t:F2} K.");
                }
            }
        }

        if (p2 < 0.5 * p1)
        {
            warnings.Add(
                $"Pressure drop is {(1.0 - p2 / p1) * 100.0:F0} % of the inlet pressure: " +
                "properties at P_avg are a coarse approximation; consider splitting the segment.");
        }

        return new FlowEquationSolution
        {
            Equation = _equation.Name,
            InletPressure = Pressure.FromPascals(p1),
            OutletPressure = Pressure.FromPascals(p2),
            StandardFlow = flow,
            MassFlow = FlowRateConverter.ConvertStandardVolumeToMass(flow, _gas.MolarMass),
            InternalDiameter = g.Diameter,
            FlowingTemperature = Temperature.FromKelvins(t),
            AveragePressure = Pressure.FromPascals(state.PAvg),
            AverageCompressibilityFactor = state.Z,
            ElevationParameter = state.S,
            EquivalentLength = state.Terms.EquivalentLength,
            TransmissionFactor = evaluation.TransmissionFactor,
            ReynoldsNumber = evaluation.ReynoldsNumber,
            Iterations = iterations,
            Warnings = warnings
        };
    }

    private VolumeFlow ToBaseConditions(StandardVolumetricFlow standardFlow)
    {
        ArgumentNullException.ThrowIfNull(standardFlow);

        double q = standardFlow.Value.CubicMetersPerSecond;

        if (!double.IsFinite(q) || q <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(standardFlow), q, "Standard flow must be finite and greater than zero.");
        }

        StandardFlowConditions c = standardFlow.Conditions;

        bool same =
            SameValue(c.Temperature.Kelvins, _base.Temperature.Kelvins)
            && SameValue(c.Pressure.Pascals, _base.Pressure.Pascals)
            && SameValue(c.CompressibilityFactor, _base.CompressibilityFactor);

        if (same)
        {
            return standardFlow.Value;
        }

        MassFlow m = FlowRateConverter.ConvertStandardVolumeToMass(standardFlow, _gas.MolarMass);
        return FlowRateConverter.ConvertMassToStandardVolume(m, _gas.MolarMass, _base).Value;
    }

    private static bool SameValue(double a, double b) => System.Math.Abs(a - b) <= 1e-12 * System.Math.Abs(b);

    private static double RequireTemperature(Temperature temperature)
    {
        double t = temperature.Kelvins;

        if (!double.IsFinite(t) || t <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(temperature), t, "Temperature must be finite and greater than zero (K).");
        }

        return t;
    }

    private static double RequirePressure(Pressure pressure, string name)
    {
        double p = pressure.Pascals;

        if (!double.IsFinite(p) || p <= 0.0)
        {
            throw new ArgumentOutOfRangeException(name, p, "Pressure must be finite and greater than zero (absolute).");
        }

        return p;
    }
}
