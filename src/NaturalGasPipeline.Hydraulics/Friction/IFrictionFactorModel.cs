using NaturalGasPipeline.Core.Common;
using UnitsNet;

namespace NaturalGasPipeline.Hydraulics;

/// <summary>
/// Darcy (Moody) friction factor model, f = 4 · f_Fanning.
///
/// To add a model: implement this interface (or derive from
/// <see cref="FlowRegimeFrictionModel"/> for a turbulent-only correlation),
/// state its source and validated range in the XML documentation, and add
/// implementation and accuracy tests.
/// </summary>
public interface IFrictionFactorModel
{
    /// <summary>Short, human-readable model name for reports.</summary>
    string Name { get; }

    /// <summary>
    /// Darcy friction factor for Reynolds number Re and relative roughness ε/D.
    /// </summary>
    double CalculateDarcyFrictionFactor(double reynoldsNumber, double relativeRoughness);

    /// <summary>
    /// True when (Re, ε/D) lies inside the range for which the model was
    /// developed or validated. Outside it the model still returns a value;
    /// callers should report a warning rather than fail.
    /// </summary>
    bool IsWithinValidatedRange(double reynoldsNumber, double relativeRoughness);
}

/// <summary>Helpers that keep units explicit at the model boundary.</summary>
public static class FrictionFactorModelExtensions
{
    /// <summary>Darcy friction factor from dimensional pipe data.</summary>
    public static double CalculateDarcyFrictionFactor(
        this IFrictionFactorModel model,
        double reynoldsNumber,
        Length internalDiameter,
        Length absoluteRoughness)
    {
        Guard.NotNull(model);

        return model.CalculateDarcyFrictionFactor(
            reynoldsNumber,
            RelativeRoughness(internalDiameter, absoluteRoughness));
    }

    /// <summary>ε/D, validated (0 ≤ ε &lt; D).</summary>
    public static double RelativeRoughness(
        Length internalDiameter,
        Length absoluteRoughness)
    {
        double d = internalDiameter.Meters;
        double e = absoluteRoughness.Meters;

        if (!double.IsFinite(d) || d <= 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(internalDiameter), d, "Internal diameter must be finite and greater than zero.");
        }

        if (!double.IsFinite(e) || e < 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(absoluteRoughness), e, "Absolute roughness must be finite and non-negative.");
        }

        if (e >= d)
        {
            throw new ArgumentException(
                "Absolute roughness must be smaller than the internal diameter.",
                nameof(absoluteRoughness));
        }

        return e / d;
    }

    /// <summary>Transmission factor F = 2 / √f (Darcy), as used by AGA and the General Flow Equation.</summary>
    public static double TransmissionFactorFromDarcy(double darcyFrictionFactor)
    {
        RequirePositive(darcyFrictionFactor, nameof(darcyFrictionFactor));

        return 2.0 / System.Math.Sqrt(darcyFrictionFactor);
    }

    /// <summary>Darcy friction factor f = 4 / F².</summary>
    public static double DarcyFromTransmissionFactor(double transmissionFactor)
    {
        RequirePositive(transmissionFactor, nameof(transmissionFactor));

        return 4.0 / (transmissionFactor * transmissionFactor);
    }

    internal static void RequirePositive(double value, string name)
    {
        if (!double.IsFinite(value) || value <= 0.0)
        {
            throw new ArgumentOutOfRangeException(name, value, "Value must be finite and greater than zero.");
        }
    }
}
