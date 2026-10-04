using UnitsNet;

namespace NaturalGasPipeline.Thermodynamics;

/// <summary>
/// Pseudo-critical properties from the gas specific gravity γ (air = 1),
/// for when the composition is not known.
///
/// To add a correlation: implement this interface, state the source, the
/// units of the published coefficients and the γ range in the XML
/// documentation, and convert to SI inside the implementation.
/// </summary>
public interface IPseudoCriticalCorrelation
{
    /// <summary>Short, human-readable name for reports.</summary>
    string Name { get; }

    /// <summary>Tpc and Ppc for specific gravity γ.</summary>
    PseudoCriticalProperties Calculate(double specificGravity);

    /// <summary>True when γ lies in the range the correlation was fitted on.</summary>
    bool IsWithinValidatedRange(double specificGravity);
}

/// <summary>
/// Shared plumbing for the quadratic field-unit correlations
/// Tpc [°R] = t₀ + t₁·γ + t₂·γ², Ppc [psia] = p₀ + p₁·γ + p₂·γ².
/// </summary>
public abstract class QuadraticPseudoCriticalCorrelation : IPseudoCriticalCorrelation
{
    public abstract string Name { get; }

    public PseudoCriticalProperties Calculate(double specificGravity)
    {
        if (!double.IsFinite(specificGravity) || specificGravity <= 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(specificGravity), specificGravity, "Specific gravity must be finite and greater than zero.");
        }

        (double t0, double t1, double t2, double p0, double p1, double p2) = Coefficients;
        double g = specificGravity;

        double rankine = t0 + t1 * g + t2 * g * g;
        double psia = p0 + p1 * g + p2 * g * g;

        return new PseudoCriticalProperties(
            Temperature.FromDegreesRankine(rankine),
            Pressure.FromPoundsForcePerSquareInch(psia));
    }

    public abstract bool IsWithinValidatedRange(double specificGravity);

    /// <summary>(t₀, t₁, t₂) in °R and (p₀, p₁, p₂) in psia, as published.</summary>
    protected abstract (double T0, double T1, double T2, double P0, double P1, double P2) Coefficients { get; }
}

/// <summary>
/// Sutton (1985), SPE 14265:
///   Tpc [°R]   = 169.2 + 349.5·γ − 74.0·γ²
///   Ppc [psia] = 756.8 − 131.0·γ − 3.6·γ²
/// Reported range 0.57 &lt; γ &lt; 1.68, hydrocarbon gas (rich gases included).
/// For CO₂/H₂S apply <see cref="WichertAzizCorrection"/> afterwards.
/// Coefficients as in Whitson &amp; Brulé, SPE Phase Behavior Monograph
/// (Eq. 3.60–3.61); some textbooks print the Ppc slope as 131.07, a
/// difference below 0.01 % of Ppc for natural gas.
/// </summary>
public sealed class SuttonPseudoCriticalCorrelation : QuadraticPseudoCriticalCorrelation
{
    public override string Name => "Sutton (1985)";

    protected override (double T0, double T1, double T2, double P0, double P1, double P2) Coefficients =>
        (169.2, 349.5, -74.0, 756.8, -131.0, -3.6);

    public override bool IsWithinValidatedRange(double specificGravity) =>
        specificGravity > 0.57 && specificGravity < 1.68;
}

/// <summary>Which of Standing's two curves to use.</summary>
public enum StandingGasSystem
{
    /// <summary>Dry hydrocarbon gas: Tpc = 168 + 325γ − 12.5γ², Ppc = p₀ + 15.0γ − 37.5γ².</summary>
    NaturalGas,

    /// <summary>Wet gas / gas condensate: Tpc = 187 + 330γ − 71.5γ², Ppc = 706 − 51.7γ − 11.1γ².</summary>
    GasCondensate
}

/// <summary>
/// Published versions of Standing's dry-gas Ppc curve, which differ in the
/// intercept p₀. Both are in use; the caller states which one its data follow.
/// </summary>
public enum StandingCoefficientSet
{
    /// <summary>p₀ = 667 psia: Whitson &amp; Brulé, SPE Phase Behavior Monograph.</summary>
    WhitsonBrule,

    /// <summary>
    /// p₀ = 677 psia: Ahmed, Reservoir Engineering Handbook, and the university
    /// course notes that follow it (e.g. King Saud University PGE 362).
    /// </summary>
    Ahmed
}

/// <summary>
/// Standing (1977) pseudo-critical curves, field units (°R, psia), for
/// hydrocarbon gas without non-hydrocarbons (apply
/// <see cref="WichertAzizCorrection"/> or <see cref="CarrKobayashiBurrowsCorrection"/>
/// for CO₂, H₂S, N₂).
///
/// Sources disagree on the dry-gas Ppc intercept (667 or 677 psia, 1.5 % in
/// Ppc); <see cref="StandingCoefficientSet"/> makes the choice explicit.
/// The condensate curve is the same in both (Ppc slope −51.7: Ppc must fall
/// as the gas gets heavier). Whitson &amp; Brulé recommend the dry-gas curve
/// for γ ≤ 0.75 and the condensate curve above;
/// <see cref="IsWithinValidatedRange"/> follows that.
/// </summary>
public sealed class StandingPseudoCriticalCorrelation : QuadraticPseudoCriticalCorrelation
{
    public StandingPseudoCriticalCorrelation(
        StandingGasSystem gasSystem,
        StandingCoefficientSet coefficientSet)
    {
        if (!Enum.IsDefined(gasSystem))
        {
            throw new ArgumentOutOfRangeException(nameof(gasSystem), gasSystem, "Unknown gas system.");
        }

        if (!Enum.IsDefined(coefficientSet))
        {
            throw new ArgumentOutOfRangeException(nameof(coefficientSet), coefficientSet, "Unknown coefficient set.");
        }

        GasSystem = gasSystem;
        CoefficientSet = coefficientSet;
    }

    public StandingGasSystem GasSystem { get; }

    public StandingCoefficientSet CoefficientSet { get; }

    public override string Name => $"Standing (1977), {GasSystem}, {CoefficientSet}";

    protected override (double T0, double T1, double T2, double P0, double P1, double P2) Coefficients =>
        GasSystem switch
        {
            StandingGasSystem.NaturalGas => (
                168.0, 325.0, -12.5,
                CoefficientSet == StandingCoefficientSet.Ahmed ? 677.0 : 667.0, 15.0, -37.5),
            _ => (187.0, 330.0, -71.5, 706.0, -51.7, -11.1)
        };

    public override bool IsWithinValidatedRange(double specificGravity) =>
        GasSystem == StandingGasSystem.NaturalGas
            ? specificGravity > 0.0 && specificGravity <= 0.75
            : specificGravity > 0.75;
}

/// <summary>
/// Wichert &amp; Aziz (1972) correction of pseudo-critical properties for
/// sour gas (field units, as published):
///   A = y_CO₂ + y_H₂S,  B = y_H₂S
///   ε [°R] = 120·(A^0.9 − A^1.6) + 15·(B^0.5 − B^4)
///   T'pc = Tpc − ε
///   P'pc = Ppc · T'pc / (Tpc + B·(1 − B)·ε)
/// Reported range: y_CO₂ &lt; 0.544, y_H₂S &lt; 0.738.
/// Never applied implicitly: the caller decides whether the gas needs it.
/// </summary>
public static class WichertAzizCorrection
{
    public static PseudoCriticalProperties Apply(
        PseudoCriticalProperties uncorrected,
        double carbonDioxideMoleFraction,
        double hydrogenSulfideMoleFraction)
    {
        ArgumentNullException.ThrowIfNull(uncorrected);
        ValidateFraction(carbonDioxideMoleFraction, nameof(carbonDioxideMoleFraction));
        ValidateFraction(hydrogenSulfideMoleFraction, nameof(hydrogenSulfideMoleFraction));

        double a = carbonDioxideMoleFraction + hydrogenSulfideMoleFraction;
        double b = hydrogenSulfideMoleFraction;

        if (a > 1.0)
        {
            throw new ArgumentException("y_CO₂ + y_H₂S cannot exceed 1.");
        }

        double epsilon = AdjustmentRankine(a, b);
        double tpc = uncorrected.Temperature.DegreesRankine;
        double ppc = uncorrected.Pressure.PoundsForcePerSquareInch;

        double tpcCorrected = tpc - epsilon;
        double ppcCorrected = ppc * tpcCorrected / (tpc + b * (1.0 - b) * epsilon);

        return new PseudoCriticalProperties(
            Temperature.FromDegreesRankine(tpcCorrected),
            Pressure.FromPoundsForcePerSquareInch(ppcCorrected));
    }

    /// <summary>ε in °R (equivalently, ε/1.8 in K).</summary>
    public static double AdjustmentRankine(double acidGasMoleFraction, double hydrogenSulfideMoleFraction) =>
        120.0 * (System.Math.Pow(acidGasMoleFraction, 0.9) - System.Math.Pow(acidGasMoleFraction, 1.6))
        + 15.0 * (System.Math.Sqrt(hydrogenSulfideMoleFraction) - System.Math.Pow(hydrogenSulfideMoleFraction, 4.0));

    public static bool IsWithinValidatedRange(double carbonDioxideMoleFraction, double hydrogenSulfideMoleFraction) =>
        carbonDioxideMoleFraction < 0.544 && hydrogenSulfideMoleFraction < 0.738;

    private static void ValidateFraction(double value, string name)
    {
        if (!double.IsFinite(value) || value < 0.0 || value > 1.0)
        {
            throw new ArgumentOutOfRangeException(name, value, "Mole fraction must be between 0 and 1.");
        }
    }
}

/// <summary>
/// Carr, Kobayashi &amp; Burrows (1954) correction of pseudo-critical
/// properties for non-hydrocarbons, used when only the gas gravity is known
/// (field units, as published):
///   T'pc [°R]   = Tpc − 80·y_CO₂ + 130·y_H₂S − 250·y_N₂
///   P'pc [psia] = Ppc + 440·y_CO₂ + 600·y_H₂S − 170·y_N₂
/// Coefficients as in King Saud University PGE 362 (Khamis, 2015) and
/// Ahmed, Reservoir Engineering Handbook. Never applied implicitly.
/// </summary>
public static class CarrKobayashiBurrowsCorrection
{
    public static PseudoCriticalProperties Apply(
        PseudoCriticalProperties uncorrected,
        double carbonDioxideMoleFraction,
        double hydrogenSulfideMoleFraction,
        double nitrogenMoleFraction)
    {
        ArgumentNullException.ThrowIfNull(uncorrected);
        ValidateFraction(carbonDioxideMoleFraction, nameof(carbonDioxideMoleFraction));
        ValidateFraction(hydrogenSulfideMoleFraction, nameof(hydrogenSulfideMoleFraction));
        ValidateFraction(nitrogenMoleFraction, nameof(nitrogenMoleFraction));

        if (carbonDioxideMoleFraction + hydrogenSulfideMoleFraction + nitrogenMoleFraction > 1.0)
        {
            throw new ArgumentException("y_CO₂ + y_H₂S + y_N₂ cannot exceed 1.");
        }

        double tpc = uncorrected.Temperature.DegreesRankine
                     - 80.0 * carbonDioxideMoleFraction
                     + 130.0 * hydrogenSulfideMoleFraction
                     - 250.0 * nitrogenMoleFraction;

        double ppc = uncorrected.Pressure.PoundsForcePerSquareInch
                     + 440.0 * carbonDioxideMoleFraction
                     + 600.0 * hydrogenSulfideMoleFraction
                     - 170.0 * nitrogenMoleFraction;

        return new PseudoCriticalProperties(
            Temperature.FromDegreesRankine(tpc),
            Pressure.FromPoundsForcePerSquareInch(ppc));
    }

    private static void ValidateFraction(double value, string name)
    {
        if (!double.IsFinite(value) || value < 0.0 || value > 1.0)
        {
            throw new ArgumentOutOfRangeException(name, value, "Mole fraction must be between 0 and 1.");
        }
    }
}
