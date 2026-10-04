using UnitsNet;

namespace NaturalGasPipeline.Hydraulics;

/// <summary>
/// Kept for compatibility: Darcy friction factor with the Colebrook-White
/// model (laminar 64/Re below Re = 2300, transitional regime rejected).
/// New code should depend on <see cref="IFrictionFactorModel"/> so the
/// correlation can be chosen; see <see cref="FrictionFactorModelFactory"/>.
/// </summary>
public static class FrictionFactorCalculator
{
    private static readonly ColebrookWhiteModel Colebrook = new();

    public static double CalculateDarcyFrictionFactor(
        double reynoldsNumber,
        Length internalDiameter,
        Length absoluteRoughness) =>
        Colebrook.CalculateDarcyFrictionFactor(
            reynoldsNumber,
            internalDiameter,
            absoluteRoughness);
}
