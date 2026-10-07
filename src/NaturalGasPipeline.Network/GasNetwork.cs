using NaturalGasPipeline.Core.Common;
using NaturalGasPipeline.Core.Pipeline;
using UnitsNet;

namespace NaturalGasPipeline.Network;

/// <summary>Boundary condition of a node.</summary>
public enum NodeKind
{
    /// <summary>Pressure is known (supply or regulated delivery); its net flow is a result.</summary>
    FixedPressure,

    /// <summary>Net withdrawal is known (positive: gas leaves the network; negative: injection); pressure is a result.</summary>
    FixedFlow
}

/// <summary>A node of a gas network.</summary>
public sealed record NetworkNode
{
    private NetworkNode(string id, NodeKind kind, Pressure? pressure, MassFlow withdrawal)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("A node id is required.", nameof(id));
        }

        Id = id;
        Kind = kind;
        FixedPressure = pressure;
        Withdrawal = withdrawal;
    }

    public string Id { get; }

    public NodeKind Kind { get; }

    /// <summary>Set for <see cref="NodeKind.FixedPressure"/> nodes.</summary>
    public Pressure? FixedPressure { get; }

    /// <summary>Net mass flow leaving the network here (negative = injection). Zero for junctions.</summary>
    public MassFlow Withdrawal { get; }

    public static NetworkNode WithPressure(string id, Pressure pressure)
    {
        if (!double.IsFinite(pressure.Pascals) || pressure.Pascals <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(pressure), pressure.Pascals, "Pressure must be finite and positive (absolute).");
        }

        return new NetworkNode(id, NodeKind.FixedPressure, pressure, MassFlow.Zero);
    }

    /// <param name="id">Node id.</param>
    /// <param name="withdrawal">Positive: delivery (gas leaves); negative: injection; zero: junction.</param>
    public static NetworkNode WithFlow(string id, MassFlow withdrawal)
    {
        if (!double.IsFinite(withdrawal.KilogramsPerSecond))
        {
            throw new ArgumentOutOfRangeException(nameof(withdrawal), withdrawal.KilogramsPerSecond, "Flow must be finite.");
        }

        return new NetworkNode(id, NodeKind.FixedFlow, null, withdrawal);
    }

    public static NetworkNode Junction(string id) => WithFlow(id, MassFlow.Zero);
}

/// <summary>
/// A pipe between two nodes. <see cref="PipeSegment.ElevationChange"/> is
/// z(To) − z(From). Positive flow goes From → To; the solver reverses the
/// segment when the gas flows the other way.
/// </summary>
public sealed record NetworkPipe(string Id, string From, string To, PipeSegment Segment, Temperature Temperature, ISegmentModel Model);

/// <summary>
/// A steady-state gas network: nodes and pipes. Validated on construction:
/// unique ids, pipes between existing distinct nodes, at least one
/// fixed-pressure node, and every node connected to a fixed-pressure node.
/// </summary>
public sealed class GasNetwork
{
    public GasNetwork(IEnumerable<NetworkNode> nodes, IEnumerable<NetworkPipe> pipes)
    {
        Guard.NotNull(nodes);
        Guard.NotNull(pipes);

        Nodes = nodes.ToList();
        Pipes = pipes.ToList();

        var ids = new HashSet<string>(StringComparer.Ordinal);

        foreach (NetworkNode n in Nodes)
        {
            if (n is null || !ids.Add(n.Id))
            {
                throw new ArgumentException($"Null or duplicated node '{n?.Id}'.", nameof(nodes));
            }
        }

        var pipeIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (NetworkPipe p in Pipes)
        {
            if (p is null || string.IsNullOrWhiteSpace(p.Id) || !pipeIds.Add(p.Id))
            {
                throw new ArgumentException($"Null, unnamed or duplicated pipe '{p?.Id}'.", nameof(pipes));
            }

            if (!ids.Contains(p.From) || !ids.Contains(p.To) || p.From == p.To)
            {
                throw new ArgumentException($"Pipe '{p.Id}' must join two different existing nodes.", nameof(pipes));
            }

            Guard.NotNull(p.Segment);
            Guard.NotNull(p.Model);
        }

        if (!Nodes.Any(n => n.Kind == NodeKind.FixedPressure))
        {
            throw new ArgumentException("At least one node must have a fixed pressure.", nameof(nodes));
        }

        // Every node must reach a fixed-pressure node.
        var reached = new HashSet<string>(Nodes.Where(n => n.Kind == NodeKind.FixedPressure).Select(n => n.Id));
        var queue = new Queue<string>(reached);

        while (queue.Count > 0)
        {
            string current = queue.Dequeue();

            foreach (NetworkPipe p in Pipes)
            {
                string? other = p.From == current ? p.To : p.To == current ? p.From : null;

                if (other is not null && reached.Add(other))
                {
                    queue.Enqueue(other);
                }
            }
        }

        string[] isolated = Nodes.Select(n => n.Id).Where(id => !reached.Contains(id)).ToArray();

        if (isolated.Length > 0)
        {
            throw new ArgumentException(
                $"Nodes not connected to any fixed-pressure node: {string.Join(", ", isolated)}.", nameof(pipes));
        }
    }

    public IReadOnlyList<NetworkNode> Nodes { get; }

    public IReadOnlyList<NetworkPipe> Pipes { get; }
}
