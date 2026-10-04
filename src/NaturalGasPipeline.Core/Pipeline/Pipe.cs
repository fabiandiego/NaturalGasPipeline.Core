using UnitsNet;

namespace NaturalGasPipeline.Core.Pipeline;

/// <summary>
/// Physical pipe as specified (outer diameter and wall thickness, as in
/// API 5L / ASME B31.8 line-pipe tables). Mechanical data such as grade or
/// design factor will be added here when the Mechanics module needs them.
///
/// For hydraulic calculations convert it to a <see cref="PipeSegment"/>,
/// which carries the internal diameter and the elevation change.
/// </summary>
public class Pipe
{
    public Length OuterDiameter { get; set; }

    public Length WallThickness { get; set; }

    public Length Length { get; set; }

    /// <summary>Absolute internal roughness ε.</summary>
    public Length Roughness { get; set; }

    /// <summary>D_int = D_ext − 2·t</summary>
    public Length CalculateInternalDiameter()
    {
        double od = OuterDiameter.Meters;
        double t = WallThickness.Meters;

        if (!double.IsFinite(od) || od <= 0.0)
        {
            throw new InvalidOperationException(
                $"Outer diameter must be finite and greater than zero. Value: {od} m");
        }

        if (!double.IsFinite(t) || t <= 0.0 || 2.0 * t >= od)
        {
            throw new InvalidOperationException(
                $"Wall thickness must be greater than zero and less than half the outer diameter. " +
                $"Value: {t} m, outer diameter: {od} m");
        }

        return UnitsNet.Length.FromMeters(od - 2.0 * t);
    }

    /// <summary>
    /// Hydraulic segment for this pipe with the given elevation change
    /// (z_outlet − z_inlet, positive uphill).
    /// </summary>
    public PipeSegment ToSegment(Length elevationChange) =>
        new(CalculateInternalDiameter(), Length, Roughness, elevationChange);
}
