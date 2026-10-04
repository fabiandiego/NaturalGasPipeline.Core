using UnitsNet;
using Xunit;
using Xunit.Abstractions;
using NaturalGasPipeline.Core.Flow;

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

        // Assert: hand calculation 0.02 · (1000 / 0.5) · (20 · 10² / 2) = 40 000 Pa
        Assert.Equal(40_000.0, pressureDrop.Pascals, 6);
    }

    /// <summary>
    /// Published reference: Arnold &amp; Stewart, Surface Production Operations,
    /// Ch. 6, Example 6.1b (liquid line, SI units, general equation).
    /// Q = 5.30 + 1.52 = 6.82 m³/h, SG = 0.91 (ρ = 910 kg/m³), L = 2130 m,
    /// Moody (Darcy) f read from the Moody chart by the book.
    /// Book results: 484 kPa (50.8 mm) and 17.7 kPa (101.6 mm), rounded
    /// to three significant figures, hence the 1 % tolerance.
    ///
    /// This chains area → velocity → Darcy-Weisbach, so it also checks
    /// PipeFlowCalculator against an independent source.
    /// </summary>
    [Theory]
    [InlineData(50.8, 0.029, 484.0)]
    [InlineData(101.6, 0.034, 17.7)]
    public void DarcyWeisbach_ShouldReproduceBookExample6_1b(
        double diameterMillimeters,
        double moodyFrictionFactor,
        double bookPressureDropKilopascals)
    {
        // Arrange
        Length diameter = Length.FromMillimeters(diameterMillimeters);
        Length pipeLength = Length.FromMeters(2130.0);
        Density density = Density.FromKilogramsPerCubicMeter(910.0);

        ActualVolumetricFlow flow =
            new(VolumeFlow.FromCubicMetersPerHour(6.82));

        // Act
        Speed velocity =
            PipeFlowCalculator.CalculateVelocity(diameter, flow);

        Pressure pressureDrop =
            PressureDropCalculator.CalculateDarcyWeisbachPressureDrop(
                pipeLength,
                diameter,
                density,
                velocity,
                moodyFrictionFactor);

        double relativeDeviation =
            (pressureDrop.Kilopascals - bookPressureDropKilopascals)
            / bookPressureDropKilopascals;

        // Output
        _output.WriteLine($"Diameter: {diameter.Millimeters} mm");
        _output.WriteLine($"Velocity: {velocity.MetersPerSecond:F6} m/s");
        _output.WriteLine($"Calculated ΔP: {pressureDrop.Kilopascals:F4} kPa");
        _output.WriteLine($"Book ΔP: {bookPressureDropKilopascals} kPa");
        _output.WriteLine($"Deviation: {relativeDeviation * 100:F3} %");

        // Assert
        Assert.InRange(relativeDeviation, -0.01, 0.01);
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
