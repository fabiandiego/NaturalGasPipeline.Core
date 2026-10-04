using NaturalGasPipeline.Core.Flow;
using NaturalGasPipeline.Core.Pipeline;
using NaturalGasPipeline.Hydraulics;
using NaturalGasPipeline.Thermodynamics;
using UnitsNet;
using Xunit;
using Xunit.Abstractions;

namespace NaturalGasPipeline.Correlations.Tests;

/// <summary>
/// Pipeline flow equations against published worked examples.
///
/// [CED]  CED Engineering, "Gas Pipeline Hydraulics", course P06-001 (USCS
///        part after Menon, Gas Pipeline Hydraulics, 2005).
///        https://www.cedengineering.com/userfiles/Gas%20Pipeline%20Hydraulics%20R1.pdf
///        The flowing temperature of Examples 11 and 12 was not legible in the
///        retrieved text; 70 °F (530 °R) with Tb = 520 °R, Pb = 14.7 psia and
///        P_atm = 14.7 psia reproduces all four published results to 0.01 psi,
///        so those are the givens used here.
/// [A&amp;S]  Arnold &amp; Stewart, Surface Production Operations, ch. 6 (project
///        document), base conditions 520 °R and 14.7 psia.
///
/// Implementation references: independent Python implementation of the same
/// equations (scipy-free fixed point / Brent), 2026-10-04.
/// </summary>
public class GasFlowEquationValidationTests
{
    private readonly ITestOutputHelper _output;

    public GasFlowEquationValidationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    // ------------------------------------------------------------ CED Example 11

    /// <summary>
    /// [CED] Example 11: NPS 18 × 0.250 in (D = 17.5 in), 20 mi, Q = 150 MMSCFD,
    /// P1 = 1000 psig, G = 0.6, Z = 0.85, E = 0.95. Published P2 (psia):
    /// Panhandle A 970.81, Panhandle B 971.81, Weymouth 946.24.
    /// </summary>
    [Theory]
    [InlineData("PanhandleA", 970.81, 970.810340850351)]
    [InlineData("PanhandleB", 971.81, 971.8088802858032)]
    [InlineData("Weymouth", 946.24, 946.2408206853545)]
    public void EmpiricalEquations_ShouldReproduceCedExample11(string equation, double published, double reference)
    {
        PowerLawFlowEquation eq = equation switch
        {
            "PanhandleA" => GasFlowEquations.PanhandleA(0.95),
            "PanhandleB" => GasFlowEquations.PanhandleB(0.95),
            _ => GasFlowEquations.Weymouth(0.95)
        };

        FlowEquationSolution s = Solver(eq, 0.6, 0.85).SolveOutletPressure(
            Segment(17.5, Length.FromMiles(20.0)),
            Temperature.FromDegreesRankine(530.0),
            Psia(1014.7),
            Mmscfd(150.0));

        Print(s);
        _output.WriteLine($"Published P2 = {published} psia");

        Assert.InRange(s.OutletPressure.PoundsForcePerSquareInch - published, -0.005, 0.005);
        Assert.Equal(reference, s.OutletPressure.PoundsForcePerSquareInch, reference * 1e-10);
    }

    // ------------------------------------------------------------ CED Example 12

    /// <summary>
    /// [CED] Example 12: NPS 16 × 0.250 in (D = 15.5 in), 100 mi, Q = 100 MMSCFD,
    /// P2 = 1000 psig, G = 0.6, Z = 0.844, F = 20 → P1 = 1180.44 psig.
    /// </summary>
    [Fact]
    public void GeneralFlow_ShouldReproduceCedExample12()
    {
        var eq = new GeneralFlowEquation(new ConstantTransmissionFactorModel(20.0));

        FlowEquationSolution s = Solver(eq, 0.6, 0.844).SolveInletPressure(
            Segment(15.5, Length.FromMiles(100.0)),
            Temperature.FromDegreesRankine(530.0),
            Psia(1014.7),
            Mmscfd(100.0));

        Print(s);

        double p1Gauge = s.InletPressure.PoundsForcePerSquareInch - 14.7;

        Assert.InRange(p1Gauge - 1180.44, -0.005, 0.005);
        Assert.Equal(1180.4376245763137, p1Gauge, 1e-7);
        Assert.Equal(20.0, s.TransmissionFactor!.Value, 12);
    }

    // ------------------------------------------------------------ Arnold & Stewart

    /// <summary>
    /// [A&amp;S] Examples 6.2 (Weymouth, E = 1) and 6.5 (Panhandle B, E = 0.9):
    /// Q = 100 MMSCFD, 1214.7 → 1014.7 psia, G = 0.621, 530 °R, 15 mi,
    /// Z = 0.863. Published diameters 11.26 in and 10.40 in.
    /// </summary>
    [Theory]
    [InlineData("Weymouth", 1.0, 11.26, 11.259978300215105)]
    [InlineData("PanhandleB", 0.9, 10.40, 10.396021741480638)]
    public void Sizing_ShouldReproduceArnoldStewartExamples(string equation, double efficiency, double published, double reference)
    {
        PowerLawFlowEquation eq = equation == "Weymouth"
            ? GasFlowEquations.Weymouth(efficiency)
            : GasFlowEquations.PanhandleB(efficiency);

        FlowEquationSolution s = Solver(eq, 0.621, 0.863).SolveInternalDiameter(
            Length.FromMiles(15.0), Length.Zero, Length.Zero,
            Temperature.FromDegreesRankine(530.0),
            Psia(1214.7), Psia(1014.7), Mmscfd(100.0));

        Print(s);
        _output.WriteLine($"Published D = {published} in");

        Assert.InRange(s.InternalDiameter.Inches - published, -0.005, 0.005);
        Assert.Equal(reference, s.InternalDiameter.Inches, reference * 1e-9);
    }

    /// <summary>
    /// [A&amp;S] Example 6.6a (C), Panhandle B, E = 0.95: 23 MMSCFD, G = 0.85,
    /// 7000 ft, P1 = 915 psia, 540 °R, Z = 0.67 → P2 = 771 psia (4 in),
    /// 897 psia (6 in). The book prints the constant as "0.0028"; its results
    /// follow from 0.028 (≡ 737 with Tb = 520 °R, Pb = 14.7 psia).
    /// </summary>
    [Theory]
    [InlineData(4.0, 771.0, 771.0808842272697)]
    [InlineData(6.0, 897.0, 897.0828853616911)]
    public void PanhandleB_ShouldReproduceArnoldStewartExample6_6a(double diameterInches, double published, double reference)
    {
        FlowEquationSolution s = Solver(GasFlowEquations.PanhandleB(0.95), 0.85, 0.67).SolveOutletPressure(
            Segment(diameterInches, Length.FromFeet(7000.0)),
            Temperature.FromDegreesRankine(540.0),
            Psia(915.0),
            Mmscfd(23.0));

        Print(s);

        Assert.InRange(s.OutletPressure.PoundsForcePerSquareInch - published, -0.5, 0.5);
        Assert.Equal(reference, s.OutletPressure.PoundsForcePerSquareInch, reference * 1e-10);
    }

    /// <summary>
    /// [A&amp;S] Example 6.7, AGA: F = 18.18 (fully turbulent at d = 11 in),
    /// 100 ft climb. The book prints d = 10.90 in, but its own intermediate
    /// result d^−2.5 = 2.532·10⁻³ gives 10.93 in; recomputing its formula
    /// (with the linearised elevation term Ec) gives 10.9292 in. This library
    /// (exact e^s / Le elevation form) gives 10.9293 in.
    /// </summary>
    [Fact]
    public void Aga_ShouldReproduceArnoldStewartExample6_7()
    {
        var eq = new GeneralFlowEquation(new ConstantTransmissionFactorModel(18.18));

        FlowEquationSolution s = Solver(eq, 0.621, 0.863).SolveInternalDiameter(
            Length.FromMiles(15.0), Length.Zero, Length.FromFeet(100.0),
            Temperature.FromDegreesRankine(530.0),
            Psia(1214.7), Psia(1014.7), Mmscfd(100.0));

        Print(s);

        Assert.InRange(s.InternalDiameter.Inches / 10.929189718495163 - 1.0, -1e-4, 1e-4);
        Assert.Equal(10.929255144933586, s.InternalDiameter.Inches, 1e-8);
    }

    // ------------------------------------------------------------ helpers

    internal static GasFlowEquationSolver Solver(IGasFlowEquation equation, double gravity, double z) =>
        new(equation,
            new FlowEquationGas(
                gravity,
                DryAirMolarMass.Gpa2145_09,
                new ConstantCompressibilityModel(z),
                new ConstantViscosityModel(DynamicViscosity.FromPascalSeconds(1.2e-5))),
            Base520());

    internal static StandardFlowConditions Base520() => new()
    {
        Temperature = Temperature.FromDegreesRankine(520.0),
        Pressure = Pressure.FromPoundsForcePerSquareInch(14.7),
        CompressibilityFactor = 1.0
    };

    internal static PipeSegment Segment(double diameterInches, Length length) =>
        new(Length.FromInches(diameterInches), length, Length.Zero, Length.Zero);

    internal static Pressure Psia(double psia) => Pressure.FromPoundsForcePerSquareInch(psia);

    /// <summary>Million standard cubic feet per day at the 520 °R / 14.7 psia base.</summary>
    internal static StandardVolumetricFlow Mmscfd(double mmscfd) =>
        new(VolumeFlow.FromCubicMetersPerSecond(mmscfd * 1e6 * 0.028316846592 / 86400.0), Base520());

    private void Print(FlowEquationSolution s)
    {
        _output.WriteLine($"{s.Equation}");
        _output.WriteLine($"P1 = {s.InletPressure.PoundsForcePerSquareInch:F4} psia, P2 = {s.OutletPressure.PoundsForcePerSquareInch:F4} psia");
        _output.WriteLine($"Q = {s.StandardFlow.Value.CubicMetersPerSecond * 86400.0 / 0.028316846592 / 1e6:F4} MMSCFD, ṁ = {s.MassFlow.KilogramsPerSecond:F3} kg/s");
        _output.WriteLine($"D = {s.InternalDiameter.Inches:F4} in, Z = {s.AverageCompressibilityFactor:F4}, s = {s.ElevationParameter:E4}, Le = {s.EquivalentLength.Miles:F4} mi");
        _output.WriteLine($"F = {s.TransmissionFactor?.ToString("F4") ?? "—"}, Re = {s.ReynoldsNumber?.ToString("E4") ?? "—"}");

        foreach (string w in s.Warnings)
        {
            _output.WriteLine($"WARNING: {w}");
        }
    }
}
