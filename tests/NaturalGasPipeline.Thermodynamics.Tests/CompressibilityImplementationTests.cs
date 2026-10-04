using NaturalGasPipeline.Thermodynamics;
using UnitsNet;
using Xunit;
using Xunit.Abstractions;
using static NaturalGasPipeline.Thermodynamics.Tests.CompressibilityTestHelpers;

namespace NaturalGasPipeline.Thermodynamics.Tests;

/// <summary>
/// LEVEL 1 — implementation correctness.
///
/// Checks that the C# code solves the Peng-Robinson and SRK equations
/// exactly as published, not whether those equations describe the real gas
/// (that is <see cref="CompressibilityAccuracyTests"/>).
///
/// Reference values come from an independent Python implementation that
/// uses the same component data and mixing rules (kij = 0) but a different
/// root finder (numpy.roots, companion-matrix eigenvalues) instead of the
/// Cardano solution used here. Agreement to 1e-9 means both the cubic
/// coefficients and the root selection are correct.
/// </summary>
public class CompressibilityImplementationTests
{
    private const double RelativeTolerance = 1e-9;

    private readonly ITestOutputHelper _output;

    public CompressibilityImplementationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [InlineData("Methane", "PR", 288.15, 7.0, 0.8492349032782932)]
    [InlineData("Methane", "SRK", 288.15, 7.0, 0.8791668534230765)]
    [InlineData("Methane", "PR", 273.15, 10.0, 0.7629959792486911)]
    [InlineData("Methane", "SRK", 273.15, 10.0, 0.8018591434651863)]
    [InlineData("Methane", "PR", 313.15, 1.0, 0.9815330495334287)]
    [InlineData("Methane", "SRK", 313.15, 1.0, 0.986268605800799)]
    [InlineData("TypicalNaturalGas", "PR", 288.15, 7.0, 0.8162522850766638)]
    [InlineData("TypicalNaturalGas", "SRK", 288.15, 7.0, 0.8473996626963167)]
    [InlineData("TypicalNaturalGas", "PR", 273.15, 10.0, 0.7092360068012978)]
    [InlineData("TypicalNaturalGas", "SRK", 273.15, 10.0, 0.7483955787985266)]
    [InlineData("TypicalNaturalGas", "PR", 313.15, 1.0, 0.9779078024080601)]
    [InlineData("TypicalNaturalGas", "SRK", 313.15, 1.0, 0.9829288215683412)]
    public void Z_ShouldMatchIndependentImplementation(
        string fluid,
        string eos,
        double temperatureKelvins,
        double pressureMegapascals,
        double expectedZ)
    {
        var model = ModelFor(eos, MixtureOf(fluid));

        double z = model.Calculate(
            Pressure.FromMegapascals(pressureMegapascals),
            Temperature.FromKelvins(temperatureKelvins));

        double error = RelativeError(z, expectedZ);

        _output.WriteLine($"{fluid}, {eos}, T = {temperatureKelvins} K, P = {pressureMegapascals} MPa");
        _output.WriteLine($"Calculated Z: {z:R}");
        _output.WriteLine($"Reference Z:  {expectedZ:R}");
        _output.WriteLine($"Relative error: {error:E2}");

        Assert.True(
            System.Math.Abs(error) < RelativeTolerance,
            $"Relative error {error:E2} exceeds {RelativeTolerance:E0}.");
    }

    [Fact]
    public void PureComponentAndSingleComponentMixture_ShouldGiveSameZ()
    {
        // The two constructors of PengRobinsonModel take different code paths.
        var pressure = Pressure.FromMegapascals(7.0);
        var temperature = Temperature.FromKelvins(288.15);

        double zComponent =
            new PengRobinsonModel(
                StandardGasComponents.Methane)
            .Calculate(pressure, temperature);

        double zMixture =
            ModelFor("PR", MixtureOf("Methane"))
            .Calculate(pressure, temperature);

        _output.WriteLine($"Z (component path): {zComponent:R}");
        _output.WriteLine($"Z (mixture path):   {zMixture:R}");

        Assert.Equal(zComponent, zMixture, 12);
    }
}
