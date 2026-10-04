using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnitsNet;
using Xunit;
using Xunit.Abstractions;
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

        _output.WriteLine(
            $"Diameter: {diameter.Meters} m");

        _output.WriteLine(
            $"Area: {area.SquareMeters} m²");

        Assert.Equal(
            0.1963495406,
            area.SquareMeters,
            6);
    }

    [Fact]
    public void CalculateVelocity_ShouldReturnCorrectVelocity()
    {
        Length diameter =
            Length.FromMeters(0.5);

        double flowRate = 10.0;

        Speed velocity =
            PipeFlowCalculator.CalculateVelocity(
                diameter,
                flowRate);

        _output.WriteLine(
            $"Diameter: {diameter.Meters} m");

        _output.WriteLine(
            $"Flow rate: {flowRate} m³/s");

        _output.WriteLine(
            $"Velocity: {velocity.MetersPerSecond} m/s");

        Assert.Equal(
            50.92958179,
            velocity.MetersPerSecond,
            6);
    }
}