using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using UnitsNet;

namespace NaturalGasPipeline.Hydraulics;

public static class PressureDropCalculator
{
    /// <summary>
    /// Calculates frictional pressure drop using Darcy-Weisbach.
    /// Assumes constant local density and velocity over the segment.
    /// Returns the positive magnitude of the pressure drop.
    /// </summary>
    public static Pressure CalculateDarcyWeisbachPressureDrop(
        Length pipeLength,
        Length internalDiameter,
        Density density,
        Speed velocity,
        double darcyFrictionFactor)
    {
        double length =
            pipeLength.Meters;

        double diameter =
            internalDiameter.Meters;

        double gasDensity =
            density.KilogramsPerCubicMeter;

        double flowVelocity =
            velocity.MetersPerSecond;

        if (!double.IsFinite(length) || length <= 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pipeLength),
                "Pipe length must be finite and greater than zero.");
        }

        if (!double.IsFinite(diameter) || diameter <= 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(internalDiameter),
                "Internal diameter must be finite and greater than zero.");
        }

        if (!double.IsFinite(gasDensity) || gasDensity <= 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(density),
                "Density must be finite and greater than zero.");
        }

        if (!double.IsFinite(flowVelocity))
        {
            throw new ArgumentOutOfRangeException(
                nameof(velocity),
                "Velocity must be finite.");
        }

        if (!double.IsFinite(darcyFrictionFactor) ||
            darcyFrictionFactor <= 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(darcyFrictionFactor),
                "Darcy friction factor must be finite and greater than zero.");
        }

        double pressureDrop =
            darcyFrictionFactor
            * (length / diameter)
            * (gasDensity * flowVelocity * flowVelocity / 2.0);

        return Pressure.FromPascals(pressureDrop);
    }
}