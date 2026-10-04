using NaturalGasPipeline.Core.Thermodynamics;
using UnitsNet;
using Xunit.Abstractions;

namespace NaturalGasPipeline.Tests;

public class PengRobinsonParametersTests
{
    private readonly ITestOutputHelper _output;

    public PengRobinsonParametersTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void MethaneParameters_ShouldMatchHandCalculation()
    {
        // Peng & Robinson (1976), methane at 288.15 K, R = 8.31446261815324 J/(mol·K):
        // κ = 0.37464 + 1.54226ω − 0.26992ω²
        // α = [1 + κ(1 − √(T/Tc))]²
        // a = 0.45724 R² Tc² / Pc · α   (Pa·m⁶/mol²)
        // b = 0.07780 R Tc / Pc          (m³/mol)
        // Reference values computed independently in Python.
        const double expectedKappa = 0.392217407205312;
        const double expectedAlpha = 0.8279435393012154;
        const double expectedA = 0.2066395210083509;
        const double expectedB = 2.6802920402019762e-05;

        var parameters =
            new PengRobinsonParameters(
                StandardGasComponents.Methane,
                Temperature.FromKelvins(288.15));

        _output.WriteLine($"κ = {parameters.Kappa:R}");
        _output.WriteLine($"α = {parameters.Alpha:R}");
        _output.WriteLine($"a = {parameters.A:R} Pa·m⁶/mol²");
        _output.WriteLine($"b = {parameters.B:R} m³/mol");

        AssertRelative(expectedKappa, parameters.Kappa);
        AssertRelative(expectedAlpha, parameters.Alpha);
        AssertRelative(expectedA, parameters.A);
        AssertRelative(expectedB, parameters.B);
    }

    private static void AssertRelative(double expected, double actual)
    {
        double error = System.Math.Abs(actual - expected) / System.Math.Abs(expected);

        Assert.True(error < 1e-12, $"Expected {expected:R}, got {actual:R} (rel. error {error:E2}).");
    }
}
