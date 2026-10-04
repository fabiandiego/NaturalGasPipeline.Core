using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using UnitsNet;

namespace NaturalGasPipeline.Hydraulics;

public static class FrictionFactorCalculator
{
    /// <summary>
    /// Calculates the Darcy friction factor.
    /// Laminar: f = 64 / Re.
    /// Turbulent: Colebrook-White equation.
    /// Transitional flow is reported as uncertain.
    /// </summary>
    public static double CalculateDarcyFrictionFactor(
        double reynoldsNumber,
        Length internalDiameter,
        Length absoluteRoughness)
    {
        double diameter =
            internalDiameter.Meters;

        double roughness =
            absoluteRoughness.Meters;

        if (!double.IsFinite(reynoldsNumber) ||
            reynoldsNumber <= 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(reynoldsNumber),
                "Reynolds number must be finite and greater than zero.");
        }

        if (!double.IsFinite(diameter) ||
            diameter <= 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(internalDiameter),
                "Internal diameter must be finite and greater than zero.");
        }

        if (!double.IsFinite(roughness) ||
            roughness < 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(absoluteRoughness),
                "Absolute roughness must be finite and non-negative.");
        }

        if (roughness >= diameter)
        {
            throw new ArgumentException(
                "Absolute roughness must be smaller than the internal diameter.",
                nameof(absoluteRoughness));
        }

        // Laminar flow
        if (reynoldsNumber < 2300.0)
        {
            return 64.0 / reynoldsNumber;
        }

        // Transitional flow
        if (reynoldsNumber < 4000.0)
        {
            throw new InvalidOperationException(
                "Flow is in the transitional regime (2300 <= Re < 4000). " +
                "The friction factor is uncertain in this range.");
        }

        // Turbulent flow: initial estimate using Swamee-Jain
        double relativeRoughness =
            roughness / diameter;

        double logarithmArgument =
            relativeRoughness / 3.7
            +
            5.74 / System.Math.Pow(reynoldsNumber, 0.9);

        double logValue =
            System.Math.Log10(logarithmArgument);

        double frictionFactor =
            0.25 / (logValue * logValue);

        // Refine the estimate using Colebrook-White
        const int maxIterations = 100;
        const double tolerance = 1e-12;

        for (int i = 0; i < maxIterations; i++)
        {
            double inverseSqrtF =
                -2.0 * System.Math.Log10(
                    relativeRoughness / 3.7
                    +
                    2.51 /
                    (reynoldsNumber *
                     System.Math.Sqrt(frictionFactor)));

            double updatedFrictionFactor =
                1.0 / (inverseSqrtF * inverseSqrtF);

            if (!double.IsFinite(updatedFrictionFactor) ||
                updatedFrictionFactor <= 0.0)
            {
                throw new InvalidOperationException(
                    "Colebrook-White iteration produced an invalid result.");
            }

            if (System.Math.Abs(
                    updatedFrictionFactor - frictionFactor)
                < tolerance)
            {
                return updatedFrictionFactor;
            }

            frictionFactor = updatedFrictionFactor;
        }

        throw new InvalidOperationException(
            "Colebrook-White iteration did not converge.");
    }
}