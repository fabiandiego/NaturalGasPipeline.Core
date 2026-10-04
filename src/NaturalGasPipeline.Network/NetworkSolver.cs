using NaturalGasPipeline.Core.Common;
using NaturalGasPipeline.Core.Flow;
using NaturalGasPipeline.Core.Numerics;
using NaturalGasPipeline.Core.Pipeline;
using UnitsNet;

namespace NaturalGasPipeline.Network;

/// <summary>
/// Steady-state solution of a gas network by the nodal method:
/// unknowns are the pressures of the fixed-flow nodes, equations are the
/// mass balances Σ inflow − Σ outflow − withdrawal = 0 at those nodes.
/// Solved by Newton's method with a central-difference Jacobian (each pipe
/// flow depends only on its two end pressures) and a backtracking line
/// search on the largest imbalance.
///
/// Pipe flows come from each pipe's <see cref="ISegmentModel"/>, so any
/// segment calculation can be used. Flow reversal is handled by solving the
/// segment in the other direction (elevation change negated).
/// Friction models that are undefined in the laminar–turbulent transition
/// (Colebrook, Haaland, Swamee-Jain) can fail on nearly idle pipes of a
/// loop; Churchill covers every regime.
/// </summary>
public sealed class NetworkSolver
{
    private readonly double _relativeMassTolerance;
    private readonly int _maxIterations;

    /// <param name="relativeMassTolerance">Converged when every nodal imbalance is below this fraction of the total withdrawal (or of 1 kg/s if larger).</param>
    /// <param name="maxIterations">Newton iteration limit; exceeding it throws.</param>
    public NetworkSolver(double relativeMassTolerance = 1e-9, int maxIterations = 100)
    {
        if (!double.IsFinite(relativeMassTolerance) || relativeMassTolerance <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(relativeMassTolerance), relativeMassTolerance, "Tolerance must be positive.");
        }

        if (maxIterations < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxIterations), maxIterations, "At least one iteration is required.");
        }

        _relativeMassTolerance = relativeMassTolerance;
        _maxIterations = maxIterations;
    }

    /// <param name="network">The network.</param>
    /// <param name="initialPressures">Optional starting pressures of fixed-flow nodes; the default is 90 % of the mean fixed pressure (a start with zero pressure differences gives zero flows, where turbulent flow laws have an infinite slope).</param>
    public NetworkSolution Solve(GasNetwork network, IReadOnlyDictionary<string, Pressure>? initialPressures = null)
    {
        Guard.NotNull(network);

        var index = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < network.Nodes.Count; i++)
        {
            index[network.Nodes[i].Id] = i;
        }

        int n = network.Nodes.Count;
        var pressure = new double[n];
        var unknown = new List<int>();

        double meanFixed = network.Nodes
            .Where(x => x.Kind == NodeKind.FixedPressure)
            .Average(x => x.FixedPressure!.Value.Pascals);

        for (int i = 0; i < n; i++)
        {
            NetworkNode node = network.Nodes[i];

            if (node.Kind == NodeKind.FixedPressure)
            {
                pressure[i] = node.FixedPressure!.Value.Pascals;
            }
            else
            {
                unknown.Add(i);
                pressure[i] = initialPressures is not null && initialPressures.TryGetValue(node.Id, out Pressure guess)
                    ? guess.Pascals
                    : 0.9 * meanFixed;
            }
        }

        var position = new int[n];
        Array.Fill(position, -1);
        for (int k = 0; k < unknown.Count; k++)
        {
            position[unknown[k]] = k;
        }

        var edges = network.Pipes
            .Select(p => (Pipe: p, From: index[p.From], To: index[p.To]))
            .ToArray();

        double scale = System.Math.Max(1.0, network.Nodes.Sum(x => System.Math.Abs(x.Withdrawal.KilogramsPerSecond)));
        double tolerance = _relativeMassTolerance * scale;

        double[] residual = Residual(network, edges, pressure, unknown);
        double norm = MaxAbs(residual);
        int iteration = 0;

        while (norm > tolerance)
        {
            if (++iteration > _maxIterations)
            {
                throw new InvalidOperationException(
                    $"Network did not converge in {_maxIterations} iterations (largest imbalance {norm:E3} kg/s).");
            }

            double[,] jacobian = Jacobian(edges, pressure, unknown.Count, position);
            double[] step = DenseLinearSolver.Solve(jacobian, residual.Select(r => -r).ToArray());

            // Backtracking: keep pressures positive and reduce the largest imbalance.
            double lambda = 1.0;
            double[] trial = (double[])pressure.Clone();
            double[] trialResidual;

            while (true)
            {
                bool positive = true;

                for (int k = 0; k < unknown.Count; k++)
                {
                    trial[unknown[k]] = pressure[unknown[k]] + lambda * step[k];
                    positive &= trial[unknown[k]] > 0.0;
                }

                if (positive)
                {
                    trialResidual = Residual(network, edges, trial, unknown);
                    double trialNorm = MaxAbs(trialResidual);

                    if (trialNorm < (1.0 - 1e-4 * lambda) * norm || lambda < 1e-6)
                    {
                        norm = trialNorm;
                        break;
                    }
                }

                lambda /= 2.0;

                if (lambda < 1e-10)
                {
                    throw new InvalidOperationException("Network line search failed: no step reduces the nodal imbalance.");
                }
            }

            pressure = trial;
            residual = trialResidual;
        }

        return BuildSolution(network, edges, pressure, iteration, norm);
    }

    // ------------------------------------------------------------ internals

    private static double[] Residual(
        GasNetwork network,
        (NetworkPipe Pipe, int From, int To)[] edges,
        double[] pressure,
        List<int> unknown)
    {
        var balance = new double[network.Nodes.Count];

        foreach ((NetworkPipe pipe, int fromIndex, int toIndex) in edges)
        {
            double q = Flow(pipe, pressure[fromIndex], pressure[toIndex], out _);
            balance[fromIndex] -= q;
            balance[toIndex] += q;
        }

        // inflow − outflow − withdrawal
        return unknown
            .Select(i => balance[i] - network.Nodes[i].Withdrawal.KilogramsPerSecond)
            .ToArray();
    }

    private static double[,] Jacobian(
        (NetworkPipe Pipe, int From, int To)[] edges,
        double[] pressure,
        int size,
        int[] position)
    {
        var j = new double[size, size];

        foreach ((NetworkPipe pipe, int fromIndex, int toIndex) in edges)
        {
            double pf = pressure[fromIndex];
            double pt = pressure[toIndex];
            double hf = 1e-6 * pf;
            double ht = 1e-6 * pt;

            double dqdpf = (Flow(pipe, pf + hf, pt, out _) - Flow(pipe, pf - hf, pt, out _)) / (2.0 * hf);
            double dqdpt = (Flow(pipe, pf, pt + ht, out _) - Flow(pipe, pf, pt - ht, out _)) / (2.0 * ht);

            int rf = position[fromIndex];
            int rt = position[toIndex];

            // Residual at "from" contains −q, at "to" contains +q.
            if (rf >= 0)
            {
                j[rf, rf] -= dqdpf;
                if (rt >= 0)
                {
                    j[rf, rt] -= dqdpt;
                }
            }

            if (rt >= 0)
            {
                j[rt, rt] += dqdpt;
                if (rf >= 0)
                {
                    j[rt, rf] += dqdpf;
                }
            }
        }

        return j;
    }

    /// <summary>Signed mass flow From → To (kg/s); the segment is reversed for negative flow.</summary>
    private static double Flow(NetworkPipe pipe, double pFrom, double pTo, out SegmentResult? result)
    {
        PipeSegment reversed = new(
            pipe.Segment.InternalDiameter,
            pipe.Segment.Length,
            pipe.Segment.AbsoluteRoughness,
            -pipe.Segment.ElevationChange);

        bool forwardFirst = pFrom >= pTo;

        for (int attempt = 0; attempt < 2; attempt++)
        {
            bool forward = forwardFirst ^ (attempt == 1);

            try
            {
                result = forward
                    ? pipe.Model.SolveMassFlow(pipe.Segment, pipe.Temperature, Pressure.FromPascals(pFrom), Pressure.FromPascals(pTo))
                    : pipe.Model.SolveMassFlow(reversed, pipe.Temperature, Pressure.FromPascals(pTo), Pressure.FromPascals(pFrom));

                double q = result.MassFlow.KilogramsPerSecond;
                return forward ? q : -q;
            }
            catch (NoForwardFlowException)
            {
                // Try the other direction.
            }
        }

        // P_from² = e^s·P_to² exactly: no flow.
        result = null;
        return 0.0;
    }

    private static double MaxAbs(double[] values) => values.Length == 0 ? 0.0 : values.Max(v => System.Math.Abs(v));

    private static NetworkSolution BuildSolution(
        GasNetwork network,
        (NetworkPipe Pipe, int From, int To)[] edges,
        double[] pressure,
        int iterations,
        double largestImbalance)
    {
        var balance = new double[network.Nodes.Count];
        var pipes = new List<NetworkPipeResult>(edges.Length);
        var warnings = new List<string>();

        foreach ((NetworkPipe pipe, int fromIndex, int toIndex) in edges)
        {
            double q = Flow(pipe, pressure[fromIndex], pressure[toIndex], out SegmentResult? segment);
            balance[fromIndex] -= q;
            balance[toIndex] += q;

            pipes.Add(new NetworkPipeResult(pipe.Id, pipe.From, pipe.To, MassFlow.FromKilogramsPerSecond(q), segment));

            if (segment is not null)
            {
                warnings.AddRange(segment.Warnings.Select(w => $"{pipe.Id}: {w}"));
            }
        }

        var nodes = network.Nodes
            .Select((node, i) => new NetworkNodeResult(
                node.Id,
                node.Kind,
                Pressure.FromPascals(pressure[i]),
                // Net withdrawal: for fixed-pressure nodes this is minus the supply.
                MassFlow.FromKilogramsPerSecond(node.Kind == NodeKind.FixedPressure
                    ? balance[i]
                    : node.Withdrawal.KilogramsPerSecond)))
            .ToList();

        return new NetworkSolution
        {
            Nodes = nodes,
            Pipes = pipes,
            Iterations = iterations,
            LargestImbalance = MassFlow.FromKilogramsPerSecond(largestImbalance),
            Warnings = warnings
        };
    }
}

/// <summary>Solved node: pressure and net withdrawal (negative = the node supplies gas).</summary>
public readonly record struct NetworkNodeResult(string Id, NodeKind Kind, Pressure Pressure, MassFlow NetWithdrawal);

/// <summary>
/// Solved pipe. <see cref="MassFlow"/> is positive From → To. The segment
/// result is in the actual flow direction (null when the flow is exactly zero).
/// </summary>
public sealed record NetworkPipeResult(string Id, string From, string To, MassFlow MassFlow, SegmentResult? Segment);

public sealed record NetworkSolution
{
    public required IReadOnlyList<NetworkNodeResult> Nodes { get; init; }

    public required IReadOnlyList<NetworkPipeResult> Pipes { get; init; }

    public required int Iterations { get; init; }

    /// <summary>Largest nodal mass imbalance at convergence.</summary>
    public required MassFlow LargestImbalance { get; init; }

    public required IReadOnlyList<string> Warnings { get; init; }

    public NetworkNodeResult Node(string id) => Nodes.Single(n => n.Id == id);

    public NetworkPipeResult Pipe(string id) => Pipes.Single(p => p.Id == id);
}
