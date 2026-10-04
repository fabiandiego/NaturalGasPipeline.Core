using NaturalGasPipeline.Core.Common;
using NaturalGasPipeline.Thermodynamics;
using UnitsNet;
using Xunit;
using Xunit.Abstractions;

namespace NaturalGasPipeline.Compression.Tests;

/// <summary>
/// Compression stage and station.
///
/// Published reference: CED Engineering, "Gas Pipeline Hydraulics"
/// (P06-001, after Menon), Example 14: Q = 100 MMSCFD, T1 = 80 °F, P1 = 800
/// psia, r = 1.6, Z1 = 1.0, Z2 = 0.85, γ = 1.4, ηa = 0.8 → HP = 2692,
/// BHP = 2747 (mechanical efficiency 0.98).
/// The course formula HP = 0.0857·(γ/(γ−1))·Q·T1·(Z1+Z2)/2·(r^((γ−1)/γ) − 1)/ηa
/// uses a rounded constant: for Tb = 520 °R, Pb = 14.7 psia the exact value is
/// 0.085664 (−0.04 %), so this library gives 2690.5 HP (−0.06 %).
///
/// Horsepower is converted here with its exact definition, 1 hp = 550 ft·lbf/s
/// = 550 · 0.3048 · 0.45359237 · 9.80665 W = 745.69987158 W. UnitsNet 5.75.1
/// returns 2690.536 hp for the same power (+0.0013 %), i.e. it uses a rounded
/// factor (≈ 745.69 W/hp); results are asserted in watts to stay independent
/// of that.
/// </summary>
public class CompressorStageTests
{
    private const double R = PhysicalConstants.MolarGasConstant;

    // 1 mechanical hp = 550 ft·lbf/s, exact from the definitions of ft, lb and g_n.
    private const double WattsPerHorsepower = 550.0 * 0.3048 * 0.45359237 * 9.80665;

    private readonly ITestOutputHelper _output;

    public CompressorStageTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static readonly MolarMass Gas06 =
        MolarMass.FromKilogramsPerMole(0.6 * DryAirMolarMass.Gpa2145_09.KilogramsPerMole);

    /// <summary>Z = 1.0 below 1000 psia and 0.85 above: the two values given in Example 14.</summary>
    private sealed class TwoValueCompressibility : ICompressibilityModel
    {
        public double Calculate(Pressure pressure, Temperature temperature) =>
            pressure.PoundsForcePerSquareInch < 1000.0 ? 1.0 : 0.85;
    }

    [Fact]
    public void IsentropicStage_ShouldReproduceCedExample14()
    {
        // 100 MMSCFD at 520 °R, 14.7 psia → mass flow.
        double baseDensity = Pressure.FromPoundsForcePerSquareInch(14.7).Pascals * Gas06.KilogramsPerMole
                             / (R * Temperature.FromDegreesRankine(520.0).Kelvins);
        var massFlow = MassFlow.FromKilogramsPerSecond(100e6 * 0.028316846592 / 86400.0 * baseDensity);

        var stage = new CompressorStage(
            Gas06, new TwoValueCompressibility(), 1.4, CompressionEfficiencyBasis.Isentropic, 0.8, 0.98);

        CompressionResult r = stage.Compress(
            Pressure.FromPoundsForcePerSquareInch(800.0),
            Temperature.FromDegreesRankine(540.0),
            Pressure.FromPoundsForcePerSquareInch(1280.0),
            massFlow);

        _output.WriteLine($"ṁ = {massFlow.KilogramsPerSecond:F4} kg/s, r = {r.PressureRatio:F3}, Z1 = {r.SuctionCompressibilityFactor}, Z2 = {r.DischargeCompressibilityFactor}");
        _output.WriteLine($"T2 = {r.DischargeTemperature.DegreesFahrenheit:F2} °F, head = {r.Head.KilojoulesPerKilogram:F3} kJ/kg");
        double gasWatts = (double)r.GasPower.Watts;
        double shaftWatts = (double)r.ShaftPower.Watts;
        double hp = gasWatts / WattsPerHorsepower;
        double bhp = shaftWatts / WattsPerHorsepower;

        _output.WriteLine($"P_gas = {gasWatts:F2} W = {hp:F3} hp (published 2692), P_shaft = {bhp:F3} hp (published 2747)");
        _output.WriteLine($"UnitsNet MechanicalHorsepower for comparison: {r.GasPower.MechanicalHorsepower:F3}");

        Assert.Equal(2006305.6761488947, gasWatts, 1e-4);
        Assert.Equal(2690.500230195556, hp, 1e-6);
        Assert.InRange(hp / 2692.0 - 1.0, -0.001, 0.001);
        Assert.InRange(bhp / 2747.0 - 1.0, -0.001, 0.001);
    }

    /// <summary>
    /// For an ideal gas, ηs and ηp that give the same discharge temperature
    /// give the same power: ηs = (r^((k−1)/k) − 1) / (r^((k−1)/(k·ηp)) − 1).
    /// Python reference (k 1.3, r 2, ηp 0.8): ηs = 0.78369330169, P = 1 376 716.76 W, T2 = 366.4012 K.
    /// </summary>
    [Fact]
    public void IsentropicAndPolytropicBases_ShouldAgreeForEquivalentEfficiencies()
    {
        var idealGas = new ConstantCompressibilityModel(1.0);
        double k = 1.3;
        double r = 2.0;
        double etaP = 0.8;
        double etaS = (System.Math.Pow(r, (k - 1) / k) - 1) / (System.Math.Pow(r, (k - 1) / (k * etaP)) - 1);

        var flow = MassFlow.FromKilogramsPerSecond(10.0);
        var p1 = Pressure.FromMegapascals(5.0);
        var p2 = Pressure.FromMegapascals(10.0);
        var t1 = Temperature.FromKelvins(300.0);

        CompressionResult isentropic = new CompressorStage(Gas06, idealGas, k, CompressionEfficiencyBasis.Isentropic, etaS, 1.0)
            .Compress(p1, t1, p2, flow);
        CompressionResult polytropic = new CompressorStage(Gas06, idealGas, k, CompressionEfficiencyBasis.Polytropic, etaP, 1.0)
            .Compress(p1, t1, p2, flow);

        _output.WriteLine($"ηs = {etaS:F8}: P = {isentropic.GasPower.Watts:F2} W, T2 = {isentropic.DischargeTemperature.Kelvins:F4} K");
        _output.WriteLine($"ηp = {etaP}: P = {polytropic.GasPower.Watts:F2} W, T2 = {polytropic.DischargeTemperature.Kelvins:F4} K");

        Assert.Equal(0.7836933016923301, etaS, 1e-12);
        Assert.Equal(1376716.7588517107, (double)isentropic.GasPower.Watts, 1e-4);
        Assert.Equal((double)isentropic.GasPower.Watts, (double)polytropic.GasPower.Watts, 1e-6);
        Assert.Equal(366.40115194172927, polytropic.DischargeTemperature.Kelvins, 1e-9);
    }

    /// <summary>Ideal gas: the gas power is exactly the enthalpy rise ṁ·cp·(T2 − T1), cp = k/(k−1)·R/M.</summary>
    [Theory]
    [InlineData(CompressionEfficiencyBasis.Isentropic, 0.78)]
    [InlineData(CompressionEfficiencyBasis.Polytropic, 0.82)]
    public void GasPower_ShouldEqualEnthalpyRiseForAnIdealGas(CompressionEfficiencyBasis basis, double efficiency)
    {
        double k = 1.28;
        var stage = new CompressorStage(Gas06, new ConstantCompressibilityModel(1.0), k, basis, efficiency, 0.97);

        CompressionResult r = stage.Compress(
            Pressure.FromMegapascals(4.5), Temperature.FromKelvins(293.15), Pressure.FromMegapascals(7.2),
            MassFlow.FromKilogramsPerSecond(80.0));

        double cp = k / (k - 1.0) * R / Gas06.KilogramsPerMole;
        double enthalpyRise = 80.0 * cp * (r.DischargeTemperature.Kelvins - 293.15);

        _output.WriteLine($"{basis}: P_gas = {r.GasPower.Megawatts:F6} MW, ṁ·cp·ΔT = {enthalpyRise / 1e6:F6} MW, P_shaft = {r.ShaftPower.Megawatts:F6} MW");

        Assert.Equal(enthalpyRise, (double)r.GasPower.Watts, enthalpyRise * 1e-12);
        Assert.Equal((double)r.GasPower.Watts / 0.97, (double)r.ShaftPower.Watts, 1e-6);
    }

    /// <summary>
    /// Polytropic stages without intercooling telescope exactly into one
    /// stage of the overall ratio (ideal gas); intercooling saves power.
    /// </summary>
    [Fact]
    public void Station_ShouldSplitTheRatioAndApplyIntercooling()
    {
        var stage = new CompressorStage(Gas06, new ConstantCompressibilityModel(1.0), 1.3, CompressionEfficiencyBasis.Polytropic, 0.8, 1.0);
        var p1 = Pressure.FromMegapascals(2.0);
        var p2 = Pressure.FromMegapascals(9.0);
        var t1 = Temperature.FromKelvins(300.0);
        var flow = MassFlow.FromKilogramsPerSecond(50.0);

        CompressorStationResult single = new CompressorStation("1 stage", stage, 1).Compress(p1, t1, p2, flow);
        CompressorStationResult threeHot = new CompressorStation("3 stages", stage, 3).Compress(p1, t1, p2, flow);
        CompressorStationResult threeCooled = new CompressorStation("3 stages, intercooled", stage, 3, t1).Compress(p1, t1, p2, flow);

        foreach (CompressorStationResult s in new[] { single, threeHot, threeCooled })
        {
            _output.WriteLine($"{s.Name}: P = {s.GasPower.Megawatts:F4} MW, T2 = {s.DischargeTemperature.Kelvins:F2} K, stage r = {s.Stages[0].PressureRatio:F4}");
        }

        Assert.Equal(System.Math.Pow(4.5, 1.0 / 3.0), threeHot.Stages[0].PressureRatio, 12);
        Assert.Equal(9.0e6, threeHot.Stages[^1].DischargePressure.Pascals, 1e-6);
        Assert.Equal((double)single.GasPower.Watts, (double)threeHot.GasPower.Watts, (double)single.GasPower.Watts * 1e-12);
        Assert.Equal(single.DischargeTemperature.Kelvins, threeHot.DischargeTemperature.Kelvins, 1e-9);
        Assert.True((double)threeCooled.GasPower.Watts < 0.9 * (double)single.GasPower.Watts);
    }

    [Fact]
    public void InvalidInputs_ShouldThrow()
    {
        var z = new ConstantCompressibilityModel(1.0);

        Assert.Throws<ArgumentOutOfRangeException>(() => new CompressorStage(Gas06, z, 1.0, CompressionEfficiencyBasis.Isentropic, 0.8, 1.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CompressorStage(Gas06, z, 1.3, CompressionEfficiencyBasis.Isentropic, 1.2, 1.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CompressorStation("x", new CompressorStage(Gas06, z, 1.3, CompressionEfficiencyBasis.Isentropic, 0.8, 1.0), 0));

        var stage = new CompressorStage(Gas06, z, 1.3, CompressionEfficiencyBasis.Isentropic, 0.8, 1.0);
        Assert.Throws<ArgumentException>(() => stage.Compress(
            Pressure.FromMegapascals(5.0), Temperature.FromKelvins(300.0), Pressure.FromMegapascals(4.0), MassFlow.FromKilogramsPerSecond(1.0)));
    }
}
