using UnitsNet;
using NaturalGasPipeline.Core.Flow;

namespace NaturalGasPipeline.Hydraulics;

public static class PipeFlowCalculator
{
    /// <summary>A = π·D² / 4</summary>
    public static Area CalculateCrossSectionalArea(
        Length internalDiameter)
    {
        double diameter =
            internalDiameter.Meters;

        if (!double.IsFinite(diameter) || diameter <= 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(internalDiameter),
                "Internal diameter must be finite and greater than zero.");
        }

        double area =
            System.Math.PI
            * diameter
            * diameter
            / 4.0;

        return Area.FromSquareMeters(area);
    }

    /// <summary>
    /// Mean velocity V = Q_actual / A.
    /// Only an actual (flowing-condition) volumetric flow is accepted;
    /// a standard volumetric flow is rejected at compile time.
    /// A negative flow (reverse direction) gives a negative velocity.
    /// </summary>
    public static Speed CalculateVelocity(
        Length internalDiameter,
        ActualVolumetricFlow actualVolumetricFlow)
    {
        double flow =
            actualVolumetricFlow.Value.CubicMetersPerSecond;

        if (!double.IsFinite(flow))
        {
            throw new ArgumentOutOfRangeException(
                nameof(actualVolumetricFlow),
                "Volumetric flow must be finite.");
        }

        Area area =
            CalculateCrossSectionalArea(
                internalDiameter);

        double velocity =
            flow
            / area.SquareMeters;

        return Speed.FromMetersPerSecond(
            velocity);
    }
}
