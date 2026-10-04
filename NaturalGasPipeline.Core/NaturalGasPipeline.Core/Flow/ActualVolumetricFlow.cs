using UnitsNet;

namespace NaturalGasPipeline.Core.Flow;

/// <summary>
/// Volumetric flow rate at the actual (flowing) pressure and temperature.
///
/// Wrapping <see cref="VolumeFlow"/> in a dedicated type makes it impossible
/// to pass a standard volumetric flow where an actual one is required:
/// the compiler rejects the mistake instead of the result being silently wrong.
/// </summary>
public readonly record struct ActualVolumetricFlow(VolumeFlow Value)
{
    public static ActualVolumetricFlow FromCubicMetersPerSecond(double value) =>
        new(VolumeFlow.FromCubicMetersPerSecond(value));

    public override string ToString() =>
        $"{Value.CubicMetersPerSecond} m³/s (actual)";
}
