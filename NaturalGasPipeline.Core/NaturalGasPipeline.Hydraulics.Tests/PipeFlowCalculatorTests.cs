using UnitsNet;
using Xunit;
using Xunit.Abstractions;
using NaturalGasPipeline.Core.Flow;
using NaturalGasPipeline.Hydraulics;

namespace NaturalGasPipeline.Hydraulics.Tests;

public class PipeFlowCalculatorTests
{
    private readonly ITestOutputHelper _output;

    public PipeFlowCalculatorTests(
        ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void CalculateCrossSectionalArea_ShouldReturnCorrectArea()
    {
        Length diameter =
            Length.FromMeters(0.5);

        Area area =
            PipeFlowCalculator.CalculateCrossSectionalArea(
                diameter);

        _output.WriteLine($"Diameter: {diameter.Meters} m");
        _output.WriteLine($"Area: {area.SquareMeters} m²");

        // π · 0.5² / 4
        Assert.Equal(0.1963495406, area.SquareMeters, 6);
    }

    [Fact]
    public void CalculateVelocity_ShouldReturnCorrectVelocity()
    {
        Length diameter =
            Length.FromMeters(0.5);

        ActualVolumetricFlow flowRate =
            ActualVolumetricFlow.FromCubicMetersPerSecond(10.0);

        Speed velocity =
            PipeFlowCalculator.CalculateVelocity(
                diameter,
                flowRate);

        _output.WriteLine($"Diameter: {diameter.Meters} m");
        _output.WriteLine($"Flow rate: {flowRate}");
        _output.WriteLine($"Velocity: {velocity.MetersPerSecond} m/s");

        // 10 / 0.1963495408
        Assert.Equal(50.92958179, velocity.MetersPerSecond, 6);
    }

    [Fact]
    public void CalculateVelocity_ReverseFlow_ShouldGiveNegativeVelocity()
    {
        Speed velocity =
            PipeFlowCalculator.CalculateVelocity(
                Length.FromMeters(0.5),
                ActualVolumetricFlow.FromCubicMetersPerSecond(-10.0));

        _output.WriteLine($"Velocity: {velocity.MetersPerSecond} m/s");

        Assert.Equal(-50.92958179, velocity.MetersPerSecond, 6);
    }

    [Fact]
    public void CalculateCrossSectionalArea_ShouldRejectZeroDiameter()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PipeFlowCalculator.CalculateCrossSectionalArea(
                Length.Zero));
    }
}
