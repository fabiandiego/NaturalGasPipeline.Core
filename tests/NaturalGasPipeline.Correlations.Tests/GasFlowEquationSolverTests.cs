using NaturalGasPipeline.Core.Flow;
using NaturalGasPipeline.Core.Pipeline;
using NaturalGasPipeline.Hydraulics;
using NaturalGasPipeline.Thermodynamics;
using UnitsNet;
using Xunit;
using Xunit.Abstractions;
using static NaturalGasPipeline.Correlations.Tests.GasFlowEquationValidationTests;

namespace NaturalGasPipeline.Correlations.Tests;

/// <summary>
/// Solver behaviour: consistency between the four solve modes, with the
/// SI solver of the Hydraulics module, base-condition handling and warnings.
/// </summary>
public class GasFlowEquationSolverTests
{
    private readonly ITestOutputHelper _output;

    public GasFlowEquationSolverTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public static IEnumerable<object[]> Equations()
    {
        yield return new object[] { "Weymouth" };
        yield return new object[] { "PanhandleA" };
        yield return new object[] { "PanhandleB" };
        yield return new object[] { "GeneralFlowColebrook" };
        yield return new object[] { "AgaDf095" };
    }

    private static IGasFlowEquation Create(string name) => name switch
    {
        "Weymouth" => GasFlowEquations.Weymouth(0.95),
        "PanhandleA" => GasFlowEquations.PanhandleA(0.95),
        "PanhandleB" => GasFlowEquations.PanhandleB(0.95),
        "GeneralFlowColebrook" => new GeneralFlowEquation(new ColebrookWhiteModel()),
        _ => new GeneralFlowEquation(new AgaTransmissionFactorModel(0.95))
    };

    /// <summary>Typical gas with a real Z model (DAK via Sutton) and LGE viscosity, 300 m climb.</summary>
    private static GasFlowEquationSolver RealGasSolver(IGasFlowEquation equation) =>
        new(equation,
            new FlowEquationGas(
                0.65,
                DryAirMolarMass.Gpa2145_09,
                CompressibilityModelFactory.CreateFromSpecificGravity(
                    CorrespondingStatesModelType.DranchukAbouKassem, 0.65, new SuttonPseudoCriticalCorrelation()),
                new LeeGonzalezEakinViscosityModel()),
            Base520());

    private static readonly PipeSegment Line =
        new(Length.FromInches(23.0), Length.FromKilometers(60.0), Length.FromMillimeters(0.0457), Length.FromMeters(300.0));

    private static readonly Temperature Flowing = Temperature.FromKelvins(293.15);

    [Theory]
    [MemberData(nameof(Equations))]
    public void SolveModes_ShouldBeMutuallyConsistent(string name)
    {
        GasFlowEquationSolver solver = RealGasSolver(Create(name));
        Pressure p1 = Pressure.FromMegapascals(7.0);
        StandardVolumetricFlow q = Mmscfd(300.0);

        FlowEquationSolution outlet = solver.SolveOutletPressure(Line, Flowing, p1, q);
        FlowEquationSolution inlet = solver.SolveInletPressure(Line, Flowing, outlet.OutletPressure, q);
        FlowEquationSolution flow = solver.SolveFlow(Line, Flowing, p1, outlet.OutletPressure);
        FlowEquationSolution diameter = solver.SolveInternalDiameter(
            Line.Length, Line.AbsoluteRoughness, Line.ElevationChange, Flowing, p1, outlet.OutletPressure, q);

        _output.WriteLine($"{outlet.Equation}: P2 = {outlet.OutletPressure.Megapascals:F6} MPa, Z = {outlet.AverageCompressibilityFactor:F5}, s = {outlet.ElevationParameter:E3}, iterations {outlet.Iterations}");
        _output.WriteLine($"P1 back = {inlet.InletPressure.Megapascals:F9} MPa, Q back = {flow.StandardFlow.Value.CubicMetersPerSecond:F6} m³/s (given {q.Value.CubicMetersPerSecond:F6}), D back = {diameter.InternalDiameter.Inches:F8} in");

        foreach (string w in outlet.Warnings)
        {
            _output.WriteLine($"WARNING: {w}");
        }

        Assert.Equal(1.0, inlet.InletPressure.Pascals / p1.Pascals, 1e-8);
        Assert.Equal(1.0, flow.StandardFlow.Value.CubicMetersPerSecond / q.Value.CubicMetersPerSecond, 1e-8);
        Assert.Equal(1.0, diameter.InternalDiameter.Meters / Line.InternalDiameter.Meters, 1e-8);
        Assert.True(outlet.OutletPressure < p1);
    }

    /// <summary>
    /// The field-unit General Flow Equation (constant 38.77) and the SI
    /// isothermal solver are the same physics. With identical Z, μ and
    /// Colebrook they differ only by the rounding of 38.77 (exact value
    /// 38.784 for M_air = 28.9625 g/mol): ΔP differs by 0.074 %
    /// (independent Python check, 2026-10-04).
    /// </summary>
    [Fact]
    public void GeneralFlow_ShouldAgreeWithTheSiIsothermalSolver()
    {
        var z = new ConstantCompressibilityModel(0.85);
        var mu = new ConstantViscosityModel(DynamicViscosity.FromPascalSeconds(1.1e-5));
        var segment = new PipeSegment(Length.FromInches(17.5), Length.FromMiles(20.0), Length.FromInches(0.0007), Length.Zero);
        var t = Temperature.FromKelvins(288.15);
        var p1 = Pressure.FromMegapascals(7.0);

        var baseConditions = new StandardFlowConditions
        {
            Temperature = Temperature.FromDegreesRankine(520.0),
            Pressure = Pressure.FromPoundsForcePerSquareInch(14.696),
            CompressibilityFactor = 1.0
        };

        var q = new StandardVolumetricFlow(
            VolumeFlow.FromCubicMetersPerSecond(150e6 * 0.028316846592 / 86400.0), baseConditions);

        var gas = new FlowEquationGas(0.6, DryAirMolarMass.Gpa2145_09, z, mu);

        FlowEquationSolution field = new GasFlowEquationSolver(
                new GeneralFlowEquation(new ColebrookWhiteModel()), gas, baseConditions)
            .SolveOutletPressure(segment, t, p1, q);

        GasFlowSolution si = new IsothermalGasFlowSolver(gas.MolarMass, z, mu)
            .SolveOutletPressure(segment, t, p1, field.MassFlow);

        double deviation = field.PressureDrop.Pascals / si.PressureDrop.Pascals - 1.0;

        _output.WriteLine($"Field General Flow: P2 = {field.OutletPressure.Pascals:F1} Pa, F = {field.TransmissionFactor:F6}");
        _output.WriteLine($"SI solver:          P2 = {si.OutletPressure.Pascals:F1} Pa, F = {FrictionFactorModelExtensions.TransmissionFactorFromDarcy(si.DarcyFrictionFactor):F6}");
        _output.WriteLine($"ΔP deviation: {deviation * 100:+0.0000;-0.0000} %");

        Assert.Equal(6646570.352739689, field.OutletPressure.Pascals, 1e-2);
        Assert.InRange(deviation, 0.0005, 0.001);
    }

    [Fact]
    public void FlowAtOtherBaseConditions_ShouldBeConvertedThroughMassFlow()
    {
        GasFlowEquationSolver solver = RealGasSolver(GasFlowEquations.PanhandleB(0.95));
        Pressure p1 = Pressure.FromMegapascals(7.0);

        StandardVolumetricFlow at520 = Mmscfd(300.0);
        MolarMass m = MolarMass.FromKilogramsPerMole(0.65 * DryAirMolarMass.Gpa2145_09.KilogramsPerMole);

        var iso = new StandardFlowConditions
        {
            Temperature = Temperature.FromDegreesCelsius(15.0),
            Pressure = Pressure.FromKilopascals(101.325),
            CompressibilityFactor = 0.998
        };

        StandardVolumetricFlow atIso = FlowRateConverter.ConvertMassToStandardVolume(
            FlowRateConverter.ConvertStandardVolumeToMass(at520, m), m, iso);

        FlowEquationSolution a = solver.SolveOutletPressure(Line, Flowing, p1, at520);
        FlowEquationSolution b = solver.SolveOutletPressure(Line, Flowing, p1, atIso);

        _output.WriteLine($"Q = {at520.Value.CubicMetersPerSecond:F5} m³/s at 520 °R / 14.7 psia ≡ {atIso.Value.CubicMetersPerSecond:F5} m³/s at 15 °C / 101.325 kPa, Z = 0.998");

        Assert.Equal(a.OutletPressure.Pascals, b.OutletPressure.Pascals, 1e-3);
        Assert.Equal(a.MassFlow.KilogramsPerSecond, b.MassFlow.KilogramsPerSecond, 1e-9);
    }

    [Fact]
    public void ApplicabilityWarnings_ShouldBeReported()
    {
        // Weymouth above 12 in; Panhandle below 12 in; efficiency above 1.
        FlowEquationSolution weymouth = Solver(GasFlowEquations.Weymouth(0.95), 0.6, 0.85).SolveOutletPressure(
            Segment(17.5, Length.FromMiles(20.0)), Temperature.FromDegreesRankine(530.0), Psia(1014.7), Mmscfd(150.0));

        FlowEquationSolution panhandle = Solver(GasFlowEquations.PanhandleB(1.05), 0.85, 0.67).SolveOutletPressure(
            Segment(6.0, Length.FromFeet(7000.0)), Temperature.FromDegreesRankine(540.0), Psia(915.0), Mmscfd(23.0));

        foreach (string w in weymouth.Warnings.Concat(panhandle.Warnings))
        {
            _output.WriteLine(w);
        }

        Assert.Contains(weymouth.Warnings, w => w.Contains("above the recommended"));
        Assert.Contains(panhandle.Warnings, w => w.Contains("below the recommended"));
        Assert.Contains(panhandle.Warnings, w => w.Contains("efficiency"));
    }

    [Fact]
    public void InvalidRequests_ShouldThrow()
    {
        GasFlowEquationSolver weymouth = Solver(GasFlowEquations.Weymouth(1.0), 0.6, 0.85);
        PipeSegment segment = Segment(12.0, Length.FromMiles(10.0));
        var t = Temperature.FromDegreesRankine(530.0);

        // Outlet above inlet on a flat line: no forward flow.
        Assert.Throws<InvalidOperationException>(() => weymouth.SolveFlow(segment, t, Psia(800.0), Psia(900.0)));

        // More flow than the line can carry from this inlet pressure.
        Assert.Throws<FlowCapacityExceededException>(() => weymouth.SolveOutletPressure(segment, t, Psia(800.0), Mmscfd(5000.0)));

        // General Flow needs a viscosity model for Re.
        var noViscosity = new GasFlowEquationSolver(
            new GeneralFlowEquation(new ColebrookWhiteModel()),
            new FlowEquationGas(0.6, DryAirMolarMass.Gpa2145_09, new ConstantCompressibilityModel(0.85)),
            Base520());

        Assert.Throws<InvalidOperationException>(() => noViscosity.SolveOutletPressure(segment, t, Psia(800.0), Mmscfd(50.0)));

        Assert.Throws<ArgumentOutOfRangeException>(() => GasFlowEquations.PanhandleA(0.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FlowEquationGas(0.0, DryAirMolarMass.Gpa2145_09, new ConstantCompressibilityModel(0.85)));
    }
}
