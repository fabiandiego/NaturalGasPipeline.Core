using UnitsNet;
using Xunit;
using Xunit.Abstractions;
using static NaturalGasPipeline.Thermodynamics.Tests.CompressibilityTestHelpers;

namespace NaturalGasPipeline.Thermodynamics.Tests;

/// <summary>
/// Gas characterization: specific gravity, pseudo-critical properties
/// (Kay, Sutton, Standing) and the Wichert-Aziz sour-gas correction.
/// References: independent Python evaluation of the published formulas
/// (field units, converted with 1 psi = 6894.757293168361 Pa, 1 °R = 5/9 K).
/// </summary>
public class PseudoCriticalCorrelationTests
{
    private readonly ITestOutputHelper _output;

    public PseudoCriticalCorrelationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    // ------------------------------------------------------------ γ ↔ M

    [Theory]
    [InlineData("ISO 6976:2016", 0.6133197435842552)]
    [InlineData("GPA 2145-09", 0.6133824255502806)]
    [InlineData("GTHPS", 0.6134353763812155)]
    [InlineData("Rounded 29", 0.6125892586206897)]
    public void SpecificGravity_ShouldDependOnTheStatedAirMolarMass(string reference, double expected)
    {
        MolarMass air = reference switch
        {
            "ISO 6976:2016" => DryAirMolarMass.Iso6976_2016,
            "GPA 2145-09" => DryAirMolarMass.Gpa2145_09,
            "GTHPS" => DryAirMolarMass.Gthps,
            _ => DryAirMolarMass.Rounded29
        };

        double g = SpecificGravity.OfMixture(NaturalGasMixtures.TypicalNaturalGas, air);

        _output.WriteLine($"Typical natural gas, M_air = {air.GramsPerMole} g/mol ({reference}): γ = {g:R}");

        Assert.Equal(expected, g, 1e-14);
    }

    [Fact]
    public void SpecificGravity_ShouldRoundTripThroughMolarMass()
    {
        MolarMass m = SpecificGravity.ToMolarMass(0.65, DryAirMolarMass.Iso6976_2016);

        Assert.Equal(0.65 * 0.02896546, m.KilogramsPerMole, 1e-15);
        Assert.Equal(0.65, SpecificGravity.FromMolarMass(m, DryAirMolarMass.Iso6976_2016), 1e-14);
        Assert.Throws<ArgumentOutOfRangeException>(() => SpecificGravity.ToMolarMass(0.0, DryAirMolarMass.Iso6976_2016));
    }

    // ------------------------------------------------------------ Kay

    [Fact]
    public void Kay_ShouldReturnTheTrueCriticalPointOfAPureFluid()
    {
        PseudoCriticalProperties pc = PseudoCriticalProperties.FromKay(MixtureOf("Methane"));

        Assert.Equal(190.56, pc.Temperature.Kelvins, 1e-10);
        Assert.Equal(4.599e6, pc.Pressure.Pascals, 1e-6);
    }

    [Fact]
    public void Kay_ShouldAverageTheComponentCriticalProperties()
    {
        PseudoCriticalProperties pc = PseudoCriticalProperties.FromKay(NaturalGasMixtures.TypicalNaturalGas);

        // 0.90·190.56 + 0.06·305.32 + 0.02·369.83 + 0.015·126.20 + 0.005·304.13
        double tpc = 0.90 * 190.56 + 0.06 * 305.32 + 0.02 * 369.83 + 0.015 * 126.20 + 0.005 * 304.13;
        double ppc = (0.90 * 4.599 + 0.06 * 4.872 + 0.02 * 4.248 + 0.015 * 3.3958 + 0.005 * 7.3773) * 1e6;

        _output.WriteLine($"Tpc = {pc.Temperature.Kelvins:F4} K, Ppc = {pc.Pressure.Megapascals:F6} MPa");

        Assert.Equal(tpc, pc.Temperature.Kelvins, 1e-10);
        Assert.Equal(ppc, pc.Pressure.Pascals, 1e-6);
        Assert.Equal(1.4362011917753494, pc.ReducedTemperature(Temperature.FromKelvins(288.15)), 1e-12);
        Assert.Equal(1.520349828151601, pc.ReducedPressure(Pressure.FromMegapascals(7.0)), 1e-12);
    }

    // ------------------------------------------------------------ γ → (Tpc, Ppc)

    [Theory]
    [InlineData("Sutton", 0.65, 202.8388888888889, 4620376.810113621)]
    [InlineData("Sutton", 0.80, 223.02222222222224, 4479496.2343423115)]
    [InlineData("Sutton", 1.20, 267.79999999999995, 4098354.051175964)]
    [InlineData("StandingNaturalGas", 0.65, 207.76041666666666, 4556788.187288052)]
    [InlineData("StandingNaturalGas", 0.80, 233.33333333333334, 4516066.027025276)]
    [InlineData("StandingGasCondensate", 0.80, 225.13333333333333, 4533551.131520751)]
    [InlineData("StandingGasCondensate", 1.20, 266.6888888888889, 4329742.105934694)]
    public void PseudoCriticalCorrelation_ShouldMatchPublishedFormula(
        string correlation,
        double specificGravity,
        double expectedKelvins,
        double expectedPascals)
    {
        IPseudoCriticalCorrelation model = Correlation(correlation);

        PseudoCriticalProperties pc = model.Calculate(specificGravity);

        _output.WriteLine($"{model.Name}, γ = {specificGravity}");
        _output.WriteLine($"Tpc = {pc.Temperature.Kelvins:R} K ({pc.Temperature.DegreesRankine:F2} °R)");
        _output.WriteLine($"Ppc = {pc.Pressure.Pascals:R} Pa ({pc.Pressure.PoundsForcePerSquareInch:F2} psia)");

        Assert.Equal(expectedKelvins, pc.Temperature.Kelvins, expectedKelvins * 1e-12);
        Assert.Equal(expectedPascals, pc.Pressure.Pascals, expectedPascals * 1e-12);
    }

    [Fact]
    public void PseudoCriticalCorrelations_ShouldFlagGravityOutsideTheirRange()
    {
        Assert.True(new SuttonPseudoCriticalCorrelation().IsWithinValidatedRange(0.65));
        Assert.False(new SuttonPseudoCriticalCorrelation().IsWithinValidatedRange(0.5539)); // pure methane
        Assert.True(Correlation("StandingNaturalGas").IsWithinValidatedRange(0.70));
        Assert.False(Correlation("StandingNaturalGas").IsWithinValidatedRange(0.80));
        Assert.True(Correlation("StandingGasCondensate").IsWithinValidatedRange(0.80));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SuttonPseudoCriticalCorrelation().Calculate(-0.6));
    }

    // ------------------------------------------------------------ Wichert-Aziz

    [Theory]
    [InlineData(0.05, 0.10, 20.735442672327267, 198.25253184870704, 4301125.911545312)]
    [InlineData(0.10, 0.00, 12.092841223718512, 203.05397709793414, 4427066.765815408)]
    public void WichertAziz_ShouldMatchPublishedFormula(
        double carbonDioxide,
        double hydrogenSulfide,
        double expectedEpsilonRankine,
        double expectedKelvins,
        double expectedPascals)
    {
        PseudoCriticalProperties sweet = new SuttonPseudoCriticalCorrelation().Calculate(0.70);
        PseudoCriticalProperties sour = WichertAzizCorrection.Apply(sweet, carbonDioxide, hydrogenSulfide);
        double epsilon = WichertAzizCorrection.AdjustmentRankine(carbonDioxide + hydrogenSulfide, hydrogenSulfide);

        _output.WriteLine($"y_CO2 = {carbonDioxide}, y_H2S = {hydrogenSulfide}: ε = {epsilon:F4} °R");
        _output.WriteLine($"Tpc {sweet.Temperature.Kelvins:F3} → {sour.Temperature.Kelvins:F3} K");
        _output.WriteLine($"Ppc {sweet.Pressure.Megapascals:F5} → {sour.Pressure.Megapascals:F5} MPa");

        Assert.Equal(expectedEpsilonRankine, epsilon, 1e-12);
        Assert.Equal(expectedKelvins, sour.Temperature.Kelvins, expectedKelvins * 1e-12);
        Assert.Equal(expectedPascals, sour.Pressure.Pascals, expectedPascals * 1e-12);
    }

    [Fact]
    public void WichertAziz_ShouldLeaveSweetGasUnchanged()
    {
        PseudoCriticalProperties sweet = new SuttonPseudoCriticalCorrelation().Calculate(0.70);
        PseudoCriticalProperties same = WichertAzizCorrection.Apply(sweet, 0.0, 0.0);

        Assert.Equal(sweet.Temperature.Kelvins, same.Temperature.Kelvins, 1e-10);
        Assert.Equal(sweet.Pressure.Pascals, same.Pressure.Pascals, 1e-6);
    }

    [Fact]
    public void WichertAziz_ShouldValidateFractions()
    {
        PseudoCriticalProperties pc = new SuttonPseudoCriticalCorrelation().Calculate(0.70);

        Assert.Throws<ArgumentOutOfRangeException>(() => WichertAzizCorrection.Apply(pc, -0.1, 0.0));
        Assert.Throws<ArgumentException>(() => WichertAzizCorrection.Apply(pc, 0.6, 0.6));
        Assert.True(WichertAzizCorrection.IsWithinValidatedRange(0.10, 0.20));
        Assert.False(WichertAzizCorrection.IsWithinValidatedRange(0.60, 0.0));
    }

    // ------------------------------------------------------------ Carr-Kobayashi-Burrows

    [Fact]
    public void CarrKobayashiBurrows_ShouldApplyPublishedCoefficients()
    {
        PseudoCriticalProperties sweet = new SuttonPseudoCriticalCorrelation().Calculate(0.70);
        PseudoCriticalProperties corrected = CarrKobayashiBurrowsCorrection.Apply(sweet, 0.02, 0.03, 0.05);

        double expectedRankine = sweet.Temperature.DegreesRankine - 80.0 * 0.02 + 130.0 * 0.03 - 250.0 * 0.05;
        double expectedPsia = sweet.Pressure.PoundsForcePerSquareInch + 440.0 * 0.02 + 600.0 * 0.03 - 170.0 * 0.05;

        Assert.Equal(expectedRankine, corrected.Temperature.DegreesRankine, 1e-9);
        Assert.Equal(expectedPsia, corrected.Pressure.PoundsForcePerSquareInch, 1e-9);
        Assert.Throws<ArgumentOutOfRangeException>(() => CarrKobayashiBurrowsCorrection.Apply(sweet, 0.0, 0.0, -0.1));
        Assert.Throws<ArgumentException>(() => CarrKobayashiBurrowsCorrection.Apply(sweet, 0.5, 0.4, 0.2));
    }

    [Fact]
    public void Standing_ShouldExposeBothPublishedIntercepts()
    {
        double whitson = new StandingPseudoCriticalCorrelation(StandingGasSystem.NaturalGas, StandingCoefficientSet.WhitsonBrule)
            .Calculate(0.7).Pressure.PoundsForcePerSquareInch;
        double ahmed = new StandingPseudoCriticalCorrelation(StandingGasSystem.NaturalGas, StandingCoefficientSet.Ahmed)
            .Calculate(0.7).Pressure.PoundsForcePerSquareInch;

        _output.WriteLine($"γ 0.7: Ppc (Whitson & Brulé) = {whitson:F3} psia, Ppc (Ahmed) = {ahmed:F3} psia");

        Assert.Equal(10.0, ahmed - whitson, 1e-9);
    }

    private static IPseudoCriticalCorrelation Correlation(string name) => name switch
    {
        "Sutton" => new SuttonPseudoCriticalCorrelation(),
        "StandingNaturalGas" => new StandingPseudoCriticalCorrelation(StandingGasSystem.NaturalGas, StandingCoefficientSet.WhitsonBrule),
        "StandingGasCondensate" => new StandingPseudoCriticalCorrelation(StandingGasSystem.GasCondensate, StandingCoefficientSet.WhitsonBrule),
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, null)
    };
}
