using UnitsNet;

namespace NaturalGasPipeline.Core.Flow;

/// <summary>
/// Conversions between mass flow, actual volumetric flow and standard
/// volumetric flow. Every input is validated: no zero divisions and no
/// silent defaults.
/// </summary>
public static class FlowRateConverter
{
    private const double R = 8.31446261815324; // J/(mol·K)

    /// <summary>ṁ = Q_actual · ρ(P, T)</summary>
    public static MassFlow ConvertActualVolumeToMass(
        ActualVolumeFlow actualVolumetricFlow,
        Density flowingDensity)
    {
        double q = actualVolumetricFlow.Value.CubicMetersPerSecond;
        double rho = flowingDensity.KilogramsPerCubicMeter;

        RequireFinite(q, nameof(actualVolumetricFlow));
        RequirePositive(rho, nameof(flowingDensity));

        return MassFlow.FromKilogramsPerSecond(q * rho);
    }

    /// <summary>Q_actual = ṁ / ρ(P, T)</summary>
    public static ActualVolumeFlow ConvertMassToActualVolume(
        MassFlow massFlow,
        Density flowingDensity)
    {
        double m = massFlow.KilogramsPerSecond;
        double rho = flowingDensity.KilogramsPerCubicMeter;

        RequireFinite(m, nameof(massFlow));
        RequirePositive(rho, nameof(flowingDensity));

        return ActualVolumeFlow.FromCubicMetersPerSecond(m / rho);
    }

    /// <summary>Q_std = ṁ · Z_std · R · T_std / (P_std · M)</summary>
    public static StandardVolumeFlow ConvertMassToStandardVolume(
        MassFlow massFlow,
        MolarMass molarMass,
        StandardFlowConditions standardConditions)
    {
        ArgumentNullException.ThrowIfNull(standardConditions);
        standardConditions.Validate();

        double m = massFlow.KilogramsPerSecond;
        double mm = molarMass.KilogramsPerMole;

        RequireFinite(m, nameof(massFlow));
        RequirePositive(mm, nameof(molarMass));

        double q =
            m
            * standardConditions.CompressibilityFactor
            * R
            * standardConditions.Temperature.Kelvins
            / (standardConditions.Pressure.Pascals * mm);

        return new StandardVolumeFlow(
            VolumeFlow.FromCubicMetersPerSecond(q),
            standardConditions);
    }

    /// <summary>ṁ = Q_std · P_std · M / (Z_std · R · T_std)</summary>
    public static MassFlow ConvertStandardVolumeToMass(
        StandardVolumeFlow standardVolumetricFlow,
        MolarMass molarMass)
    {
        ArgumentNullException.ThrowIfNull(standardVolumetricFlow);

        StandardFlowConditions conditions = standardVolumetricFlow.Conditions;
        conditions.Validate();

        double q = standardVolumetricFlow.Value.CubicMetersPerSecond;
        double mm = molarMass.KilogramsPerMole;

        RequireFinite(q, nameof(standardVolumetricFlow));
        RequirePositive(mm, nameof(molarMass));

        double m =
            q
            * conditions.Pressure.Pascals
            * mm
            / (conditions.CompressibilityFactor
               * R
               * conditions.Temperature.Kelvins);

        return MassFlow.FromKilogramsPerSecond(m);
    }

    private static void RequireFinite(double value, string name)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(
                name, value, "Value must be finite.");
        }
    }

    private static void RequirePositive(double value, string name)
    {
        if (!double.IsFinite(value) || value <= 0.0)
        {
            throw new ArgumentOutOfRangeException(
                name, value, "Value must be finite and greater than zero.");
        }
    }
}
