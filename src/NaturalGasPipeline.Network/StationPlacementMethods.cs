using NaturalGasPipeline.Core.Common;
using NaturalGasPipeline.Core.Numerics;

namespace NaturalGasPipeline.Network;

/// <summary>
/// Method 1 — greedy maximum spacing (hydraulic marching).
///
/// 1. start at A with the inlet pressure;
/// 2. march along the route until the pressure falls to the minimum suction
///    pressure; place a station there, which restores the discharge pressure;
/// 3. repeat until B;
/// 4. the last station discharges exactly what B needs (backward march from
///    B). If the gas would arrive below the target without falling to the
///    minimum suction, one more station is placed where the backward profile
///    from B reaches the discharge pressure.
///
/// Gives the minimum number of stations for the pressure limits, with the
/// terrain. It does not minimise power or cost, and the last station can be
/// much smaller than the others (use <see cref="EqualRatioPlacement"/>).
/// See Edgar, Himmelblau &amp; Bickel (SPE J. 18(2), 1978) for the cost-optimal problem.
/// </summary>
public sealed class MaximumSpacingPlacement : IStationPlacementMethod
{
    private const int MaximumStations = 500;

    public string Name => "Maximum spacing (minimum number of stations)";

    public StationPlan Place(CompressorStationPlanner planner, StationPlanningProblem problem)
    {
        Guard.NotNull(planner);
        PlanningRun run = planner.Begin(problem);

        var stations = new List<(double X, double Suction, double Discharge)>();
        var sections = new List<List<SegmentResult>>();
        var warnings = new List<string>();

        double x = 0.0;
        double p = run.InletPressure;
        PlanningRun.March current;

        while (true)
        {
            current = run.Forward(x, p, run.MinimumSuction);

            if (!current.Stopped)
            {
                p = current.Pressure;
                break;
            }

            sections.Add(current.Segments);
            stations.Add((current.Position, current.Pressure, run.Discharge));

            if (stations.Count > MaximumStations)
            {
                throw new StationPlacementInfeasibleException(
                    $"More than {MaximumStations} stations would be needed: the flow is too large for this pipe and these pressures.");
            }

            x = current.Position;
            p = run.Discharge;
        }

        double sectionStart = stations.Count == 0 ? 0.0 : stations[^1].X;
        double sectionInlet = stations.Count == 0 ? run.InletPressure : run.Discharge;

        if (p >= run.DeliveryPressure)
        {
            if (stations.Count == 0)
            {
                if (p > run.DeliveryPressure)
                {
                    warnings.Add(
                        $"No compression needed: the gas arrives at {p / 1e6:F3} MPa, " +
                        $"{(p - run.DeliveryPressure) / 1e3:F1} kPa above the delivery pressure.");
                }

                sections.Add(current.Segments);
            }
            else
            {
                // Trim the last station: discharge only what B needs.
                PlanningRun.March backward = run.Backward(run.Route.Length, run.DeliveryPressure, sectionStart, stopAt: null);
                double required = backward.Pressure;
                (double sx, double suction, _) = stations[^1];

                if (required < suction)
                {
                    warnings.Add(
                        $"The last station is not needed for delivery (B would receive {run.DeliveryPressure / 1e6:F3} MPa without it); " +
                        "it is kept only to hold the minimum suction pressure along the way.");
                    required = suction;
                    current = run.Forward(sx, suction, stopAt: null);
                }
                else
                {
                    current = backward;
                }

                stations[^1] = (sx, suction, required);
                sections.Add(current.Segments);
            }
        }
        else
        {
            // Arrives below the target (but not below the minimum suction):
            // one more station, where the backward profile from B reaches the
            // discharge pressure.
            PlanningRun.March backward = run.Backward(run.Route.Length, run.DeliveryPressure, sectionStart, stopAt: run.Discharge);

            if (!backward.Stopped)
            {
                throw new StationPlacementInfeasibleException(
                    "Cannot deliver the target pressure: even the discharge pressure right after the last station is not enough.");
            }

            PlanningRun.March upToStation = run.Forward(sectionStart, sectionInlet, stopAt: null, end: backward.Position);
            double suction = upToStation.Pressure;

            if (suction < run.MinimumSuction)
            {
                warnings.Add(
                    $"Station at km {backward.Position / 1e3:F2}: suction {suction / 1e6:F3} MPa is below the minimum " +
                    $"suction pressure {run.MinimumSuction / 1e6:F3} MPa (terrain between the stations).");
            }

            sections.Add(upToStation.Segments);
            stations.Add((backward.Position, suction, run.Discharge));
            sections.Add(backward.Segments);
        }

        return run.Build(Name, stations, sections, warnings);
    }
}

/// <summary>
/// Method 2 — N stations with equal pressure ratios.
///
/// Every station discharges at the station discharge pressure P_d and takes
/// suction at a common pressure P_s, so every ratio is P_d / P_s:
/// 1. the last station sits where the backward profile from B (at the
///    delivery target) reaches P_d — its position does not depend on P_s;
/// 2. P_s is the root (Brent's method) of
///    g(P_s) = (position of the N-th point where the forward march from A,
///    restarting at P_d after each station, falls to P_s) − (position of 1).
///    Raising P_s brings the stations closer, so g decreases.
///
/// This is the usual industry heuristic. Equal ratios minimise compression
/// energy for stations with the same suction temperature, but not total
/// cost once station capital cost counts (Edgar, Himmelblau &amp; Bickel 1978).
/// N must be at least the count found by <see cref="MaximumSpacingPlacement"/>;
/// with fewer stations P_s would have to fall below the minimum suction.
/// </summary>
public sealed class EqualRatioPlacement : IStationPlacementMethod
{
    /// <param name="stationCount">Number of stations N (≥ 1).</param>
    public EqualRatioPlacement(int stationCount)
    {
        if (stationCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(stationCount), stationCount, "At least one station is required.");
        }

        StationCount = stationCount;
    }

    public int StationCount { get; }

    public string Name => $"Equal pressure ratio, {StationCount} station(s)";

    public StationPlan Place(CompressorStationPlanner planner, StationPlanningProblem problem)
    {
        Guard.NotNull(planner);
        PlanningRun run = planner.Begin(problem);
        int n = StationCount;

        // 1. Last station: where the backward profile from B reaches P_d.
        PlanningRun.March back = run.Backward(run.Route.Length, run.DeliveryPressure, 0.0, stopAt: run.Discharge);

        if (!back.Stopped)
        {
            throw new StationPlacementInfeasibleException(
                "No station is needed: a discharge pressure at A already delivers the target at B.");
        }

        double xLast = back.Position;

        // 2. Common suction pressure.
        double Gap(double suction)
        {
            double x = 0.0;
            double p = run.InletPressure;

            for (int k = 1; k <= n; k++)
            {
                PlanningRun.March m = run.Forward(x, p, suction, end: xLast);

                if (!m.Stopped)
                {
                    // Fewer than N stations needed before xLast: suction too low.
                    return xLast - x + 1.0;
                }

                if (k == n)
                {
                    return m.Position - xLast;
                }

                x = m.Position;
                p = run.Discharge;
            }

            throw new InvalidOperationException("Unreachable.");
        }

        double low = run.MinimumSuction;
        double high = run.Discharge * (1.0 - 1e-9);

        if (Gap(low) < 0.0)
        {
            throw new StationPlacementInfeasibleException(
                $"{n} station(s) cannot meet the criteria: the suction pressure would have to fall below the minimum " +
                $"{run.MinimumSuction / 1e6:F3} MPa. Use more stations.");
        }

        double ps = BrentRootSolver.FindRoot(Gap, low, high, absoluteTolerance: 1e-3);

        // 3. Final layout, built forward so that the plan is self-consistent.
        var stations = new List<(double X, double Suction, double Discharge)>(n);
        var sections = new List<List<SegmentResult>>(n + 1);
        double xs = 0.0;
        double pressure = run.InletPressure;

        for (int k = 1; k <= n; k++)
        {
            PlanningRun.March m = k < n
                ? run.Forward(xs, pressure, ps, end: xLast)
                : run.Forward(xs, pressure, stopAt: null, end: xLast);

            sections.Add(m.Segments);
            stations.Add((m.Position, m.Pressure, run.Discharge));
            xs = m.Position;
            pressure = run.Discharge;
        }

        sections.Add(back.Segments);

        var warnings = new List<string>();

        if (run.InletPressure < run.Discharge)
        {
            warnings.Add(
                $"The inlet pressure at A ({run.InletPressure / 1e6:F3} MPa) is below the station discharge pressure: " +
                "the first section is shorter than the others for the same ratio.");
        }

        return run.Build(Name, stations, sections, warnings);
    }
}
