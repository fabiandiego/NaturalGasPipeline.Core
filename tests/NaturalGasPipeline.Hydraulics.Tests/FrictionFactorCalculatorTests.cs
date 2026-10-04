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

        // Assert: 64 / 1000
        Assert.Equal(0.064, factor, 6);
    }

    /// <summary>
    /// Turbulent Darcy friction factor against the exact Colebrook-White
    /// solution. Reference values were computed independently of this code
    /// with two different methods that agree to 13 significant digits:
    /// the closed-form Lambert W solution (scipy.special.lambertw) and a
    /// bracketed root search on 1/√f (scipy.optimize.brentq).
    /// </summary>
    [Theory]
    [InlineData(1.0e5, 0.5, 0.050, 0.0185138660775)] // ε/D = 1.0e-4
    [InlineData(1.0e7, 0.5, 0.045, 0.0119453804169)] // ε/D = 9.0e-5, typical transmission line
    [InlineData(4.0e3, 0.5, 0.050, 0.0400084312336)] // lower turbulent limit
    [InlineData(1.0e8, 1.0, 0.000, 0.0059404663516)] // smooth pipe
    public void CalculateDarcyFrictionFactor_TurbulentFlow_ShouldMatchExactColebrook(
        double reynolds,
        double diameterMeters,
        double roughnessMillimeters,
        double expected)
    {
        // Arrange
        Length diameter = Length.FromMeters(diameterMeters);
        Length roughness = Length.FromMillimeters(roughnessMillimeters);

        // Act
        double factor =
            FrictionFactorCalculator.CalculateDarcyFrictionFactor(
                reynolds, diameter, roughness);

        double relativeError =
            System.Math.Abs(factor - expected) / expected;

        // Output
        _output.WriteLine($"Reynolds number: {reynolds:E3}");
        _output.WriteLine($"Relative roughness: {roughness.Meters / diameter.Meters:E3}");
        _output.WriteLine($"Calculated f: {factor:F13}");
        _output.WriteLine($"Reference f:  {expected:F13}");
        _output.WriteLine($"Relative error: {relativeError:E2}");

        // Assert: reference values are given to 13 decimals
        Assert.True(
            relativeError < 1e-9,
            $"Relative error {relativeError:E2} exceeds 1e-9.");
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
