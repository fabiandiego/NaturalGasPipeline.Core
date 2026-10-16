using UnitsNet;
using Xunit;
using Xunit.Abstractions;

namespace NaturalGasPipeline.Hydraulics.Tests;

/// <summary>
/// Mean gas velocity and API RP 14E erosional velocity
/// (Arnold &amp; Stewart, Surface Production Operations, 2016, Eq. 6.15:
/// V_e = C/√ρ, ft/s and lbm/ft³).
/// </summary>
public class GasVelocityCalculatorTests
{
    private readonly ITestOutputHelper _output;

    public GasVelocityCalculatorTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Erosional_OnePoundPerCubicFoot_C100_ShouldBe100FeetPerSecond()
    {
        // ρ = 1 lbm/ft³ → V_e = 100/√1 = 100 ft/s = 30.48 m/s exactly.
        // 1 lbm/ft³ is built from the exact definitions (0.45359237 kg / 0.3048³ m³):
        // UnitsNet 5.75.1 Density.FromPoundsPerCubicFoot uses a rounded factor
        // (density −6.8e-9 relative), which shows up here as V_e = 30.4800001 m/s.
        var rho = Density.FromKilogramsPerCubicMeter(0.45359237 / (0.3048 * 0.3048 * 0.3048));
        Speed ve = GasVelocityCalculator.ErosionalVelocity(rho);

        _output.WriteLine($"ρ = {rho.KilogramsPerCubicMeter:F6} kg/m³ → V_e = {ve.FeetPerSecond:F9} ft/s = {ve.MetersPerSecond:F9} m/s");
        Assert.Equal(30.48, ve.MetersPerSecond, 1e-9);
    }

    [Fact]
    public void Erosional_SiForm_ShouldBe1Point2199TimesCOverRootRho()
    {
        // SI form: V_e = C·0.3048·√(0.45359237/0.3048³)/√ρ = 1.21990…·C/√ρ.
        double k = 0.3048 * System.Math.Sqrt(0.45359237 / (0.3048 * 0.3048 * 0.3048));
        var rho = Density.FromKilogramsPerCubicMeter(44.37);

        Speed ve = GasVelocityCalculator.ErosionalVelocity(rho, GasVelocityCalculator.CorrosiveServiceConstant);

        _output.WriteLine($"k = {k:F6} (m/s)·(kg/m³)^½ per unit C; V_e(60, 44.37 kg/m³) = {ve.MetersPerSecond:F4} m/s");
        Assert.Equal(1.2199, k, 1e-4);
        Assert.Equal(k * 60.0 / System.Math.Sqrt(44.37), ve.MetersPerSecond, 1e-9);
    }

    [Fact]
    public void Velocity_ShouldBeMassFlowOverDensityTimesArea()
    {
        // 245 kg/s, ρ = 44.37 kg/m³, D = 0.882 m: V = 245/(44.37·π·0.882²/4).
        Speed v = GasVelocityCalculator.Velocity(
            MassFlow.FromKilogramsPerSecond(245.0), Density.FromKilogramsPerCubicMeter(44.37), Length.FromMeters(0.882));

        double expected = 245.0 / (44.37 * System.Math.PI * 0.882 * 0.882 / 4.0);
        _output.WriteLine($"V = {v.MetersPerSecond:F4} m/s");
        Assert.Equal(expected, v.MetersPerSecond, 1e-12);
    }

    [Fact]
    public void NonPositiveDensity_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GasVelocityCalculator.ErosionalVelocity(Density.Zero));
    }
}
