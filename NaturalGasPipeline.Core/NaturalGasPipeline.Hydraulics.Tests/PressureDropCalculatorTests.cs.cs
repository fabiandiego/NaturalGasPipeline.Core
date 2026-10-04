using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using UnitsNet;
using Xunit;
using Xunit.Abstractions;

namespace NaturalGasPipeline.Hydraulics.Tests;

public class PressureDropCalculatorTests
{
    private readonly ITestOutputHelper _output;

    public PressureDropCalculatorTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void CalculateDarcyWeisbachPressureDrop_ShouldReturnExpectedPressureDrop()
    {
        // Arrange
        Length pipeLength = Length.FromMeters(1000.0);
        Length diameter = Length.FromMeters(0.5);
        Density density = Density.FromKilogramsPerCubicMeter(20.0);
        Speed velocity = Speed.FromMetersPerSecond(10.0);
        double frictionFactor = 0.02;

        // Act
        Pressure pressureDrop =
            PressureDropCalculator.CalculateDarcyWeisbachPressureDrop(
                pipeLength,
                diameter,
                density,
                velocity,
                frictionFactor);

        // Output
        _output.WriteLine($"Pipe length: {pipeLength.Meters:F1} m");
        _output.WriteLine($"Internal diameter: {diameter.Meters:F3} m");
        _output.WriteLine($"Gas density: {density.KilogramsPerCubicMeter:F2} kg/m³");
        _output.WriteLine($"Velocity: {velocity.MetersPerSecond:F2} m/s");
        _output.WriteLine($"Darcy friction factor: {frictionFactor:F4}");
        _output.WriteLine($"Pressure drop: {pressureDrop.Pascals:F2} Pa");
        _output.WriteLine($"Pressure drop: {pressureDrop.Bars:F6} bar");

        // Assert
        Assert.Equal(40_000.0, pressureDrop.Pascals, 6);
    }

    [Fact]
    public void CalculateDarcyWeisbachPressureDrop_ShouldRejectZeroDiameter()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PressureDropCalculator.CalculateDarcyWeisbachPressureDrop(
                Length.FromMeters(1000.0),
                Length.Zero,
                Density.FromKilogramsPerCubicMeter(20.0),
                Speed.FromMetersPerSecond(10.0),
                0.02));
    }
}