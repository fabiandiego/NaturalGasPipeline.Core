using UnitsNet;
using Xunit;
using Xunit.Abstractions;

namespace NaturalGasPipeline.Thermodynamics.Tests;

/// <summary>
/// LEVEL 1 — corresponding-states Z correlations reproduce their published
/// equations.
///
/// References: an independent Python implementation solved with
/// scipy.optimize.brentq (xtol 1e-15). DAK and Hall-Yarborough are implicit,
/// so this also checks the root bracketing and Brent's method.
///
/// Cross-model consistency (measured 2026-10-04, same chart, different fits):
///   Hall-Yarborough vs DAK, 1.15 ≤ Tpr ≤ 3, 0.2 ≤ Ppr ≤ 20.5: ≤ 2.9 %
///   Papay vs DAK,           1.4 ≤ Tpr ≤ 3, Ppr ≤ 4:          ≤ 3.2 %
/// </summary>
public class CorrespondingStatesImplementationTests
{
    private readonly ITestOutputHelper _output;

    public CorrespondingStatesImplementationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    // Any pseudo-critical point works for reduced-property tests.
    private static readonly PseudoCriticalProperties Reference =
        new(Temperature.FromKelvins(200.0), Pressure.FromMegapascals(4.6));

    [Theory]
    [InlineData(1.5, 1.5, 0.8593143805613469, 0.8581232116677932, 0.858231191009301)]
    [InlineData(1.2, 3.0, 0.5302397946800431, 0.5305433323180749, 0.5552961215442255)]
    [InlineData(2.0, 5.0, 0.9594514350527716, 0.9581701151512212, 0.9676895973693047)]
    [InlineData(1.05, 0.8, 0.6997239977715397, 0.7065439123159544, 0.7610735223407048)]
    [InlineData(1.0, 0.2, 0.9254387640721163, 0.9281331139127723, 0.9279690539355394)]
    [InlineData(3.0, 30.0, 1.8259130058283146, 1.7818895786078923, 1.760329503378303)]
    [InlineData(1.1, 20.0, 2.1815181598911138, 2.2039033419333927, 9.004865136123119)]
    public void Correlations_ShouldMatchIndependentImplementation(
        double tr,
        double pr,
        double expectedDak,
        double expectedHallYarborough,
        double expectedPapay)
    {
        double dak = new DranchukAbouKassemModel(Reference).CalculateFromReducedProperties(tr, pr);
        double hy = new HallYarboroughModel(Reference).CalculateFromReducedProperties(tr, pr);
        double papay = new PapayModel(Reference).CalculateFromReducedProperties(tr, pr);

        _output.WriteLine($"Tpr = {tr}, Ppr = {pr}");
        _output.WriteLine($"DAK   {dak:R} (ref {expectedDak:R})");
        _output.WriteLine($"HY    {hy:R} (ref {expectedHallYarborough:R})");
        _output.WriteLine($"Papay {papay:R} (ref {expectedPapay:R})");

        Assert.Equal(expectedDak, dak, expectedDak * 1e-12);
        Assert.Equal(expectedHallYarborough, hy, expectedHallYarborough * 1e-12);
        Assert.Equal(expectedPapay, papay, expectedPapay * 1e-13);
    }

    [Fact]
    public void Calculate_ShouldUseReducedProperties()
    {
        // Typical natural gas, Kay pseudo-criticals, 288.15 K and 7 MPa
        // → Tpr 1.4362, Ppr 1.5203.
        ICompressibilityModel model = CompressibilityModelFactory.Create(
            CompressibilityModelType.DranchukAbouKassem,
            NaturalGasMixtures.TypicalNaturalGas);

        double z = model.Calculate(Pressure.FromMegapascals(7.0), Temperature.FromKelvins(288.15));

        _output.WriteLine($"Typical natural gas, DAK (Kay): Z = {z:R}");

        Assert.Equal(0.8319026056401168, z, 1e-12);
    }

    [Fact]
    public void SpecificGravityPath_ShouldMatchHandCalculation()
    {
        // γ = 0.65 → Sutton → DAK at 288.15 K, 7 MPa (Tpr 1.42059, Ppr 1.51503).
        CorrespondingStatesCompressibilityModel model = CompressibilityModelFactory.CreateFromSpecificGravity(
            CorrespondingStatesModelType.DranchukAbouKassem,
            0.65,
            new SuttonPseudoCriticalCorrelation());

        double z = model.Calculate(Pressure.FromMegapascals(7.0), Temperature.FromKelvins(288.15));

        _output.WriteLine($"γ = 0.65, Sutton + DAK: Z = {z:R}");

        Assert.Equal(0.8252407862204437, z, 1e-12);
        Assert.True(model.IsWithinValidatedRange(Pressure.FromMegapascals(7.0), Temperature.FromKelvins(288.15)));
    }

    [Fact]
    public void HallYarborough_ShouldAgreeWithDakInsideItsValidatedRange()
    {
        double worst = MaxDeviationFromDak(new HallYarboroughModel(Reference), 1.15, 3.0, 0.2, 20.5);

        _output.WriteLine($"max |Z_HY − Z_DAK| / Z_DAK = {worst * 100:F3} % (allowed 3 %)");

        Assert.True(worst < 0.03);
    }

    [Fact]
    public void Papay_ShouldAgreeWithDakInsideItsValidatedRange()
    {
        double worst = MaxDeviationFromDak(new PapayModel(Reference), 1.4, 3.0, 0.2, 4.0);

        _output.WriteLine($"max |Z_Papay − Z_DAK| / Z_DAK = {worst * 100:F3} % (allowed 3.2 %)");

        Assert.True(worst < 0.032);
    }

    [Fact]
    public void ValidatedRanges_ShouldMatchTheDocumentation()
    {
        var dak = new DranchukAbouKassemModel(Reference);
        var hy = new HallYarboroughModel(Reference);
        var papay = new PapayModel(Reference);

        Assert.True(dak.IsWithinValidatedReducedRange(1.0, 30.0));
        Assert.False(dak.IsWithinValidatedReducedRange(0.95, 1.0));
        Assert.False(hy.IsWithinValidatedReducedRange(1.1, 1.0));
        Assert.True(hy.IsWithinValidatedReducedRange(1.5, 20.0));
        Assert.True(papay.IsWithinValidatedReducedRange(1.45, 1.5));
        Assert.False(papay.IsWithinValidatedReducedRange(1.25, 1.5));
        Assert.False(papay.IsWithinValidatedReducedRange(2.0, 6.0));
    }

    [Fact]
    public void Calculate_ShouldRejectNonPhysicalStates()
    {
        var dak = new DranchukAbouKassemModel(Reference);

        Assert.Throws<ArgumentOutOfRangeException>(() => dak.Calculate(Pressure.FromPascals(0.0), Temperature.FromKelvins(300.0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => dak.Calculate(Pressure.FromMegapascals(5.0), Temperature.FromKelvins(0.0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PseudoCriticalProperties(Temperature.FromKelvins(-1.0), Pressure.FromMegapascals(4.6)));
    }

    // ------------------------------------------------------------ CNGA

    [Theory]
    [InlineData(7.0e6, 288.15, 0.6133, 101325.0, 0.849729870044166)]
    [InlineData(6996082.293168361, 288.8888888888889, 0.6, 101325.0, 0.8578458317084443)] // 1000 psig, 520 °R
    public void Cnga_ShouldMatchPublishedFormula(
        double pressurePascals,
        double temperatureKelvins,
        double specificGravity,
        double atmosphericPascals,
        double expected)
    {
        var model = new CngaCompressibilityModel(specificGravity, Pressure.FromPascals(atmosphericPascals));

        double z = model.Calculate(Pressure.FromPascals(pressurePascals), Temperature.FromKelvins(temperatureKelvins));

        _output.WriteLine($"CNGA: P = {pressurePascals / 1e6:F4} MPa abs, T = {temperatureKelvins:F2} K, γ = {specificGravity}: Z = {z:R}");

        Assert.Equal(expected, z, expected * 1e-12);
    }

    [Fact]
    public void Cnga_ShouldUseTheStatedAtmosphericPressure()
    {
        // Same absolute pressure, different atmospheres → different gauge pressure → different Z.
        var seaLevel = new CngaCompressibilityModel(0.65, Pressure.FromPascals(101325.0));
        var highland = new CngaCompressibilityModel(0.65, Pressure.FromKilopascals(75.0));

        Pressure p = Pressure.FromMegapascals(5.0);
        Temperature t = Temperature.FromKelvins(290.0);

        double zSea = seaLevel.Calculate(p, t);
        double zHigh = highland.Calculate(p, t);

        _output.WriteLine($"Z (101.325 kPa atm) = {zSea:F6}, Z (75 kPa atm) = {zHigh:F6}");

        Assert.True(zHigh < zSea);
        Assert.Equal(1.0, seaLevel.Calculate(Pressure.FromPascals(101325.0), t), 1e-15);
        Assert.False(seaLevel.IsWithinValidatedRange(Pressure.FromKilopascals(500.0), t)); // ≈ 58 psig
        Assert.True(seaLevel.IsWithinValidatedRange(p, t));
    }

    private static double MaxDeviationFromDak(
        CorrespondingStatesCompressibilityModel model,
        double trMin,
        double trMax,
        double prMin,
        double prMax)
    {
        var dak = new DranchukAbouKassemModel(Reference);
        double worst = 0.0;

        for (int i = 0; i < 20; i++)
        {
            double tr = trMin + i * (trMax - trMin) / 19.0;

            for (int j = 0; j < 20; j++)
            {
                double pr = prMin + j * (prMax - prMin) / 19.0;
                double reference = dak.CalculateFromReducedProperties(tr, pr);
                double deviation = System.Math.Abs(model.CalculateFromReducedProperties(tr, pr) - reference) / reference;

                worst = System.Math.Max(worst, deviation);
            }
        }

        return worst;
    }
}
