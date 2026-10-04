
using UnitsNet;
using Xunit;
using Xunit.Abstractions;

namespace NaturalGasPipeline.Hydraulics.Tests;

public class ReynoldsNumberCalculatorTests
{
    private readonly ITestOutputHelper _output;

    public ReynoldsNumberCalculatorTests(
        ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Calculate_ShouldReturnExpectedReynoldsNumber()
    {
        // Arrange
        MassFlow massFlow =
            MassFlow.FromKilogramsPerSecond(10.0);

        Length diameter =
            Length.FromMeters(0.5);

        DynamicViscosity viscosity =
            DynamicViscosity.FromPascalSeconds(1.1e-5);

        // Independent reference value calculated from the equation.
        const double expected = 2_314_980.9904275686;

        // Act
        double actual =
            ReynoldsNumberCalculator.Calculate(
                massFlow,
                diameter,
                viscosity);

        // Output
        _output.WriteLine("TEST: Reynolds number calculation");

        _output.WriteLine(
            $"Mass flow: {massFlow.KilogramsPerSecond:R} kg/s");

        _output.WriteLine(
            $"Diameter: {diameter.Meters:R} m");

        _output.WriteLine(
            $"Dynamic viscosity: {viscosity.PascalSeconds:R} Pa·s");

        _output.WriteLine(
            $"Expected Reynolds number: {expected:R}");

        _output.WriteLine(
            $"Actual Reynolds number: {actual:R}");

        // Assert
        Assert.Equal(expected, actual, 6);
    }

    [Fact]
    public void Calculate_ShouldRejectZeroMassFlow()
    {
        // Arrange
        MassFlow massFlow =
            MassFlow.FromKilogramsPerSecond(0.0);

        Length diameter =
            Length.FromMeters(0.5);

        DynamicViscosity viscosity =
            DynamicViscosity.FromPascalSeconds(1.1e-5);

        // Act and assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ReynoldsNumberCalculator.Calculate(
                massFlow,
                diameter,
                viscosity));
    }

    [Fact]
    public void Calculate_ShouldRejectZeroDiameter()
    {
        // Arrange
        MassFlow massFlow =
            MassFlow.FromKilogramsPerSecond(10.0);

        Length diameter = Length.Zero;

        DynamicViscosity viscosity =
            DynamicViscosity.FromPascalSeconds(1.1e-5);

        // Act and assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ReynoldsNumberCalculator.Calculate(
                massFlow,
                diameter,
                viscosity));
    }

    [Fact]
    public void Calculate_ShouldRejectZeroViscosity()
    {
        // Arrange
        MassFlow massFlow =
            MassFlow.FromKilogramsPerSecond(10.0);

        Length diameter =
            Length.FromMeters(0.5);

        DynamicViscosity viscosity =
            DynamicViscosity.FromPascalSeconds(0.0);

        // Act and assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ReynoldsNumberCalculator.Calculate(
                massFlow,
                diameter,
                viscosity));
    }
}