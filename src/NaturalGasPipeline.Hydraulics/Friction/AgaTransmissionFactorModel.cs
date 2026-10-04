namespace NaturalGasPipeline.Hydraulics;

/// <summary>
/// AGA transmission factor (AGA NB-13 method, as presented by Menon,
/// Gas Pipeline Hydraulics), covering both turbulent zones:
///
///   partially turbulent (smooth-pipe law with drag factor Df):
///     Ft = 4·log10(Re / Ft) − 0.6
///     F  = 4·Df·log10(Re / (1.4125·Ft))
///   fully turbulent (rough-pipe law):
///     F  = 4·log10(3.7·D/ε)
///   F = min(partially turbulent, fully turbulent),  f (Darcy) = 4 / F².
///
/// With Df = 1 the partially turbulent value equals Ft
/// (4·log10 1.4125 = 0.59995): Ft reproduces the AGA table printed by
/// Arnold &amp; Stewart, Figure 6.16 (e.g. 22.028 at Re = 10⁷), see tests.
///
/// The drag factor accounts for bends and fittings (bend index); typical
/// values 0.90–0.99. It is an explicit input: there is no default.
/// Laminar and transitional flow (Re &lt; 4000) are outside the method.
/// </summary>
public sealed class AgaTransmissionFactorModel : IFrictionFactorModel
{
    public AgaTransmissionFactorModel(double dragFactor)
    {
        if (!double.IsFinite(dragFactor) || dragFactor <= 0.0 || dragFactor > 1.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(dragFactor), dragFactor, "Drag factor must be in (0, 1].");
        }

        DragFactor = dragFactor;
    }

    public double DragFactor { get; }

    public string Name => $"AGA (Df = {DragFactor})";

    public double CalculateDarcyFrictionFactor(double reynoldsNumber, double relativeRoughness)
    {
        FlowRegimeFrictionModel.ValidateInputs(reynoldsNumber, relativeRoughness);

        return FrictionFactorModelExtensions.DarcyFromTransmissionFactor(
            TransmissionFactor(reynoldsNumber, relativeRoughness));
    }

    /// <summary>F = min(partially turbulent, fully turbulent).</summary>
    public double TransmissionFactor(double reynoldsNumber, double relativeRoughness)
    {
        FlowRegimeFrictionModel.ValidateInputs(reynoldsNumber, relativeRoughness);

        double smooth = SmoothPipeTransmissionFactor(reynoldsNumber);
        double partial = 4.0 * DragFactor * System.Math.Log10(reynoldsNumber / (1.4125 * smooth));

        return relativeRoughness > 0.0
            ? System.Math.Min(partial, AgaFullyTurbulentModel.TransmissionFactor(relativeRoughness))
            : partial;
    }

    /// <summary>
    /// Smooth-pipe transmission factor Ft = 4·log10(Re/Ft) − 0.6, solved by
    /// fixed-point iteration (contraction factor 4/(ln10·Ft) ≈ 0.08).
    /// </summary>
    public static double SmoothPipeTransmissionFactor(double reynoldsNumber)
    {
        if (!double.IsFinite(reynoldsNumber) || reynoldsNumber < FlowRegimeFrictionModel.TurbulentLimit)
        {
            throw new ArgumentOutOfRangeException(
                nameof(reynoldsNumber), reynoldsNumber, "The AGA method needs turbulent flow (Re ≥ 4000).");
        }

        double ft = 20.0;

        for (int i = 0; i < 100; i++)
        {
            double updated = 4.0 * System.Math.Log10(reynoldsNumber / ft) - 0.6;

            if (System.Math.Abs(updated - ft) < 1e-13 * ft)
            {
                return updated;
            }

            ft = updated;
        }

        throw new InvalidOperationException("AGA smooth-pipe transmission factor did not converge.");
    }

    public bool IsWithinValidatedRange(double reynoldsNumber, double relativeRoughness) =>
        reynoldsNumber >= FlowRegimeFrictionModel.TurbulentLimit;
}
