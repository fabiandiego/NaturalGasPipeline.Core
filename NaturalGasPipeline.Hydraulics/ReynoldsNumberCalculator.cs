
using UnitsNet;

namespace NaturalGasPipeline.Hydraulics;

public static class ReynoldsNumberCalculator
{
    /// <summary>
    /// Calculates the Reynolds number for steady flow in a circular pipe
    /// using mass flow rate.
    ///
    /// Re = 4 * |m_dot| / (pi * D * mu)
    /// </summary>
    public static double Calculate(
        MassFlow massFlow,
        Length internalDiameter,
        DynamicViscosity dynamicViscosity)
    {
        double massFlowValue =
            System.Math.Abs(massFlow.KilogramsPerSecond);

        double diameter =
            internalDiameter.Meters;

        double viscosity =
            dynamicViscosity.PascalSeconds;

        // Validate inputs
        if (!double.IsFinite(massFlowValue) ||
            massFlowValue <= 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(massFlow),
                "Mass flow magnitude must be finite and greater than zero.");
        }

        if (!double.IsFinite(diameter) ||
            diameter <= 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(internalDiameter),
                "Internal diameter must be finite and greater than zero.");
        }

        if (!double.IsFinite(viscosity) ||
            viscosity <= 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(dynamicViscosity),
                "Dynamic viscosity must be finite and greater than zero.");
        }

        // Diagnostic output: remove after investigating the discrepancy.
        System.Console.WriteLine(
            $"Mass flow: {massFlowValue:R} kg/s");

        System.Console.WriteLine(
            $"Diameter: {diameter:R} m");

        System.Console.WriteLine(
            $"Viscosity: {viscosity:R} Pa·s");

        double denominator =
            System.Math.PI * diameter * viscosity;

        System.Console.WriteLine(
            $"Denominator: {denominator:R}");

        double reynolds =
            4.0 * massFlowValue / denominator;

        System.Console.WriteLine(
            $"Reynolds number: {reynolds:R}");

        return reynolds;
    }
}