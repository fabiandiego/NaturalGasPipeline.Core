using UnitsNet;
using Xunit;
using Xunit.Abstractions;
using NaturalGasPipeline.Core.Flow;
using NaturalGasPipeline.Core.Thermodynamics;

namespace NaturalGasPipeline.Hydraulics.Tests;

/// <summary>
/// Isothermal compressible flow in one segment.
///
/// Three independent checks:
/// 1. Implementation: an independent Python implementation of the same
///    closed form (numpy.roots for PR, Lambert W for Colebrook).
/// 2. Physics: direct numerical integration (scipy DOP853, rtol 1e-12) of
///    dp/dx = −ρ·g·sinθ − f·ρ·v²/(2D) with LOCAL Z(p). This tests the
///    approximation of evaluating Z at P_avg, not just the algebra.
/// 3. Published example: Arnold &amp; Stewart, Example 6.6a (general equation).
///
/// Common case: typical natural gas (PR, LGE), D = 0.5 m, L = 50 km,
/// ε = 0.045 mm, T = 288.15 K, P1 = 7 MPa, ṁ = 100 kg/s.
/// </summary>
public class IsothermalGasFlowSolverTests
{
    private readonly ITestOutputHelper _output;

    public IsothermalGasFlowSolverTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static IsothermalGasFlowSolver TypicalGasSolver() =>
        IsothermalGasFlowSolver.ForMixture(
            NaturalGasMixtures.TypicalNaturalGas,
            CompressibilityModelType.PengRobinson,
            new LeeGonzalezEakinViscosityModel());

    private static PipeSegment Segment(
        double diameterMeters,
        double lengthMeters,
        double elevationChangeMeters) =>
        new(
            Length.FromMeters(diameterMeters),
            Length.FromMeters(lengthMeters),
            Length.FromMillimeters(0.045),
            Length.FromMeters(elevationChangeMeters));

    private void Print(GasFlowSolution s)
    {
        _output.WriteLine($"P1 = {s.InletPressure.Bars:F4} bar, P2 = {s.OutletPressure.Bars:F4} bar, ΔP = {s.PressureDrop.Bars:F4} bar");
        _output.WriteLine($"ṁ = {s.MassFlow.KilogramsPerSecond:F4} kg/s");
        _output.WriteLine($"P_avg = {s.AveragePressure.Bars:F4} bar, Z_avg = {s.AverageCompressibilityFactor:F6}, ρ_avg = {s.AverageDensity.KilogramsPerCubicMeter:F3} kg/m³");
        _output.WriteLine($"μ_avg = {s.AverageViscosity.MicropascalSeconds:F4} µPa·s, Re = {s.ReynoldsNumber:E4}, f = {s.DarcyFrictionFactor:F6}");
        _output.WriteLine($"v1 = {s.InletVelocity.MetersPerSecond:F2} m/s, v2 = {s.OutletVelocity.MetersPerSecond:F2} m/s, s = {s.ElevationParameter:E4}");
        _output.WriteLine($"Iterations: {s.Iterations}");

        foreach (string warning in s.Warnings)
        {
            _output.WriteLine($"WARNING: {warning}");
        }
    }

    // ---------------------------------------------------------------- 1

    [Theory]
    [InlineData(0.5, 50_000, 0.0, 7.0e6, 100.0, 3715087.4828586094)]
    [InlineData(0.5, 50_000, 500.0, 7.0e6, 100.0, 3335071.4643247286)]
    [InlineData(0.5, 50_000, -300.0, 7.0e6, 100.0, 3944392.7712881723)]
    [InlineData(0.3, 5_000, 0.0, 5.0e6, 20.0, 4790937.799407471)]
    public void SolveOutletPressure_ShouldMatchIndependentImplementation(
        double diameter,
        double length,
        double elevationChange,
        double inletPascals,
        double massFlowKgPerS,
        double expectedOutletPascals)
    {
        GasFlowSolution s = TypicalGasSolver().SolveOutletPressure(
            Segment(diameter, length, elevationChange),
            Temperature.FromKelvins(288.15),
            Pressure.FromPascals(inletPascals),
            MassFlow.FromKilogramsPerSecond(massFlowKgPerS));

        Print(s);

        double error =
            System.Math.Abs(s.OutletPressure.Pascals - expectedOutletPascals)
            / expectedOutletPascals;

        _output.WriteLine($"Reference P2 = {expectedOutletPascals:F3} Pa, relative error {error:E2}");

        Assert.True(error < 1e-7, $"Relative error {error:E2}");
    }

    // ---------------------------------------------------------------- 2

    /// <summary>
    /// The P_avg approximation against the marching solution with local Z(p).
    /// Measured deviation, as a fraction of ΔP: flat +0.019 %, uphill +0.346 %,
    /// downhill −0.167 %, short line 0.000 %.
    /// </summary>
    [Theory]
    [InlineData(0.5, 50_000, 0.0, 7.0e6, 100.0, 3714475.656, 0.001)]
    [InlineData(0.5, 50_000, 500.0, 7.0e6, 100.0, 3322353.313, 0.005)]
    [InlineData(0.5, 50_000, -300.0, 7.0e6, 100.0, 3949474.818, 0.005)]
    [InlineData(0.3, 5_000, 0.0, 5.0e6, 20.0, 4790937.801, 0.001)]
    public void SolveOutletPressure_ShouldAgreeWithNumericalIntegrationOfTheMomentumEquation(
        double diameter,
        double length,
        double elevationChange,
        double inletPascals,
        double massFlowKgPerS,
        double integratedOutletPascals,
        double allowedFractionOfPressureDrop)
    {
        GasFlowSolution s = TypicalGasSolver().SolveOutletPressure(
            Segment(diameter, length, elevationChange),
            Temperature.FromKelvins(288.15),
            Pressure.FromPascals(inletPascals),
            MassFlow.FromKilogramsPerSecond(massFlowKgPerS));

        double pressureDrop = inletPascals - integratedOutletPascals;
        double deviation = (s.OutletPressure.Pascals - integratedOutletPascals) / pressureDrop;

        _output.WriteLine($"P2 closed form: {s.OutletPressure.Pascals:F1} Pa");
        _output.WriteLine($"P2 integrated:  {integratedOutletPascals:F1} Pa");
        _output.WriteLine($"Deviation: {deviation * 100:F3} % of ΔP (allowed ±{allowedFractionOfPressureDrop * 100:F1} %)");

        Assert.InRange(deviation, -allowedFractionOfPressureDrop, allowedFractionOfPressureDrop);
    }

    // ---------------------------------------------------------------- 3

    /// <summary>
    /// Arnold &amp; Stewart, Surface Production Operations, Example 6.6a (A):
    /// Q = 23 MMSCFD (14.7 psia, 520 °R), S = 0.85, L = 7000 ft,
    /// P1 = 915 psia, T = 540 °R, Z = 0.67, Darcy f read from the Moody chart.
    /// Book: 4 in → P2 = 614 psia (f = 0.0164); 6 in → P2 = 883 psia (f = 0.015).
    /// The book rounds its constant (25.2) and results; tolerance 0.5 %.
    /// </summary>
    [Theory]
    [InlineData(4.0, 0.0164, 614.0)]
    [InlineData(6.0, 0.0150, 883.0)]
    public void SolveOutletPressure_ShouldReproduceBookExample6_6a(
        double diameterInches,
        double darcyFrictionFactor,
        double bookOutletPsia)
    {
        // Gas: specific gravity 0.85 relative to air (M_air = 28.9647 g/mol).
        MolarMass molarMass = MolarMass.FromGramsPerMole(0.85 * 28.9647);

        // 23 MMSCFD → kg/s through the standard conditions stated by the book.
        var standard = new StandardFlowConditions
        {
            Temperature = Temperature.FromDegreesRankine(520.0),
            Pressure = Pressure.FromPoundsForcePerSquareInch(14.7),
            CompressibilityFactor = 1.0
        };

        MassFlow massFlow =
            FlowRateConverter.ConvertStandardVolumeToMass(
                new StandardVolumetricFlow(
                    VolumeFlow.FromCubicFeetPerHour(23.0e6 / 24.0),
                    standard),
                molarMass);

        var solver = new IsothermalGasFlowSolver(
            molarMass,
            new ConstantCompressibilityModel(0.67),
            new ConstantViscosityModel(DynamicViscosity.FromCentipoise(0.013)),
            darcyFrictionFactorOverride: darcyFrictionFactor);

        GasFlowSolution s = solver.SolveOutletPressure(
            PipeSegment.Horizontal(
                Length.FromInches(diameterInches),
                Length.FromFeet(7000.0),
                Length.Zero),
            Temperature.FromDegreesRankine(540.0),
            Pressure.FromPoundsForcePerSquareInch(915.0),
            massFlow);

        double p2Psia = s.OutletPressure.PoundsForcePerSquareInch;
        double deviation = (p2Psia - bookOutletPsia) / bookOutletPsia;

        _output.WriteLine($"Mass flow: {massFlow.KilogramsPerSecond:F6} kg/s");
        _output.WriteLine($"P2 calculated: {p2Psia:F2} psia");
        _output.WriteLine($"P2 book:       {bookOutletPsia:F0} psia");
        _output.WriteLine($"Deviation: {deviation * 100:F2} %");

        Assert.InRange(deviation, -0.005, 0.005);
    }

    // ---------------------------------------------------------------- consistency

    [Fact]
    public void SolveInletPressure_ShouldInvertSolveOutletPressure()
    {
        var solver = TypicalGasSolver();
        var segment = Segment(0.5, 50_000, 500.0);
        var temperature = Temperature.FromKelvins(288.15);
        var massFlow = MassFlow.FromKilogramsPerSecond(100.0);

        GasFlowSolution forward = solver.SolveOutletPressure(
            segment, temperature, Pressure.FromMegapascals(7.0), massFlow);

        GasFlowSolution backward = solver.SolveInletPressure(
            segment, temperature, forward.OutletPressure, massFlow);

        _output.WriteLine($"P1 original:  {7.0e6:F3} Pa");
        _output.WriteLine($"P1 recovered: {backward.InletPressure.Pascals:F3} Pa");

        double error = System.Math.Abs(backward.InletPressure.Pascals - 7.0e6) / 7.0e6;

        Assert.True(error < 1e-8, $"Relative error {error:E2}");
    }

    [Fact]
    public void SolveMassFlow_ShouldInvertSolveOutletPressure()
    {
        var solver = TypicalGasSolver();
        var segment = Segment(0.5, 50_000, -300.0);
        var temperature = Temperature.FromKelvins(288.15);

        GasFlowSolution forward = solver.SolveOutletPressure(
            segment, temperature, Pressure.FromMegapascals(7.0),
            MassFlow.FromKilogramsPerSecond(100.0));

        GasFlowSolution inverse = solver.SolveMassFlow(
            segment, temperature, forward.InletPressure, forward.OutletPressure);

        _output.WriteLine($"ṁ original:  100 kg/s");
        _output.WriteLine($"ṁ recovered: {inverse.MassFlow.KilogramsPerSecond:R} kg/s");

        Assert.Equal(100.0, inverse.MassFlow.KilogramsPerSecond, 6);
    }

    [Fact]
    public void Elevation_ShouldReduceOutletPressureUphillAndRaiseItDownhill()
    {
        var solver = TypicalGasSolver();
        var t = Temperature.FromKelvins(288.15);
        var p1 = Pressure.FromMegapascals(7.0);
        var m = MassFlow.FromKilogramsPerSecond(100.0);

        double flat = solver.SolveOutletPressure(Segment(0.5, 50_000, 0.0), t, p1, m).OutletPressure.Pascals;
        double up = solver.SolveOutletPressure(Segment(0.5, 50_000, 500.0), t, p1, m).OutletPressure.Pascals;
        double down = solver.SolveOutletPressure(Segment(0.5, 50_000, -300.0), t, p1, m).OutletPressure.Pascals;

        _output.WriteLine($"P2 flat {flat:F0} Pa, uphill {up:F0} Pa, downhill {down:F0} Pa");

        Assert.True(up < flat);
        Assert.True(down > flat);
    }

    /// <summary>
    /// (e^s − 1)/s on both sides of the switch between the series and the
    /// exact expression. Reference: Python math.expm1(s)/s (no cancellation).
    /// </summary>
    [Theory]
    [InlineData(0.0, 1.0)]
    [InlineData(0.99e-4, 1.0000495016335404)]
    [InlineData(1.01e-4, 1.0000505017002097)]
    [InlineData(0.01, 1.0050167084168058)]
    [InlineData(-0.05, 0.9754115099857198)]
    public void ElevationFactor_ShouldMatchReferenceOnBothBranches(double s, double expected)
    {
        double value = IsothermalGasFlowSolver.ElevationFactor(s);
        double error = System.Math.Abs(value - expected) / expected;

        _output.WriteLine($"s = {s:E2}: {value:R} (reference {expected:R}, rel. error {error:E1})");

        Assert.True(error < 1e-10, $"Relative error {error:E2}");
    }

    [Fact]
    public void LargePressureDrop_ShouldProduceWarning()
    {
        // Uphill case: P2/P1 ≈ 0.48.
        GasFlowSolution s = TypicalGasSolver().SolveOutletPressure(
            Segment(0.5, 50_000, 500.0),
            Temperature.FromKelvins(288.15),
            Pressure.FromMegapascals(7.0),
            MassFlow.FromKilogramsPerSecond(100.0));

        Print(s);

        Assert.Contains(s.Warnings, w => w.Contains("inlet pressure"));
    }

    [Fact]
    public void MassFlowAboveCapacity_ShouldThrow()
    {
        Assert.Throws<InvalidOperationException>(() =>
            TypicalGasSolver().SolveOutletPressure(
                Segment(0.5, 50_000, 0.0),
                Temperature.FromKelvins(288.15),
                Pressure.FromMegapascals(7.0),
                MassFlow.FromKilogramsPerSecond(1_000.0)));
    }

    [Fact]
    public void SolveMassFlow_WithoutDrivingPressure_ShouldThrow()
    {
        Assert.Throws<InvalidOperationException>(() =>
            TypicalGasSolver().SolveMassFlow(
                Segment(0.5, 50_000, 0.0),
                Temperature.FromKelvins(288.15),
                Pressure.FromMegapascals(5.0),
                Pressure.FromMegapascals(5.0)));
    }
}
