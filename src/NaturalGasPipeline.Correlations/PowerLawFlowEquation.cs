using NaturalGasPipeline.Core.Common;
using UnitsNet;

namespace NaturalGasPipeline.Correlations;

/// <summary>
/// Empirical flow equations of the common form (USCS field units):
///
///   Q = C · E · (Tb/Pb)^a · [ (P1² − e^s·P2²) / (G^b · Tf · Le · Z) ]^c · D^d
///
///   Q  standard flow, ft³/day at (Tb, Pb)     Tb, Tf  °R
///   Pb, P1, P2  psia                           Le  miles
///   D  internal diameter, in                   G   specific gravity (air = 1)
///   E  pipeline efficiency (explicit input; 1.0 for new pipe)
///
/// The friction factor is built into the constants (Weymouth: f ∝ D^(−1/3);
/// Panhandle: f ∝ Re^(−n)). Constants from Menon, Gas Pipeline Hydraulics
/// (2005), USCS forms; they reproduce exactly (0.01 psia) the Panhandle A,
/// Panhandle B and Weymouth results of the CED Engineering course
/// "Gas Pipeline Hydraulics" (P06-001), Example 11, and Arnold &amp; Stewart
/// Examples 6.2, 6.5 and 6.6a — see GasFlowEquationValidationTests.
/// </summary>
public sealed class PowerLawFlowEquation : IGasFlowEquation
{
    private readonly double _c;
    private readonly double _a;
    private readonly double _b;
    private readonly double _exponent;
    private readonly double _d;
    private readonly Length? _minimumRecommendedDiameter;
    private readonly Length? _maximumRecommendedDiameter;

    public PowerLawFlowEquation(
        string name,
        double coefficient,
        double baseRatioExponent,
        double gravityExponent,
        double pressureTermExponent,
        double diameterExponent,
        double efficiency,
        Length? minimumRecommendedDiameter = null,
        Length? maximumRecommendedDiameter = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A name is required.", nameof(name));
        }

        foreach ((double value, string label) in new[]
                 {
                     (coefficient, nameof(coefficient)),
                     (baseRatioExponent, nameof(baseRatioExponent)),
                     (gravityExponent, nameof(gravityExponent)),
                     (pressureTermExponent, nameof(pressureTermExponent)),
                     (diameterExponent, nameof(diameterExponent)),
                     (efficiency, nameof(efficiency))
                 })
        {
            if (!double.IsFinite(value) || value <= 0.0)
            {
                throw new ArgumentOutOfRangeException(label, value, "Value must be finite and greater than zero.");
            }
        }

        Name = name;
        _c = coefficient;
        _a = baseRatioExponent;
        _b = gravityExponent;
        _exponent = pressureTermExponent;
        _d = diameterExponent;
        Efficiency = efficiency;
        _minimumRecommendedDiameter = minimumRecommendedDiameter;
        _maximumRecommendedDiameter = maximumRecommendedDiameter;
    }

    public string Name { get; }

    public double Efficiency { get; }

    public FlowEquationEvaluation CalculateFlow(double pressureTermPascalsSquared, FlowEquationTerms terms)
    {
        Guard.NotNull(terms);
        RequirePositive(pressureTermPascalsSquared, nameof(pressureTermPascalsSquared));

        double n = FieldUnits.PsiSquared(pressureTermPascalsSquared);
        double q = Constant(terms) * System.Math.Pow(n / Denominator(terms), _exponent);

        return new FlowEquationEvaluation(pressureTermPascalsSquared, FieldUnits.FromCubicFeetPerDay(q), null, null);
    }

    public FlowEquationEvaluation CalculatePressureTerm(VolumeFlow standardFlow, FlowEquationTerms terms)
    {
        Guard.NotNull(terms);

        double q = FieldUnits.CubicFeetPerDay(standardFlow);
        RequirePositive(q, nameof(standardFlow));

        double n = System.Math.Pow(q / Constant(terms), 1.0 / _exponent) * Denominator(terms);

        return new FlowEquationEvaluation(FieldUnits.PascalsSquared(n), standardFlow, null, null);
    }

    public IEnumerable<string> CheckApplicability(FlowEquationTerms terms, FlowEquationEvaluation evaluation)
    {
        Guard.NotNull(terms);

        if (Efficiency > 1.0)
        {
            yield return $"{Name}: efficiency E = {Efficiency} is above 1.";
        }

        double d = terms.InternalDiameter.Inches;

        if (_minimumRecommendedDiameter is Length min && d < min.Inches)
        {
            yield return $"{Name}: internal diameter {d:F2} in is below the recommended {min.Inches:F1} in.";
        }

        if (_maximumRecommendedDiameter is Length max && d > max.Inches)
        {
            yield return $"{Name}: internal diameter {d:F2} in is above the recommended {max.Inches:F1} in " +
                         "(results become increasingly conservative).";
        }
    }

    // C · E · (Tb/Pb)^a · D^d
    private double Constant(FlowEquationTerms terms) =>
        _c * Efficiency
           * System.Math.Pow(FieldUnits.BaseRatio(terms.BaseConditions), _a)
           * System.Math.Pow(terms.InternalDiameter.Inches, _d);

    // G^b · Tf · Le · Z
    private double Denominator(FlowEquationTerms terms) =>
        System.Math.Pow(terms.SpecificGravity, _b)
        * terms.FlowingTemperature.DegreesRankine
        * terms.EquivalentLength.Miles
        * terms.CompressibilityFactor;

    private static void RequirePositive(double value, string name)
    {
        if (!double.IsFinite(value) || value <= 0.0)
        {
            throw new ArgumentOutOfRangeException(name, value, "Value must be finite and greater than zero.");
        }
    }
}

/// <summary>Built-in empirical equations.</summary>
public static class GasFlowEquations
{
    /// <summary>
    /// Weymouth: C = 433.5, a = 1, b = 1, c = 0.5, d = 2.667.
    /// Arnold &amp; Stewart: most accurate below 12 in; increasingly
    /// conservative (under-predicts capacity) for larger pipe.
    /// </summary>
    public static PowerLawFlowEquation Weymouth(double efficiency) =>
        new("Weymouth", 433.5, 1.0, 1.0, 0.5, 2.667, efficiency,
            maximumRecommendedDiameter: Length.FromInches(12.0));

    /// <summary>
    /// Panhandle A: C = 435.87, a = 1.0788, b = 0.8539, c = 0.5394, d = 2.6182.
    /// Developed for long, large-diameter (12 in and above) transmission lines.
    /// </summary>
    public static PowerLawFlowEquation PanhandleA(double efficiency) =>
        new("Panhandle A", 435.87, 1.0788, 0.8539, 0.5394, 2.6182, efficiency,
            minimumRecommendedDiameter: Length.FromInches(12.0));

    /// <summary>
    /// Panhandle B: C = 737, a = 1.02, b = 0.961, c = 0.51, d = 2.53.
    /// Developed for long, large-diameter (12 in and above) transmission lines.
    /// </summary>
    public static PowerLawFlowEquation PanhandleB(double efficiency) =>
        new("Panhandle B", 737.0, 1.02, 0.961, 0.51, 2.53, efficiency,
            minimumRecommendedDiameter: Length.FromInches(12.0));
}
