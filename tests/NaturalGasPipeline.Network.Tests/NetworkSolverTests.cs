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
/// Network (nodal method) solutions.
///
/// No published looped gas-network example with complete data was found
/// (CED course P06-001 Example 13 is descriptive only), so the solver is
/// checked three ways:
/// 1. against an independent Python implementation (same pipe physics:
///    isothermal, Z/μ at P_avg, Churchill friction, typical natural gas with
///    Peng-Robinson and Lee-Gonzalez-Eakin; nodal equations solved with
///    scipy.optimize.fsolve, xtol 1e-14), 2026-10-04;
/// 2. against analytic results (parallel pipes with a power-law equation);
/// 3. against direct segment calculations on a tree.
/// </summary>
public class NetworkSolverTests
{
    private readonly ITestOutputHelper _output;

    public NetworkSolverTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static readonly GasMixture Gas = NaturalGasMixtures.TypicalNaturalGas;
    private static readonly Temperature T = Temperature.FromKelvins(288.15);

    private static readonly ISegmentModel Churchill =
        new IsothermalSegmentModel(
            Gas.CalculateMolarMass(),
            CompressibilityModelFactory.Create(CompressibilityModelType.PengRobinson, Gas),
            new LeeGonzalezEakinViscosityModel(),
            new ChurchillModel());

    private static readonly Dictionary<string, double> Elevation = new()
    {
        ["S"] = 0.0, ["A"] = 20.0, ["B"] = 60.0, ["C"] = 40.0, ["D"] = 90.0, ["E"] = 90.0
    };

    private static NetworkPipe Pipe(string from, string to, double diameter, double lengthKm) =>
        new($"{from}-{to}", from, to,
            new PipeSegment(Length.FromMeters(diameter), Length.FromKilometers(lengthKm), Length.FromMillimeters(0.045),
                Length.FromMeters(Elevation[to] - Elevation[from])),
            T, Churchill);

    private static List<NetworkPipe> LoopedPipes() => new()
    {
        Pipe("S", "A", 0.6, 30),
        Pipe("A", "B", 0.4, 20),
        Pipe("A", "C", 0.5, 25),
        Pipe("C", "B", 0.3, 15),
        Pipe("C", "D", 0.4, 20),
        Pipe("B", "D", 0.3, 30),
    };

    private static List<NetworkNode> Loads() => new()
    {
        NetworkNode.WithFlow("A", MassFlow.FromKilogramsPerSecond(20.0)),
        NetworkNode.WithFlow("B", MassFlow.FromKilogramsPerSecond(30.0)),
        NetworkNode.WithFlow("C", MassFlow.FromKilogramsPerSecond(25.0)),
        NetworkNode.WithFlow("D", MassFlow.FromKilogramsPerSecond(40.0)),
    };

    [Fact]
    public void LoopedNetwork_ShouldMatchIndependentImplementation()
    {
        var nodes = Loads();
        nodes.Add(NetworkNode.WithPressure("S", Pressure.FromMegapascals(7.0)));

        NetworkSolution s = new NetworkSolver().Solve(new GasNetwork(nodes, LoopedPipes()));
        Print(s);

        AssertPressure(s, "A", 6185345.515954268);
        AssertPressure(s, "B", 5646664.146234146);
        AssertPressure(s, "C", 5677201.528114967);
        AssertPressure(s, "D", 5301985.725710372);

        AssertFlow(s, "S-A", 115.0);
        AssertFlow(s, "A-B", 37.01110543776555);
        AssertFlow(s, "A-C", 57.988894562234556);
        AssertFlow(s, "C-B", 3.8480049837561814);
        AssertFlow(s, "C-D", 29.140889578478337);
        AssertFlow(s, "B-D", 10.859110421521644);

        Assert.Equal(-115.0, s.Node("S").NetWithdrawal.KilogramsPerSecond, 1e-6);
    }

    /// <summary>Two supplies: E (6 MPa) feeds D, so pipe D-E runs backwards.</summary>
    [Fact]
    public void TwoSupplies_ShouldMatchIndependentImplementationIncludingReversedFlow()
    {
        var nodes = Loads();
        nodes.Add(NetworkNode.WithPressure("S", Pressure.FromMegapascals(7.0)));
        nodes.Add(NetworkNode.WithPressure("E", Pressure.FromMegapascals(6.0)));

        var pipes = LoopedPipes();
        pipes.Add(Pipe("D", "E", 0.3, 10));

        NetworkSolution s = new NetworkSolver().Solve(new GasNetwork(nodes, pipes));
        Print(s);

        AssertPressure(s, "A", 6372405.572949207);
        AssertPressure(s, "B", 5979099.545251472);
        AssertPressure(s, "C", 6017889.130450464);
        AssertPressure(s, "D", 5844226.574239246);

        AssertFlow(s, "S-A", 101.45399589781398);
        AssertFlow(s, "C-B", 4.6701200416339015);
        AssertFlow(s, "D-E", -13.54600410218596);

        Assert.Equal(-101.45399589781398, s.Node("S").NetWithdrawal.KilogramsPerSecond, 1e-5);
        Assert.Equal(-13.54600410218596, s.Node("E").NetWithdrawal.KilogramsPerSecond, 1e-5);

        // The reversed pipe reports its segment in the actual flow direction.
        SegmentResult reversed = s.Pipe("D-E").Segment!;
        Assert.Equal(6.0e6, reversed.InletPressure.Pascals, 1e-6);
        Assert.Equal(-s.Pipe("D-E").MassFlow.KilogramsPerSecond, reversed.MassFlow.KilogramsPerSecond, 1e-9);
    }

    /// <summary>
    /// Two parallel pipes, Weymouth with constant Z, same length and end
    /// pressures: Q ∝ D^2.667 exactly, so the split is (12/8)^2.667.
    /// </summary>
    [Fact]
    public void ParallelPipes_ShouldSplitTheFlowAnalytically()
    {
        var baseConditions = new StandardFlowConditions
        {
            Temperature = Temperature.FromDegreesRankine(520.0),
            Pressure = Pressure.FromPoundsForcePerSquareInch(14.7),
            CompressibilityFactor = 1.0
        };

        var weymouth = new FlowEquationSegmentModel(
            GasFlowEquations.Weymouth(0.95),
            new FlowEquationGas(0.6, DryAirMolarMass.Gpa2145_09, new ConstantCompressibilityModel(0.88)),
            baseConditions);

        var tf = Temperature.FromDegreesRankine(530.0);
        var length = Length.FromMiles(20.0);

        var network = new GasNetwork(
            new[]
            {
                NetworkNode.WithPressure("In", Pressure.FromPoundsForcePerSquareInch(1000.0)),
                NetworkNode.WithFlow("Out", MassFlow.FromKilogramsPerSecond(40.0))
            },
            new[]
            {
                new NetworkPipe("Big", "In", "Out", new PipeSegment(Length.FromInches(12.0), length, Length.Zero, Length.Zero), tf, weymouth),
                new NetworkPipe("Small", "In", "Out", new PipeSegment(Length.FromInches(8.0), length, Length.Zero, Length.Zero), tf, weymouth)
            });

        NetworkSolution s = new NetworkSolver().Solve(network);
        Print(s);

        double big = s.Pipe("Big").MassFlow.KilogramsPerSecond;
        double small = s.Pipe("Small").MassFlow.KilogramsPerSecond;

        Assert.Equal(System.Math.Pow(1.5, 2.667), big / small, 1e-8);
        Assert.Equal(40.0, big + small, 1e-7);
    }

    /// <summary>A tree is solved exactly by marching: the network must agree.</summary>
    [Fact]
    public void TreeNetwork_ShouldMatchDirectSegmentCalculations()
    {
        var first = new PipeSegment(Length.FromMeters(0.5), Length.FromKilometers(40.0), Length.FromMillimeters(0.045), Length.FromMeters(80.0));
        var second = new PipeSegment(Length.FromMeters(0.4), Length.FromKilometers(30.0), Length.FromMillimeters(0.045), Length.FromMeters(-50.0));

        var network = new GasNetwork(
            new[]
            {
                NetworkNode.WithPressure("S", Pressure.FromMegapascals(7.0)),
                NetworkNode.WithFlow("A", MassFlow.FromKilogramsPerSecond(20.0)),
                NetworkNode.WithFlow("B", MassFlow.FromKilogramsPerSecond(45.0))
            },
            new[]
            {
                new NetworkPipe("S-A", "S", "A", first, T, Churchill),
                new NetworkPipe("A-B", "A", "B", second, T, Churchill)
            });

        NetworkSolution s = new NetworkSolver().Solve(network);
        Print(s);

        Pressure pA = Churchill.SolveOutletPressure(first, T, Pressure.FromMegapascals(7.0), MassFlow.FromKilogramsPerSecond(65.0)).OutletPressure;
        Pressure pB = Churchill.SolveOutletPressure(second, T, pA, MassFlow.FromKilogramsPerSecond(45.0)).OutletPressure;

        Assert.Equal(1.0, s.Node("A").Pressure.Pascals / pA.Pascals, 1e-8);
        Assert.Equal(1.0, s.Node("B").Pressure.Pascals / pB.Pascals, 1e-8);
    }

    [Fact]
    public void InvalidNetworks_ShouldThrow()
    {
        var s = NetworkNode.WithPressure("S", Pressure.FromMegapascals(7.0));
        var a = NetworkNode.WithFlow("A", MassFlow.FromKilogramsPerSecond(10.0));
        var b = NetworkNode.Junction("B");
        var pipe = Pipe("S", "A", 0.5, 10);

        // Duplicated node, no fixed pressure, isolated node, unknown node.
        Assert.Throws<ArgumentException>(() => new GasNetwork(new[] { s, s }, new[] { pipe }));
        Assert.Throws<ArgumentException>(() => new GasNetwork(new[] { a, b }, new[] { new NetworkPipe("A-B", "A", "B", pipe.Segment, T, Churchill) }));
        Assert.Throws<ArgumentException>(() => new GasNetwork(new[] { s, a, b }, new[] { pipe }));
        Assert.Throws<ArgumentException>(() => new GasNetwork(new[] { s, a }, new[] { new NetworkPipe("S-X", "S", "X", pipe.Segment, T, Churchill) }));
    }

    // ------------------------------------------------------------ helpers

    private static void AssertPressure(NetworkSolution s, string node, double expectedPascals) =>
        Assert.Equal(1.0, s.Node(node).Pressure.Pascals / expectedPascals, 1e-7);

    private static void AssertFlow(NetworkSolution s, string pipe, double expectedKgPerS) =>
        Assert.Equal(expectedKgPerS, s.Pipe(pipe).MassFlow.KilogramsPerSecond, 1e-5);

    private void Print(NetworkSolution s)
    {
        _output.WriteLine($"Iterations: {s.Iterations}, largest imbalance {s.LargestImbalance.KilogramsPerSecond:E2} kg/s");

        foreach (NetworkNodeResult n in s.Nodes)
        {
            _output.WriteLine($"Node {n.Id,-4} {n.Kind,-13} P = {n.Pressure.Megapascals:F6} MPa, withdrawal = {n.NetWithdrawal.KilogramsPerSecond:F4} kg/s");
        }

        foreach (NetworkPipeResult p in s.Pipes)
        {
            _output.WriteLine($"Pipe {p.Id,-6} ṁ = {p.MassFlow.KilogramsPerSecond:F5} kg/s");
        }

        foreach (string w in s.Warnings)
        {
            _output.WriteLine($"WARNING: {w}");
        }
    }
}
