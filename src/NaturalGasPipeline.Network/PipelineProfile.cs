using NaturalGasPipeline.Core.Pipeline;
using UnitsNet;

namespace NaturalGasPipeline.Network;

/// <summary>A surveyed point of the route: distance along the pipe and elevation.</summary>
public readonly record struct ProfilePoint(Length Chainage, Length Elevation);

/// <summary>
/// Builds pipe segments from a route profile. Each pair of consecutive
/// points becomes one segment with a uniform slope; long segments can be
/// subdivided, because Z, μ and f are evaluated once per segment (at P_avg):
/// the error of that approximation falls as 1/N² (see tests).
/// </summary>
public static class PipelineProfile
{
    /// <param name="points">At least two points, chainage strictly increasing (measured along the pipe).</param>
    public static IReadOnlyList<PipeSegment> ToSegments(
        IReadOnlyList<ProfilePoint> points,
        Length internalDiameter,
        Length absoluteRoughness)
    {
        ArgumentNullException.ThrowIfNull(points);

        if (points.Count < 2)
        {
            throw new ArgumentException("A profile needs at least two points.", nameof(points));
        }

        var segments = new List<PipeSegment>(points.Count - 1);

        for (int i = 1; i < points.Count; i++)
        {
            double length = points[i].Chainage.Meters - points[i - 1].Chainage.Meters;
            double rise = points[i].Elevation.Meters - points[i - 1].Elevation.Meters;

            if (!double.IsFinite(length) || length <= 0.0)
            {
                throw new ArgumentException($"Chainage must increase strictly (points {i - 1} → {i}).", nameof(points));
            }

            if (System.Math.Abs(rise) > length)
            {
                throw new ArgumentException(
                    $"Elevation change {rise} m exceeds the pipe length {length} m between points {i - 1} and {i}.", nameof(points));
            }

            segments.Add(new PipeSegment(internalDiameter, Length.FromMeters(length), absoluteRoughness, Length.FromMeters(rise)));
        }

        return segments;
    }

    /// <summary>Splits a segment into N equal parts with the same slope.</summary>
    public static IReadOnlyList<PipeSegment> Subdivide(PipeSegment segment, int parts)
    {
        ArgumentNullException.ThrowIfNull(segment);

        if (parts < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(parts), parts, "At least one part is required.");
        }

        var length = Length.FromMeters(segment.Length.Meters / parts);
        var rise = Length.FromMeters(segment.ElevationChange.Meters / parts);

        return Enumerable.Range(0, parts)
            .Select(_ => new PipeSegment(segment.InternalDiameter, length, segment.AbsoluteRoughness, rise))
            .ToList();
    }
}
