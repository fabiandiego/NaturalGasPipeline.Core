using UnitsNet;

namespace NaturalGasPipeline.Hydraulics;

/// <summary>
/// Geometry of one pipe segment with a uniform slope.
/// ElevationChange = z_outlet − z_inlet (positive uphill).
/// </summary>
public sealed record PipeSegment
{
    public PipeSegment(
        Length internalDiameter,
        Length length,
        Length absoluteRoughness,
        Length elevationChange)
    {
        double d = internalDiameter.Meters;
        double l = length.Meters;
        double e = absoluteRoughness.Meters;
        double dz = elevationChange.Meters;

        if (!double.IsFinite(d) || d <= 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(internalDiameter), d, "Internal diameter must be finite and greater than zero.");
        }

        if (!double.IsFinite(l) || l <= 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(length), l, "Length must be finite and greater than zero.");
        }

        if (!double.IsFinite(e) || e < 0.0 || e >= d)
        {
            throw new ArgumentOutOfRangeException(
                nameof(absoluteRoughness), e, "Roughness must be finite, non-negative and smaller than the diameter.");
        }

        if (!double.IsFinite(dz) || System.Math.Abs(dz) > l)
        {
            throw new ArgumentOutOfRangeException(
                nameof(elevationChange), dz, "Elevation change must be finite and not larger than the segment length.");
        }

        InternalDiameter = internalDiameter;
        Length = length;
        AbsoluteRoughness = absoluteRoughness;
        ElevationChange = elevationChange;
    }

    public Length InternalDiameter { get; }

    public Length Length { get; }

    public Length AbsoluteRoughness { get; }

    public Length ElevationChange { get; }

    public static PipeSegment Horizontal(
        Length internalDiameter,
        Length length,
        Length absoluteRoughness) =>
        new(internalDiameter, length, absoluteRoughness, UnitsNet.Length.Zero);
}
