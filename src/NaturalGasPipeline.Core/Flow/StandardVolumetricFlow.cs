using NaturalGasPipeline.Core.Common;
using UnitsNet;

namespace NaturalGasPipeline.Core.Flow;

/// <summary>
/// Volumetric flow rate referred to a set of standard (base) conditions.
///
/// A standard volume is meaningless without its reference conditions
/// (15 °C / 101.325 kPa, 60 °F / 14.696 psia, 0 °C "normal", ...), so the
/// conditions travel with the value. This removes the silent assumption of
/// which standard a number belongs to.
/// </summary>
public sealed record StandardVolumetricFlow
{
    public StandardVolumetricFlow(
        VolumeFlow value,
        StandardFlowConditions conditions)
    {
        Guard.NotNull(conditions);

        conditions.Validate();

        Value = value;
        Conditions = conditions;
    }

    public VolumeFlow Value { get; }

    public StandardFlowConditions Conditions { get; }

    public override string ToString() =>
        $"{Value.CubicMetersPerSecond} m³/s (standard at " +
        $"{Conditions.Temperature.Kelvins} K, " +
        $"{Conditions.Pressure.Pascals} Pa, " +
        $"Z = {Conditions.CompressibilityFactor})";
}
