namespace NaturalGasPipeline.Hydraulics;

/// <summary>
/// Base for correlations that only describe turbulent flow.
///
/// Regime handling, shared by every derived model:
/// - Re &lt; 2300: laminar, f = 64 / Re (exact, Hagen–Poiseuille);
/// - 2300 ≤ Re &lt; 4000: transitional, the friction factor is not defined
///   by these correlations → <see cref="InvalidOperationException"/>
///   (use <see cref="ChurchillModel"/> if the transition must be crossed);
/// - Re ≥ 4000: the derived turbulent correlation.
///
/// The 2300 laminar limit is the usual one for pipe flow (e.g. Çengel &amp;
/// Cimbala); Arnold &amp; Stewart use 2000. Both bracket the same unstable band.
/// </summary>
public abstract class FlowRegimeFrictionModel : IFrictionFactorModel
{
    public const double LaminarLimit = 2300.0;

    public const double TurbulentLimit = 4000.0;

    public abstract string Name { get; }

    public double CalculateDarcyFrictionFactor(double reynoldsNumber, double relativeRoughness)
    {
        ValidateInputs(reynoldsNumber, relativeRoughness);

        if (reynoldsNumber < LaminarLimit)
        {
            return 64.0 / reynoldsNumber;
        }

        if (reynoldsNumber < TurbulentLimit)
        {
            throw new InvalidOperationException(
                $"Flow is in the transitional regime ({LaminarLimit} <= Re < {TurbulentLimit}); " +
                $"{Name} does not define a friction factor there. Re = {reynoldsNumber:F0}. " +
                "Use the Churchill model to cover the transition.");
        }

        double f = CalculateTurbulent(reynoldsNumber, relativeRoughness);

        if (!double.IsFinite(f) || f <= 0.0)
        {
            throw new InvalidOperationException(
                $"{Name} produced a non-physical friction factor {f} at Re = {reynoldsNumber}, ε/D = {relativeRoughness}.");
        }

        return f;
    }

    public bool IsWithinValidatedRange(double reynoldsNumber, double relativeRoughness) =>
        reynoldsNumber < LaminarLimit
        || IsTurbulentRangeValidated(reynoldsNumber, relativeRoughness);

    /// <summary>Turbulent correlation; called only for Re ≥ 4000.</summary>
    protected abstract double CalculateTurbulent(double reynoldsNumber, double relativeRoughness);

    /// <summary>Validated range of the turbulent correlation (Re ≥ 4000 assumed).</summary>
    protected abstract bool IsTurbulentRangeValidated(double reynoldsNumber, double relativeRoughness);

    internal static void ValidateInputs(double reynoldsNumber, double relativeRoughness)
    {
        if (!double.IsFinite(reynoldsNumber) || reynoldsNumber <= 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(reynoldsNumber), reynoldsNumber, "Reynolds number must be finite and greater than zero.");
        }

        if (!double.IsFinite(relativeRoughness) || relativeRoughness < 0.0 || relativeRoughness >= 1.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(relativeRoughness), relativeRoughness, "Relative roughness must be finite, non-negative and below 1.");
        }
    }
}
