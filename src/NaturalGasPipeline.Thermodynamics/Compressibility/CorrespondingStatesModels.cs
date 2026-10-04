using NaturalGasPipeline.Core.Numerics;
using UnitsNet;

namespace NaturalGasPipeline.Thermodynamics;

/// <summary>
/// Base for Z correlations of the generalized (Standing–Katz) chart:
/// Z = Z(Tpr, Ppr) with Tpr = T/Tpc and Ppr = P/Ppc. Where the
/// pseudo-critical properties come from is the caller's choice
/// (<see cref="PseudoCriticalProperties"/>).
///
/// To add a correlation: derive from this class, implement
/// <see cref="CalculateFromReducedProperties"/> and the validated range in
/// reduced coordinates, and document the source.
/// </summary>
public abstract class CorrespondingStatesCompressibilityModel : ICompressibilityCorrelation
{
    protected CorrespondingStatesCompressibilityModel(PseudoCriticalProperties pseudoCritical)
    {
        PseudoCritical = pseudoCritical ?? throw new ArgumentNullException(nameof(pseudoCritical));
    }

    public PseudoCriticalProperties PseudoCritical { get; }

    public abstract string Name { get; }

    public double Calculate(Pressure pressure, Temperature temperature)
    {
        double tr = PseudoCritical.ReducedTemperature(temperature);
        double pr = PseudoCritical.ReducedPressure(pressure);

        if (!double.IsFinite(tr) || tr <= 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(temperature), temperature.Kelvins, "Temperature must be finite and greater than zero (K).");
        }

        if (!double.IsFinite(pr) || pr <= 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pressure), pressure.Pascals, "Pressure must be finite and greater than zero (absolute).");
        }

        return CalculateFromReducedProperties(tr, pr);
    }

    public bool IsWithinValidatedRange(Pressure pressure, Temperature temperature) =>
        IsWithinValidatedReducedRange(
            PseudoCritical.ReducedTemperature(temperature),
            PseudoCritical.ReducedPressure(pressure));

    /// <summary>Z at pseudo-reduced temperature Tpr and pressure Ppr.</summary>
    public abstract double CalculateFromReducedProperties(double reducedTemperature, double reducedPressure);

    /// <summary>Validated range in reduced coordinates.</summary>
    public abstract bool IsWithinValidatedReducedRange(double reducedTemperature, double reducedPressure);
}

/// <summary>
/// Dranchuk &amp; Abou-Kassem (1975): 11-constant fit of the Standing–Katz
/// chart (Benedict-Webb-Rubin form), solved for the reduced density
/// ρr = 0.27·Ppr / (Z·Tpr):
///
///   Z = 1 + (A1 + A2/Tr + A3/Tr³ + A4/Tr⁴ + A5/Tr⁵)·ρr
///         + (A6 + A7/Tr + A8/Tr²)·ρr²
///         − A9·(A7/Tr + A8/Tr²)·ρr⁵
///         + A10·(1 + A11·ρr²)·(ρr²/Tr³)·exp(−A11·ρr²)
///
/// Validated range: 1.0 ≤ Tpr ≤ 3.0, 0.2 ≤ Ppr ≤ 30.
/// Inside that range the equation has exactly one root in 0 &lt; ρr ≤ 3
/// (checked on an 81 × 150 grid, largest root 2.46), found by Brent's method.
/// Measured against NIST (pure methane/nitrogen, Kay pseudo-criticals =
/// true criticals): see CorrespondingStatesAccuracyTests.
/// </summary>
public sealed class DranchukAbouKassemModel : CorrespondingStatesCompressibilityModel
{
    private const double A1 = 0.3265;
    private const double A2 = -1.0700;
    private const double A3 = -0.5339;
    private const double A4 = 0.01569;
    private const double A5 = -0.05165;
    private const double A6 = 0.5475;
    private const double A7 = -0.7361;
    private const double A8 = 0.1844;
    private const double A9 = 0.1056;
    private const double A10 = 0.6134;
    private const double A11 = 0.7210;

    public DranchukAbouKassemModel(PseudoCriticalProperties pseudoCritical)
        : base(pseudoCritical)
    {
    }

    public override string Name => "Dranchuk-Abou-Kassem";

    public override double CalculateFromReducedProperties(double reducedTemperature, double reducedPressure)
    {
        double tr = reducedTemperature;
        double pr = reducedPressure;

        double c1 = A1 + A2 / tr + A3 / (tr * tr * tr) + A4 / (tr * tr * tr * tr) + A5 / (tr * tr * tr * tr * tr);
        double c2 = A6 + A7 / tr + A8 / (tr * tr);
        double c3 = A9 * (A7 / tr + A8 / (tr * tr));
        double c4 = A10 / (tr * tr * tr);

        double Residual(double rho)
        {
            double rho2 = rho * rho;
            double z = 1.0
                       + c1 * rho
                       + c2 * rho2
                       - c3 * rho2 * rho2 * rho
                       + c4 * (1.0 + A11 * rho2) * rho2 * System.Math.Exp(-A11 * rho2);

            return z - 0.27 * pr / (rho * tr);
        }

        // Residual → −∞ as ρr → 0; positive at ρr = 3 throughout the
        // validated range. Outside it, widen the bracket before giving up.
        const double lower = 1e-10;
        double upper = 3.0;

        while (Residual(upper) < 0.0)
        {
            upper *= 1.5;

            if (upper > 20.0)
            {
                throw new InvalidOperationException(
                    $"Dranchuk-Abou-Kassem: no root found for Tpr = {tr}, Ppr = {pr}.");
            }
        }

        double rhoR = BrentRootSolver.FindRoot(Residual, lower, upper, 1e-14);

        return 0.27 * pr / (rhoR * tr);
    }

    public override bool IsWithinValidatedReducedRange(double reducedTemperature, double reducedPressure) =>
        reducedTemperature >= 1.0 && reducedTemperature <= 3.0
        && reducedPressure >= 0.2 && reducedPressure <= 30.0;
}

/// <summary>
/// Hall &amp; Yarborough (1973): Starling-Carnahan form fitted to the
/// Standing–Katz chart, solved for the reduced density parameter y:
///
///   t = 1/Tpr
///   0 = −A·Ppr + (y + y² + y³ − y⁴)/(1 − y)³ − B·y² + C·y^D
///   A = 0.06125·t·exp(−1.2·(1 − t)²)
///   B = t·(14.76 − 9.76·t + 4.58·t²)
///   C = t·(90.7 − 242.2·t + 42.4·t²)
///   D = 2.18 + 2.82·t
///   Z = A·Ppr / y
///
/// Validated range, as usually reported: 1.15 ≤ Tpr ≤ 3.0, 0.2 ≤ Ppr ≤ 20.5.
/// Below Tpr ≈ 1.15 it departs from DAK by up to 12 % (measured at Tpr 1.05).
/// The equation has one root in 0 &lt; y &lt; 1 over 1 ≤ Tpr ≤ 3,
/// 0.2 ≤ Ppr ≤ 30 (checked on an 81 × 150 grid; largest root y = 0.55),
/// found by Brent's method.
/// </summary>
public sealed class HallYarboroughModel : CorrespondingStatesCompressibilityModel
{
    public HallYarboroughModel(PseudoCriticalProperties pseudoCritical)
        : base(pseudoCritical)
    {
    }

    public override string Name => "Hall-Yarborough";

    public override double CalculateFromReducedProperties(double reducedTemperature, double reducedPressure)
    {
        double t = 1.0 / reducedTemperature;
        double pr = reducedPressure;

        double a = 0.06125 * t * System.Math.Exp(-1.2 * (1.0 - t) * (1.0 - t));
        double b = t * (14.76 - 9.76 * t + 4.58 * t * t);
        double c = t * (90.7 - 242.2 * t + 42.4 * t * t);
        double d = 2.18 + 2.82 * t;

        double Residual(double y)
        {
            double oneMinusY = 1.0 - y;

            return -a * pr
                   + (y + y * y + y * y * y - y * y * y * y) / (oneMinusY * oneMinusY * oneMinusY)
                   - b * y * y
                   + c * System.Math.Pow(y, d);
        }

        // Residual(0⁺) = −A·Ppr < 0 and Residual → +∞ as y → 1.
        double root = BrentRootSolver.FindRoot(Residual, 1e-12, 0.99, 1e-15);

        return a * pr / root;
    }

    public override bool IsWithinValidatedReducedRange(double reducedTemperature, double reducedPressure) =>
        reducedTemperature >= 1.15 && reducedTemperature <= 3.0
        && reducedPressure >= 0.2 && reducedPressure <= 20.5;
}

/// <summary>
/// Papay (1968), explicit:
///   Z = 1 − 3.53·Ppr / 10^(0.9813·Tpr) + 0.274·Ppr² / 10^(0.8157·Tpr)
///
/// Closed form, no iteration. The sources consulted give no validity range.
/// The range declared here is where Papay stays within
/// <see cref="MaxDeviationFromDak"/> (5 %) of DAK, the reference fit of the
/// same chart. That region is not a rectangle: the largest acceptable Ppr
/// grows from 0.53 at Tpr 1.0 to 7.45 at Tpr 1.4 and beyond, so it is
/// tabulated every 0.05 in Tpr (<see cref="MaxReducedPressure"/>), each node
/// rounded down to 0.01 and the lower of the two neighbouring nodes used in
/// between. Scanned for Ppr ≤ 15. Dense check (401 × 300 points inside the
/// region, 2026-10-04): largest deviation 4.998 %.
/// </summary>
public sealed class PapayModel : CorrespondingStatesCompressibilityModel
{
    /// <summary>Acceptance criterion used to build the validated range.</summary>
    public const double MaxDeviationFromDak = 0.05;

    private const double TableStart = 1.0;
    private const double TableStep = 0.05;

    // Largest Ppr with |Z_Papay − Z_DAK| / Z_DAK ≤ 5 % for every Ppr below it,
    // at Tpr = 1.00, 1.05, …, 3.00. 15.0 = no exceedance up to the scan limit.
    private static readonly double[] PressureLimits =
    {
        0.53, 0.67, 0.84, 1.02, 1.24, 1.51, 1.88, 4.90, 7.45, 7.20,
        6.97, 6.80, 6.69, 6.66, 6.71, 6.82, 7.01, 7.25, 7.55, 7.92,
        8.35, 8.84, 9.41, 10.04, 10.74, 11.52, 12.37, 13.31, 14.34, 15.00,
        15.00, 15.00, 9.33, 7.97, 7.40, 7.02, 6.73, 6.50, 6.30, 6.14,
        6.00
    };

    public PapayModel(PseudoCriticalProperties pseudoCritical)
        : base(pseudoCritical)
    {
    }

    public override string Name => "Papay";

    public override double CalculateFromReducedProperties(double reducedTemperature, double reducedPressure)
    {
        double tr = reducedTemperature;
        double pr = reducedPressure;

        return 1.0
               - 3.53 * pr / System.Math.Pow(10.0, 0.9813 * tr)
               + 0.274 * pr * pr / System.Math.Pow(10.0, 0.8157 * tr);
    }

    public override bool IsWithinValidatedReducedRange(double reducedTemperature, double reducedPressure) =>
        reducedTemperature >= TableStart
        && reducedTemperature <= TableStart + TableStep * (PressureLimits.Length - 1)
        && reducedPressure > 0.0
        && reducedPressure <= MaxReducedPressure(reducedTemperature);

    /// <summary>
    /// Largest Ppr inside the validated range at this Tpr (conservative:
    /// lower of the two bracketing table nodes); 0 outside 1 ≤ Tpr ≤ 3.
    /// </summary>
    public static double MaxReducedPressure(double reducedTemperature)
    {
        double position = (reducedTemperature - TableStart) / TableStep;

        if (!double.IsFinite(position) || position < -1e-9 || position > PressureLimits.Length - 1 + 1e-9)
        {
            return 0.0;
        }

        int i = System.Math.Clamp((int)System.Math.Floor(position + 1e-9), 0, PressureLimits.Length - 1);

        // Exactly on a node: that node's limit; otherwise the lower neighbour.
        if (System.Math.Abs(position - i) < 1e-9 || i == PressureLimits.Length - 1)
        {
            return PressureLimits[i];
        }

        return System.Math.Min(PressureLimits[i], PressureLimits[i + 1]);
    }
}
