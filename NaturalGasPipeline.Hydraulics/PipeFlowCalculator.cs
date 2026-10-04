using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnitsNet;

namespace NaturalGasPipeline.Hydraulics;

public static class PipeFlowCalculator
{
    public static Area CalculateCrossSectionalArea(
        Length internalDiameter)
    {
        double diameter =
            internalDiameter.Meters;

        double area =
            System.Math.PI
            * diameter
            * diameter
            / 4.0;

        return Area.FromSquareMeters(area);
    }

    public static Speed CalculateVelocity(
        Length internalDiameter,
        double volumetricFlowRate)
    {
        Area area =
            CalculateCrossSectionalArea(
                internalDiameter);

        double velocity =
            volumetricFlowRate
            / area.SquareMeters;

        return Speed.FromMetersPerSecond(
            velocity);
    }
}