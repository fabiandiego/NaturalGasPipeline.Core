using NaturalGasPipeline.Compression;
using NaturalGasPipeline.Core.Flow;
using NaturalGasPipeline.Core.Pipeline;
using NaturalGasPipeline.Correlations;
using NaturalGasPipeline.Hydraulics;
using NaturalGasPipeline.Thermodynamics;
using UnitsNet;
using Xunit;
using Xunit.Abstractions;

namespace NaturalGasPipeline.Network.Tests;

/// <summary>
/// Series systems: segment marching, route profiles, compressors.
///
/// Physics reference: direct numerical integration (scipy DOP853, rtol 1e-12)
/// of dp/dx = −ρ·g·sinθ − f·ρ·v²/(2D) with LOCAL Z(p), μ(p) and Colebrook f,
/// typical natural gas (Peng-Robinson, Lee-Gonzalez-Eakin), D = 0.5 m,
/// L = 50 km, ε = 0.045 mm, T = 288.15 K, P1 = 7 MPa, ṁ = 100 kg/s
/// (independent Python implementation, 2026-10-04).
/// Splitting the line into N segments, each with Z, μ, f at its own P_avg,
/// converges to it as 1/N²; measured deviation (fraction of ΔP):
///   +500 m: N=1 +0.347 %, N=2 +0.087 %, N=5 +0.014 %, N=10 +0.0035 %, N=20 +0.0009 %
///   −300 m: N=1 −0.163 %, N=20 −0.0004 %
/// </summary>
public class SeriesSystemTests
{
    private readonly ITestOutputHelper _output;

    public SeriesSystemTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static readonly GasMixture Gas = NaturalGasMixtures.TypicalNaturalGas;

    private static ISegmentModel TypicalModel() =>
        new IsothermalSegmentModel(
            Gas.CalculateMolarMass(),
            CompressibilityModelFactory.Create(CompressibilityModelType.PengRobinson, Gas),
            new LeeGonzalezEakinViscosityModel());

    private static readonly Temperature T = Temperature.FromKelvins(288.15);

    [Theory]
    [InlineData(500.0, 3322299.2251226353)]
    [InlineData(-300.0, 3949350.2151696347)]
    public void Subdivision_ShouldConvergeToTheIntegratedMomentumEquation(double rise, double integratedOutletPascals)
    {
        var line = new PipeSegment(Length.FromMeters(0.5), Length.FromKilometers(50.0), Length.FromMillimeters(0.045), Length.FromMeters(rise));
        ISegmentModel model = TypicalModel();
        double dropReference = 7.0e6 - integratedOutletPascals;

        double Deviation(int n)
        {
            SeriesSystem system = new(PipelineProfile.Subdivide(line, n)
                .Select((s, i) => (SystemElement)new PipeElement($"S{i + 1}", s, T, model)));

            double p2 = system.SolveForward(Pressure.FromMegapascals(7.0), T, MassFlow.FromKilogramsPerSecond(100.0))
                .OutletPressure.Pascals;

            double deviation = (p2 - integratedOutletPascals) / dropReference;
            _output.WriteLine($"Δz = {rise} m, N = {n,2}: P2 = {p2:F1} Pa, deviation {deviation * 100:+0.0000;-0.0000} % of ΔP");
            return deviation;
        }

        double d1 = Deviation(1);
        double d10 = Deviation(10);
        double d20 = Deviation(20);

        Assert.InRange(System.Math.Abs(d20), 0.0, 2e-5);
        Assert.InRange(d10 / d20, 3.5, 4.5);          // second order
        Assert.True(System.Math.Abs(d1) > 100 * System.Math.Abs(d20));
    }

    [Fact]
    public void Profile_ShouldBuildUniformSlopeSegments()
    {
        var points = new[]
        {
            new ProfilePoint(Length.Zero, Length.FromMeters(100.0)),
            new ProfilePoint(Length.FromKilometers(10.0), Length.FromMeters(350.0)),
            new ProfilePoint(Length.FromKilometers(25.0), Length.FromMeters(50.0)),
        };

        IReadOnlyList<PipeSegment> segments = PipelineProfile.ToSegments(points, Length.FromMeters(0.5), Length.FromMillimeters(0.045));

        Assert.Equal(2, segments.Count);
        Assert.Equal(10_000.0, segments[0].Length.Meters, 9);
        Assert.Equal(250.0, segments[0].ElevationChange.Meters, 9);
        Assert.Equal(15_000.0, segments[1].Length.Meters, 9);
        Assert.Equal(-300.0, segments[1].ElevationChange.Meters, 9);

        Assert.Throws<ArgumentException>(() => PipelineProfile.ToSegments(
            new[] { points[1], points[0] }, Length.FromMeters(0.5), Length.Zero));
        Assert.Throws<ArgumentException>(() => PipelineProfile.ToSegments(
            new[] { new ProfilePoint(Length.Zero, Length.Zero), new ProfilePoint(Length.FromMeters(10.0), Length.FromMeters(20.0)) },
            Length.FromMeters(0.5), Length.Zero));
    }

    /// <summary>
    /// CED course P06-001 (Menon), Example 12: 100 mi, D = 15.5 in, F = 20,
    /// Z = 0.844, 100 MMSCFD, P2 = 1000 psig → P1 = 1180.44 psig. With Z and F
    /// constant, P1² − P2² is additive, so two 50 mi segments must give exactly
    /// the single-segment result. (The course quotes 1183.75 psig for two
    /// segments with Z recomputed per segment; the method behind that number
    /// is not given in the retrieved text and is not used as a reference.)
    /// </summary>
    [Fact]
    public void TwoSegments_ShouldMatchCedExample12WhenZAndFAreConstant()
    {
        var baseConditions = new StandardFlowConditions
        {
            Temperature = Temperature.FromDegreesRankine(520.0),
            Pressure = Pressure.FromPoundsForcePerSquareInch(14.7),
            CompressibilityFactor = 1.0
        };

        var gas = new FlowEquationGas(
            0.6, DryAirMolarMass.Gpa2145_09, new ConstantCompressibilityModel(0.844),
            new ConstantViscosityModel(DynamicViscosity.FromPascalSeconds(1.2e-5)));

        var model = new FlowEquationSegmentModel(
            new GeneralFlowEquation(new ConstantTransmissionFactorModel(20.0)), gas, baseConditions);

        MassFlow massFlow = FlowRateConverter.ConvertStandardVolumeToMass(
            new StandardVolumetricFlow(VolumeFlow.FromCubicMetersPerSecond(100e6 * 0.028316846592 / 86400.0), baseConditions),
            gas.MolarMass);

        var half = new PipeSegment(Length.FromInches(15.5), Length.FromMiles(50.0), Length.Zero, Length.Zero);
        var tf = Temperature.FromDegreesRankine(530.0);

        var system = new SeriesSystem(new SystemElement[]
        {
            new PipeElement("Upstream 50 mi", half, tf, model),
            new PipeElement("Downstream 50 mi", half, tf, model)
        });

        SeriesSolution s = system.SolveBackward(Pressure.FromPoundsForcePerSquareInch(1014.7), tf, massFlow);

        foreach (NodeState n in s.Nodes)
        {
            _output.WriteLine($"{n.Name}: {n.Pressure.PoundsForcePerSquareInch - 14.7:F4} psig");
        }

        Assert.Equal(1180.4376245763137, s.InletPressure.PoundsForcePerSquareInch - 14.7, 1e-7);
    }

    /// <summary>
    /// Pipe → 2-stage station (r = 1.6, intercooled) → aftercooler → pipe.
    /// Forward, backward and flow solutions must agree; the reported power
    /// must equal the station evaluated at the solved node pressures.
    /// </summary>
    [Fact]
    public void CompressorSystem_ShouldBeConsistentInAllSolveModes()
    {
        ISegmentModel model = TypicalModel();
        var stage = new CompressorStage(
            Gas.CalculateMolarMass(),
            CompressibilityModelFactory.Create(CompressibilityModelType.PengRobinson, Gas),
            1.30, CompressionEfficiencyBasis.Polytropic, 0.82, 0.98);

        var station = new CompressorStation("CS-1", stage, 2, Temperature.FromKelvins(298.15));
        var segment = new PipeSegment(Length.FromMeters(0.6), Length.FromKilometers(80.0), Length.FromMillimeters(0.045), Length.FromMeters(120.0));

        var system = new SeriesSystem(new SystemElement[]
        {
            new PipeElement("Line A", segment, T, model),
            new CompressorElement("CS-1", station, 1.6),
            new CoolerElement("Aftercooler", T, Pressure.FromKilopascals(50.0)),
            new PipeElement("Line B", segment, T, model)
        });

        var p1 = Pressure.FromMegapascals(7.0);
        var m = MassFlow.FromKilogramsPerSecond(100.0);

        SeriesSolution forward = system.SolveForward(p1, T, m);
        SeriesSolution backward = system.SolveBackward(forward.OutletPressure, T, m);
        SeriesSolution flow = system.SolveMassFlow(p1, T, forward.OutletPressure, MassFlow.FromKilogramsPerSecond(60.0));

        foreach (NodeState n in forward.Nodes)
        {
            _output.WriteLine($"{n.Name,-20} P = {n.Pressure.Megapascals:F5} MPa, T = {n.Temperature.Kelvins:F2} K");
        }

        _output.WriteLine($"Shaft power = {forward.CompressorShaftPower.Megawatts:F4} MW; flow solve: ṁ = {flow.MassFlow.KilogramsPerSecond:F8} kg/s");

        foreach (string w in forward.Warnings)
        {
            _output.WriteLine($"WARNING: {w}");
        }

        var cs = (CompressorStationResult)forward.ElementResults[1];
        CompressorStationResult direct = station.Compress(forward.Nodes[1].Pressure, T, forward.Nodes[2].Pressure, m);

        Assert.Equal(1.6, forward.Nodes[2].Pressure.Pascals / forward.Nodes[1].Pressure.Pascals, 12);
        Assert.Equal(forward.Nodes[2].Pressure.Pascals - 50_000.0, forward.Nodes[3].Pressure.Pascals, 1e-6);
        Assert.Equal((double)direct.ShaftPower.Watts, (double)cs.ShaftPower.Watts, 1e-6);
        Assert.Equal((double)cs.ShaftPower.Watts, (double)forward.CompressorShaftPower.Watts, 1e-6);
        Assert.Equal(1.0, backward.InletPressure.Pascals / p1.Pascals, 1e-8);
        Assert.Equal(1.0, flow.MassFlow.KilogramsPerSecond / m.KilogramsPerSecond, 1e-7);
        Assert.DoesNotContain(forward.Warnings, w => w.Contains("isothermal"));
    }

    [Fact]
    public void HotGasEnteringAnIsothermalPipe_ShouldBeReported()
    {
        ISegmentModel model = TypicalModel();
        var stage = new CompressorStage(
            Gas.CalculateMolarMass(), new ConstantCompressibilityModel(0.9),
            1.30, CompressionEfficiencyBasis.Isentropic, 0.8, 1.0);
        var segment = new PipeSegment(Length.FromMeters(0.6), Length.FromKilometers(40.0), Length.FromMillimeters(0.045), Length.Zero);

        var system = new SeriesSystem(new SystemElement[]
        {
            new CompressorElement("CS", new CompressorStation("CS", stage, 1), 1.5),
            new PipeElement("Line", segment, T, model)
        });

        SeriesSolution s = system.SolveForward(Pressure.FromMegapascals(5.0), T, MassFlow.FromKilogramsPerSecond(80.0));

        foreach (string w in s.Warnings)
        {
            _output.WriteLine(w);
        }

        Assert.Contains(s.Warnings, w => w.Contains("isothermal"));
    }

    [Fact]
    public void InvalidSystems_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => new SeriesSystem(Array.Empty<SystemElement>()));
        Assert.Throws<ArgumentException>(() => new SeriesSystem(new SystemElement[]
        {
            new CompressorElement("CS", new CompressorStation("CS",
                new CompressorStage(Gas.CalculateMolarMass(), new ConstantCompressibilityModel(0.9), 1.3, CompressionEfficiencyBasis.Isentropic, 0.8, 1.0), 1), 0.8)
        }));
    }
}
