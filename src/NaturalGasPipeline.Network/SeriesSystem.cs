using NaturalGasPipeline.Core.Common;
using NaturalGasPipeline.Compression;
using NaturalGasPipeline.Core.Flow;
using NaturalGasPipeline.Core.Numerics;
using NaturalGasPipeline.Core.Pipeline;
using UnitsNet;

namespace NaturalGasPipeline.Network;

/// <summary>One element of a series system, between two nodes.</summary>
public abstract record SystemElement(string Name);

/// <summary>
/// An isothermal pipe segment at its own flowing temperature. The gas
/// leaves it at that temperature (heat exchange with the surroundings is
/// assumed complete); a warning is issued when the gas enters more than
/// <see cref="SeriesSystem.TemperatureMismatchWarning"/> away from it.
/// </summary>
public sealed record PipeElement(string Name, PipeSegment Segment, Temperature FlowingTemperature, ISegmentModel Model)
    : SystemElement(Name);

/// <summary>
/// A compressor station with a fixed overall pressure ratio. The suction
/// temperature is the temperature arriving from upstream.
/// </summary>
public sealed record CompressorElement(string Name, CompressorStation Station, double PressureRatio)
    : SystemElement(Name);

/// <summary>A cooler (e.g. station aftercooler): sets the outlet temperature, with an explicit pressure drop.</summary>
public sealed record CoolerElement(string Name, Temperature OutletTemperature, Pressure PressureDrop)
    : SystemElement(Name);

/// <summary>Pressure and temperature at a node (between elements).</summary>
public readonly record struct NodeState(string Name, Pressure Pressure, Temperature Temperature);

/// <summary>
/// Elements in series carrying one mass flow (no branches): pipe segments,
/// compressor stations and coolers. Solves
/// - forward:  inlet pressure and mass flow → every node;
/// - backward: outlet pressure and mass flow → every node (compressors at a
///             fixed ratio; temperatures do not depend on pressure in these
///             models, so they are known before the pressure march);
/// - flow:     inlet and outlet pressures → mass flow (Brent's method).
/// </summary>
public sealed class SeriesSystem
{
    /// <summary>Gas entering a pipe more than this away from its flowing temperature triggers a warning.</summary>
    public static readonly TemperatureDelta TemperatureMismatchWarning = TemperatureDelta.FromKelvins(5.0);

    private readonly IReadOnlyList<SystemElement> _elements;

    public SeriesSystem(IEnumerable<SystemElement> elements)
    {
        Guard.NotNull(elements);
        _elements = elements.ToList();

        if (_elements.Count == 0)
        {
            throw new ArgumentException("A system needs at least one element.", nameof(elements));
        }

        foreach (SystemElement e in _elements)
        {
            switch (e)
            {
                case null:
                    throw new ArgumentException("Elements cannot be null.", nameof(elements));
                case CompressorElement c when !double.IsFinite(c.PressureRatio) || c.PressureRatio < 1.0:
                    throw new ArgumentException($"{c.Name}: pressure ratio must be ≥ 1.", nameof(elements));
                case CoolerElement k when !double.IsFinite(k.PressureDrop.Pascals) || k.PressureDrop.Pascals < 0.0:
                    throw new ArgumentException($"{k.Name}: pressure drop must be ≥ 0.", nameof(elements));
                case PipeElement or CompressorElement or CoolerElement:
                    break;
                default:
                    throw new ArgumentException($"Unsupported element type {e.GetType().Name}.", nameof(elements));
            }
        }
    }

    public IReadOnlyList<SystemElement> Elements => _elements;

    public SeriesSolution SolveForward(Pressure inletPressure, Temperature inletTemperature, MassFlow massFlow)
    {
        RequirePositive(inletPressure.Pascals, nameof(inletPressure));
        IReadOnlyList<Temperature> temperatures = TemperatureProfile(inletTemperature);
        RequirePositive(massFlow.KilogramsPerSecond, nameof(massFlow));

        var pressures = new Pressure[_elements.Count + 1];
        var results = new object[_elements.Count];
        pressures[0] = inletPressure;

        for (int i = 0; i < _elements.Count; i++)
        {
            (pressures[i + 1], results[i]) = _elements[i] switch
            {
                PipeElement p => Outlet(p.Model.SolveOutletPressure(p.Segment, p.FlowingTemperature, pressures[i], massFlow)),
                CompressorElement c => Compress(c, pressures[i], temperatures[i], Pressure.FromPascals(pressures[i].Pascals * c.PressureRatio), massFlow),
                CoolerElement k => Cool(k, pressures[i] - k.PressureDrop),
                _ => throw new InvalidOperationException()
            };

            RequirePositive(pressures[i + 1].Pascals, $"pressure after {_elements[i].Name}");
        }

        return Build(pressures, temperatures, results, massFlow);
    }

    public SeriesSolution SolveBackward(Pressure outletPressure, Temperature inletTemperature, MassFlow massFlow)
    {
        RequirePositive(outletPressure.Pascals, nameof(outletPressure));
        IReadOnlyList<Temperature> temperatures = TemperatureProfile(inletTemperature);
        RequirePositive(massFlow.KilogramsPerSecond, nameof(massFlow));

        var pressures = new Pressure[_elements.Count + 1];
        var results = new object[_elements.Count];
        pressures[^1] = outletPressure;

        for (int i = _elements.Count - 1; i >= 0; i--)
        {
            (pressures[i], results[i]) = _elements[i] switch
            {
                PipeElement p => Inlet(p.Model.SolveInletPressure(p.Segment, p.FlowingTemperature, pressures[i + 1], massFlow)),
                CompressorElement c => Suction(c, pressures[i + 1], temperatures[i], massFlow),
                CoolerElement k => Cool(k, pressures[i + 1] + k.PressureDrop),
                _ => throw new InvalidOperationException()
            };
        }

        return Build(pressures, temperatures, results, massFlow);
    }

    /// <summary>
    /// Mass flow that delivers <paramref name="outletPressure"/> from
    /// <paramref name="inletPressure"/>. Flows the system cannot carry count
    /// as "too much flow" while bracketing.
    /// </summary>
    public SeriesSolution SolveMassFlow(
        Pressure inletPressure,
        Temperature inletTemperature,
        Pressure outletPressure,
        MassFlow initialGuess,
        double relativeTolerance = 1e-10)
    {
        double target = RequirePositive(outletPressure.Pascals, nameof(outletPressure));
        double guess = RequirePositive(initialGuess.KilogramsPerSecond, nameof(initialGuess));

        double Residual(double m)
        {
            try
            {
                return SolveForward(inletPressure, inletTemperature, MassFlow.FromKilogramsPerSecond(m))
                    .OutletPressure.Pascals - target;
            }
            catch (FlowCapacityExceededException)
            {
                return -target;
            }
        }

        // Bracket: residual > 0 (too little flow) at lo, < 0 at hi.
        double lo = guess;
        double hi = guess;
        int expansions = 0;

        while (Residual(lo) <= 0.0)
        {
            lo /= 2.0;
            Expand(ref expansions);
        }

        while (Residual(hi) >= 0.0)
        {
            hi *= 2.0;
            Expand(ref expansions);
        }

        double m = BrentRootSolver.FindRoot(Residual, lo, hi, relativeTolerance * hi);

        return SolveForward(inletPressure, inletTemperature, MassFlow.FromKilogramsPerSecond(m));

        static void Expand(ref int count)
        {
            if (++count > 60)
            {
                throw new InvalidOperationException(
                    "No mass flow delivers the requested outlet pressure (bracket search failed).");
            }
        }
    }

    // ------------------------------------------------------------ internals

    /// <summary>Temperature at every node; independent of pressure in these models.</summary>
    private IReadOnlyList<Temperature> TemperatureProfile(Temperature inletTemperature)
    {
        RequirePositive(inletTemperature.Kelvins, nameof(inletTemperature));

        var t = new Temperature[_elements.Count + 1];
        t[0] = inletTemperature;

        for (int i = 0; i < _elements.Count; i++)
        {
            t[i + 1] = _elements[i] switch
            {
                PipeElement p => p.FlowingTemperature,
                CompressorElement c => StationDischargeTemperature(c, t[i]),
                CoolerElement k => k.OutletTemperature,
                _ => throw new InvalidOperationException()
            };
        }

        return t;
    }

    // Discharge temperature depends only on suction temperature and ratio:
    // evaluate the station at a nominal pressure and unit flow.
    private static Temperature StationDischargeTemperature(CompressorElement c, Temperature suction) =>
        c.Station.Compress(
                Pressure.FromMegapascals(1.0), suction,
                Pressure.FromMegapascals(c.PressureRatio), MassFlow.FromKilogramsPerSecond(1.0))
            .DischargeTemperature;

    private static (Pressure, object) Outlet(SegmentResult r) => (r.OutletPressure, r);

    private static (Pressure, object) Inlet(SegmentResult r) => (r.InletPressure, r);

    private static (Pressure, object) Compress(CompressorElement c, Pressure suction, Temperature t, Pressure discharge, MassFlow m)
    {
        CompressorStationResult r = c.Station.Compress(suction, t, discharge, m);
        return (r.DischargePressure, r);
    }

    private static (Pressure, object) Suction(CompressorElement c, Pressure discharge, Temperature t, MassFlow m)
    {
        var suction = Pressure.FromPascals(discharge.Pascals / c.PressureRatio);
        CompressorStationResult r = c.Station.Compress(suction, t, discharge, m);
        return (suction, r);
    }

    private static (Pressure, object) Cool(CoolerElement k, Pressure other) => (other, k);

    private SeriesSolution Build(Pressure[] pressures, IReadOnlyList<Temperature> temperatures, object[] results, MassFlow massFlow)
    {
        var nodes = new List<NodeState>(pressures.Length)
        {
            new("Inlet", pressures[0], temperatures[0])
        };

        var warnings = new List<string>();

        for (int i = 0; i < _elements.Count; i++)
        {
            nodes.Add(new NodeState($"After {_elements[i].Name}", pressures[i + 1], temperatures[i + 1]));

            if (_elements[i] is PipeElement p)
            {
                double mismatch = System.Math.Abs(temperatures[i].Kelvins - p.FlowingTemperature.Kelvins);

                if (mismatch > TemperatureMismatchWarning.Kelvins)
                {
                    warnings.Add(
                        $"{p.Name}: gas enters at {temperatures[i].Kelvins:F1} K but the pipe is modelled isothermal at " +
                        $"{p.FlowingTemperature.Kelvins:F1} K; add a cooler or split the pipe.");
                }

                warnings.AddRange(((SegmentResult)results[i]).Warnings.Select(w => $"{p.Name}: {w}"));
            }
        }

        return new SeriesSolution
        {
            Nodes = nodes,
            ElementResults = results,
            MassFlow = massFlow,
            CompressorShaftPower = Power.FromWatts(results.OfType<CompressorStationResult>().Sum(r => r.ShaftPower.Watts)),
            Warnings = warnings
        };
    }

    private static double RequirePositive(double value, string name)
    {
        if (!double.IsFinite(value) || value <= 0.0)
        {
            throw new ArgumentOutOfRangeException(name, value, "Value must be finite and greater than zero.");
        }

        return value;
    }
}

/// <summary>Solved series system.</summary>
public sealed record SeriesSolution
{
    /// <summary>Inlet node, then the node after each element.</summary>
    public required IReadOnlyList<NodeState> Nodes { get; init; }

    /// <summary>
    /// One entry per element: <see cref="SegmentResult"/>, <see cref="CompressorStationResult"/>
    /// or the <see cref="CoolerElement"/> itself.
    /// </summary>
    public required IReadOnlyList<object> ElementResults { get; init; }

    public required MassFlow MassFlow { get; init; }

    public required Power CompressorShaftPower { get; init; }

    public required IReadOnlyList<string> Warnings { get; init; }

    public Pressure InletPressure => Nodes[0].Pressure;

    public Pressure OutletPressure => Nodes[^1].Pressure;
}
