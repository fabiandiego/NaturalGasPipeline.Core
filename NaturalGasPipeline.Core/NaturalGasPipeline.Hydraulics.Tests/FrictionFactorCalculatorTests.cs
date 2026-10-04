using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


using UnitsNet;
using Xunit;
using Xunit.Abstractions;

namespace NaturalGasPipeline.Hydraulics.Tests;

public class FrictionFactorCalculatorTests
{
    private readonly ITestOutputHelper _output;

    public FrictionFactorCalculatorTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void CalculateDarcyFrictionFactor_LaminarFlow_ShouldReturn64OverRe()
    {
        // Arrange
        double reynolds = 1000.0;
        Length diameter = Length.FromMeters(0.5);
        Length roughness = Length.FromMillimeters(0.05);

        // Act
        double factor =
            FrictionFactorCalculator.CalculateDarcyFrictionFactor(
                reynolds, diameter, roughness);

        // Output
        _output.WriteLine($"Reynolds number: {reynolds:F1}");
        _output.WriteLine($"Darcy friction factor: {factor:F6}");

        // Assert
        Assert.Equal(0.064, factor, 6);
    }

    [Fact]
    public void CalculateDarcyFrictionFactor_TurbulentFlow_ShouldReturnValidFactor()
    {
        // Arrange
        double reynolds = 100_000.0;
        Length diameter = Length.FromMeters(0.5);
        Length roughness = Length.FromMillimeters(0.05);

        // Act
        double factor =
            FrictionFactorCalculator.CalculateDarcyFrictionFactor(
                reynolds, diameter, roughness);

        // Output
        _output.WriteLine($"Reynolds number: {reynolds:F0}");
        _output.WriteLine($"Relative roughness: {roughness.Meters / diameter.Meters:F6}");
        _output.WriteLine($"Darcy friction factor: {factor:F6}");

        // Assert
        Assert.True(factor > 0.0 && factor < 0.1);
    }

    [Fact]
    public void CalculateDarcyFrictionFactor_TransitionalFlow_ShouldThrow()
    {
        Assert.Throws<InvalidOperationException>(() =>
            FrictionFactorCalculator.CalculateDarcyFrictionFactor(
                3000.0,
                Length.FromMeters(0.5),
                Length.FromMillimeters(0.05)));
    }
}