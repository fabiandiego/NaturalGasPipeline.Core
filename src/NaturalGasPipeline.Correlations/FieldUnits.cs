using NaturalGasPipeline.Core.Common;
using NaturalGasPipeline.Core.Flow;
using UnitsNet;

namespace NaturalGasPipeline.Correlations;

/// <summary>
/// Conversions to the USCS field units in which the empirical constants are
/// published. Used only inside the equations.
/// </summary>
internal static class FieldUnits
{
    /// <summary>1 ft³ = 0.028316846592 m³ (exact, international foot).</summary>
    private const double CubicMetersPerCubicFoot = 0.028316846592;

    private const double SecondsPerDay = 86400.0;

    private static readonly double PascalsPerPsi = Pressure.FromPoundsForcePerSquareInch(1.0).Pascals;

    /// <summary>N in psia² from Pa².</summary>
    public static double PsiSquared(double pascalsSquared) =>
        pascalsSquared / (PascalsPerPsi * PascalsPerPsi);

    /// <summary>N in Pa² from psia².</summary>
    public static double PascalsSquared(double psiSquared) =>
        psiSquared * PascalsPerPsi * PascalsPerPsi;

    /// <summary>Standard cubic feet per day.</summary>
    public static double CubicFeetPerDay(VolumeFlow flow) =>
        flow.CubicMetersPerSecond * SecondsPerDay / CubicMetersPerCubicFoot;

    public static VolumeFlow FromCubicFeetPerDay(double scfd) =>
        VolumeFlow.FromCubicMetersPerSecond(scfd * CubicMetersPerCubicFoot / SecondsPerDay);

    /// <summary>Tb / Pb in °R / psia.</summary>
    public static double BaseRatio(StandardFlowConditions baseConditions) =>
        baseConditions.Temperature.DegreesRankine / baseConditions.Pressure.PoundsForcePerSquareInch;

    /// <summary>
    /// Reynolds number of the standard flow Q: Re = 4·ṁ/(π·D·μ), with
    /// ṁ = Q·ρ_b and ρ_b = P_b·M / (Z_b·R·T_b) (the base Z is explicit).
    /// </summary>
    public static double Reynolds(VolumeFlow standardFlow, FlowEquationTerms terms, DynamicViscosity viscosity)
    {
        StandardFlowConditions b = terms.BaseConditions;

        double rhoBase = b.Pressure.Pascals * terms.MolarMass.KilogramsPerMole
                         / (b.CompressibilityFactor * PhysicalConstants.MolarGasConstant * b.Temperature.Kelvins);

        double massFlow = System.Math.Abs(standardFlow.CubicMetersPerSecond) * rhoBase;

        return 4.0 * massFlow / (System.Math.PI * terms.InternalDiameter.Meters * viscosity.PascalSeconds);
    }
}
