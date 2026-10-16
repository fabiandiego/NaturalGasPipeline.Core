using NaturalGasPipeline.Compression;
using NaturalGasPipeline.Core.Common;
using NaturalGasPipeline.Core.Flow;
using NaturalGasPipeline.Core.Numerics;
using NaturalGasPipeline.Core.Pipeline;
using NaturalGasPipeline.Hydraulics;
using NaturalGasPipeline.Thermodynamics;
using UnitsNet;

namespace NaturalGasPipeline.Network;

/// <summary>
/// Answers "how many compressor stations, and how far apart?" for a route
/// with a real elevation profile.
///
/// The planner owns the physics (pipe-flow model, flowing temperature, gas)
/// and three building blocks shared by every placement method:
/// - marching along the route, forward from A or backward from B, stopping
///   where the pressure reaches a given value (located exactly by Brent's
///   method inside a step);
/// - <see cref="EvaluateLayout"/>: the hydraulics of any given layout
///   (station positions and discharge pressures), which optimisation
///   methods use to score candidates;
/// - the result assembly (sections, friction / elevation split, isentropic
///   power per station).
///
/// Placement methods: <see cref="MaximumSpacingPlacement"/> (method 1) and
/// <see cref="EqualRatioPlacement"/> (method 2); <see cref="CompareStationCounts"/>
/// tabulates power against the number of stations.
///
/// Stated assumptions:
/// - pipe flow is isothermal at the flowing (ground) temperature; the gas
///   leaves each station cooled to that temperature (aftercooler duty is not
///   computed here);
/// - station power is the isentropic power (η = 1) from the fluid's
///   heat-capacity ratio: the minimum any machine needs. Real power follows
///   from the hardware chosen later.
/// </summary>
public sealed class CompressorStationPlanner
{
    private readonly ISegmentModel _model;
    private readonly Temperature _temperature;
    private readonly CompressorStage _isentropicReference;

    /// <param name="segmentModel">Pipe segment calculation.</param>
    /// <param name="flowingTemperature">Gas temperature in the pipe (isothermal).</param>
    /// <param name="molarMass">Molar mass of the gas (flow conversion and isentropic power).</param>
    /// <param name="compressibility">Z model of the gas (for the isentropic power).</param>
    /// <param name="heatCapacityRatio">k = cp/cv of the gas (for the isentropic power).</param>
    public CompressorStationPlanner(
        ISegmentModel segmentModel,
        Temperature flowingTemperature,
        MolarMass molarMass,
        ICompressibilityModel compressibility,
        double heatCapacityRatio)
    {
        Guard.NotNull(segmentModel);
        PlanningGuard.RequirePositive(flowingTemperature.Kelvins, nameof(flowingTemperature));

        _model = segmentModel;
        _temperature = flowingTemperature;
        _isentropicReference = new CompressorStage(
            molarMass, compressibility, heatCapacityRatio, CompressionEfficiencyBasis.Isentropic, 1.0, 1.0);
    }

    public Temperature FlowingTemperature => _temperature;

    public MolarMass MolarMass => _isentropicReference.MolarMass;

    /// <summary>
    /// Builds a problem from a standard volumetric flow. The conversion to
    /// mass flow is explicit: ṁ = Q_std·P_std·M / (Z_std·R·T_std) with the
    /// conditions carried by <paramref name="standardFlow"/>.
    /// </summary>
    /// <param name="route">Surveyed route profile from A to B.</param>
    /// <param name="internalDiameter">Pipe internal diameter.</param>
    /// <param name="absoluteRoughness">Pipe wall roughness.</param>
    /// <param name="standardFlow">Design flow at stated standard conditions.</param>
    /// <param name="criteria">Pressure criteria.</param>
    public StationPlanningProblem ProblemFor(
        IReadOnlyList<ProfilePoint> route,
        Length internalDiameter,
        Length absoluteRoughness,
        StandardVolumetricFlow standardFlow,
        StationPlanningCriteria criteria)
    {
        Guard.NotNull(standardFlow);

        return new StationPlanningProblem
        {
            Route = route,
            InternalDiameter = internalDiameter,
            AbsoluteRoughness = absoluteRoughness,
            MassFlow = FlowRateConverter.ConvertStandardVolumeToMass(standardFlow, MolarMass),
            StandardFlow = standardFlow,
            Criteria = criteria
        };
    }

    /// <summary>Places stations with the given method.</summary>
    public StationPlan Plan(StationPlanningProblem problem, IStationPlacementMethod method)
    {
        Guard.NotNull(method);
        return method.Place(this, problem);
    }

    /// <summary>
    /// Method 1 for the minimum number of stations, then method 2 for
    /// N = N_min … N_min + <paramref name="additionalStations"/>.
    /// </summary>
    /// <param name="problem">The planning problem.</param>
    /// <param name="additionalStations">How many station counts above the minimum to evaluate.</param>
    public StationCountComparison CompareStationCounts(StationPlanningProblem problem, int additionalStations = 3)
    {
        if (additionalStations < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(additionalStations), additionalStations, "Must not be negative.");
        }

        StationPlan minimum = Plan(problem, new MaximumSpacingPlacement());
        var options = new List<StationCountOption>();

        if (minimum.StationCount > 0)
        {
            for (int n = minimum.StationCount; n <= minimum.StationCount + additionalStations; n++)
            {
                try
                {
                    StationPlan plan = Plan(problem, new EqualRatioPlacement(n));
                    options.Add(new StationCountOption { StationCount = n, Feasible = true, Plan = plan, Reason = null });
                }
                catch (StationPlacementInfeasibleException e)
                {
                    options.Add(new StationCountOption { StationCount = n, Feasible = false, Plan = null, Reason = e.Message });
                }
            }
        }

        return new StationCountComparison { MaximumSpacing = minimum, Options = options };
    }

    /// <summary>
    /// Hydraulics of a given layout: marches from A with the inlet pressure;
    /// at each station the gas is compressed to the given discharge pressure.
    /// No station is moved and nothing is trimmed: criteria that are not met
    /// (suction below the minimum, discharge above the limit, delivery below
    /// the target) are reported as warnings, so that optimisation methods can
    /// score and reject candidates.
    /// </summary>
    /// <param name="problem">The planning problem.</param>
    /// <param name="stations">Stations in flow order, strictly inside the route.</param>
    /// <exception cref="ArgumentException">A discharge pressure is below the suction pressure, or positions are not increasing.</exception>
    /// <exception cref="FlowCapacityExceededException">The flow cannot pass some part of the route.</exception>
    public StationPlan EvaluateLayout(StationPlanningProblem problem, IReadOnlyList<StationSetting> stations)
    {
        Guard.NotNull(stations);
        PlanningRun run = Begin(problem);

        var layout = new List<(double X, double Suction, double Discharge)>(stations.Count);
        var sections = new List<List<SegmentResult>>(stations.Count + 1);
        var warnings = new List<string>();
        double x = 0.0;
        double p = run.InletPressure;

        foreach (StationSetting s in stations)
        {
            double sx = s.Chainage.Meters;
            double discharge = PlanningGuard.RequirePositive(s.DischargePressure.Pascals, nameof(stations));

            if (!(sx > x + 1e-6) || !(sx < run.Route.Length - 1e-6))
            {
                throw new ArgumentException("Station chainages must increase strictly and lie inside the route.", nameof(stations));
            }

            PlanningRun.March m = run.Forward(x, p, stopAt: null, end: sx);

            if (discharge < m.Pressure)
            {
                throw new ArgumentException(
                    $"Station at km {sx / 1e3:F3}: discharge {discharge / 1e6:F4} MPa is below its suction {m.Pressure / 1e6:F4} MPa.",
                    nameof(stations));
            }

            sections.Add(m.Segments);
            layout.Add((sx, m.Pressure, discharge));
            x = sx;
            p = discharge;
        }

        PlanningRun.March last = run.Forward(x, p, stopAt: null);
        sections.Add(last.Segments);

        StationPlan plan = run.Build("Evaluated layout", layout, sections, warnings);
        AddCriteriaWarnings(plan, run, warnings);
        return plan;
    }

    private static void AddCriteriaWarnings(StationPlan plan, PlanningRun run, List<string> warnings)
    {
        foreach (PlannedStation s in plan.Stations)
        {
            if (s.SuctionPressure.Pascals < run.MinimumSuction - 1.0)
            {
                warnings.Add($"{s.Name}: suction {s.SuctionPressure.Megapascals:F4} MPa is below the minimum suction pressure.");
            }

            if (s.DischargePressure.Pascals > run.Discharge + 1.0)
            {
                warnings.Add($"{s.Name}: discharge {s.DischargePressure.Megapascals:F4} MPa is above the station discharge limit.");
            }
        }

        if (plan.DeliveryPressure.Pascals < run.DeliveryPressure - 1.0)
        {
            warnings.Add($"Delivery {plan.DeliveryPressure.Megapascals:F4} MPa is below the target.");
        }
    }

    internal PlanningRun Begin(StationPlanningProblem problem) => new(_model, _temperature, _isentropicReference, problem);
}

/// <summary>Thrown when a placement method cannot meet the criteria (e.g. too few stations).</summary>
public sealed class StationPlacementInfeasibleException : InvalidOperationException
{
    public StationPlacementInfeasibleException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// One planning calculation: the route, flow and criteria of a problem with
/// the planner's physics. Shared by the placement methods.
/// </summary>
internal sealed class PlanningRun
{
    internal sealed record March(List<SegmentResult> Segments, double Position, double Pressure, bool Stopped);

    private readonly ISegmentModel _model;
    private readonly Temperature _temperature;
    private readonly CompressorStage _isentropic;
    private readonly StationPlanningProblem _problem;
    private readonly MassFlow _flow;
    private readonly double _step;

    public PlanningRun(ISegmentModel model, Temperature temperature, CompressorStage isentropic, StationPlanningProblem problem)
    {
        Guard.NotNull(problem);
        Guard.NotNull(problem.Criteria);

        _model = model;
        _temperature = temperature;
        _isentropic = isentropic;
        _problem = problem;

        Route = new RouteProfile(problem.Route, problem.InternalDiameter, problem.AbsoluteRoughness);
        _flow = MassFlow.FromKilogramsPerSecond(PlanningGuard.RequirePositive(problem.MassFlow.KilogramsPerSecond, nameof(problem.MassFlow)));

        StationPlanningCriteria c = problem.Criteria;
        InletPressure = PlanningGuard.RequirePositive(c.InletPressure.Pascals, nameof(c.InletPressure));
        DeliveryPressure = PlanningGuard.RequirePositive(c.DeliveryPressure.Pascals, nameof(c.DeliveryPressure));
        Discharge = PlanningGuard.RequirePositive(c.Discharge, nameof(c.StationDischargePressure));
        MinimumSuction = PlanningGuard.RequirePositive(c.MinimumSuction, nameof(c.MinimumSuctionPressure));
        _step = PlanningGuard.RequirePositive(c.MaximumStepLength.Meters, nameof(c.MaximumStepLength));
        PlanningGuard.RequirePositive(c.ErosionalVelocityConstant, nameof(c.ErosionalVelocityConstant));

        if (c.MaximumGasVelocity is Speed vmax)
        {
            PlanningGuard.RequirePositive(vmax.MetersPerSecond, nameof(c.MaximumGasVelocity));
        }

        if (InletPressure <= MinimumSuction)
        {
            throw new ArgumentException("The inlet pressure must be above the minimum suction pressure.", nameof(problem));
        }

        if (Discharge <= MinimumSuction)
        {
            throw new ArgumentException("The station discharge pressure must be above the minimum suction pressure.", nameof(problem));
        }
    }

    public RouteProfile Route { get; }

    public double InletPressure { get; }

    public double DeliveryPressure { get; }

    public double Discharge { get; }

    public double MinimumSuction { get; }

    /// <summary>
    /// From (x, p) towards the end of the route (or <paramref name="end"/>);
    /// stops where the pressure falls to <paramref name="stopAt"/>.
    /// </summary>
    public March Forward(double x, double p, double? stopAt, double? end = null)
    {
        double xEnd = end ?? Route.Length;
        var results = new List<SegmentResult>();

        if (stopAt is double floor0 && p <= floor0)
        {
            return new March(results, x, p, true);
        }

        while (xEnd - x > 1e-6)
        {
            double length = Route.StepLength(x, xEnd, _step, forward: true);
            SegmentResult? r = TryOutlet(x, length, p);

            if (stopAt is double floor && (r is null || r.OutletPressure.Pascals <= floor))
            {
                double pIn = p;
                double xIn = x;
                double l = BrentRootSolver.FindRoot(
                    len => (TryOutlet(xIn, len, pIn)?.OutletPressure.Pascals ?? 0.0) - floor,
                    1e-9 * length,
                    length,
                    absoluteTolerance: 1e-3);

                SegmentResult last = TryOutlet(x, l, p)
                    ?? throw new FlowCapacityExceededException("Segment capacity exceeded at the located station.");

                results.Add(last);
                return new March(results, x + l, last.OutletPressure.Pascals, true);
            }

            if (r is null)
            {
                throw new FlowCapacityExceededException(
                    $"The flow cannot pass the pipe after km {x / 1e3:F2}: no positive outlet pressure.");
            }

            results.Add(r);
            x += length;
            p = r.OutletPressure.Pascals;
        }

        return new March(results, xEnd, p, false);
    }

    /// <summary>
    /// From (xEnd, pEnd) back towards <paramref name="start"/>; stops where
    /// the required upstream pressure rises to <paramref name="stopAt"/>.
    /// Segments are returned in flow order.
    /// </summary>
    public March Backward(double xEnd, double pEnd, double start, double? stopAt)
    {
        var results = new List<SegmentResult>();
        double x = xEnd;
        double p = pEnd;

        while (x - start > 1e-6)
        {
            double length = Route.StepLength(x, start, _step, forward: false);
            SegmentResult r = Inlet(x - length, length, p);

            if (stopAt is double ceiling && r.InletPressure.Pascals >= ceiling)
            {
                double pOut = p;
                double xOut = x;
                double l = BrentRootSolver.FindRoot(
                    len => Inlet(xOut - len, len, pOut).InletPressure.Pascals - ceiling,
                    1e-9 * length,
                    length,
                    absoluteTolerance: 1e-3);

                SegmentResult first = Inlet(x - l, l, p);
                results.Add(first);
                results.Reverse();
                return new March(results, x - l, first.InletPressure.Pascals, true);
            }

            results.Add(r);
            x -= length;
            p = r.InletPressure.Pascals;
        }

        results.Reverse();
        return new March(results, start, p, false);
    }

    private SegmentResult Inlet(double x, double length, double pOut) =>
        _model.SolveInletPressure(Route.Segment(x, length), _temperature, Pressure.FromPascals(pOut), _flow);

    private SegmentResult? TryOutlet(double x, double length, double p)
    {
        try
        {
            return _model.SolveOutletPressure(Route.Segment(x, length), _temperature, Pressure.FromPascals(p), _flow);
        }
        catch (FlowCapacityExceededException)
        {
            return null;
        }
    }

    /// <summary>
    /// Compares the highest pressure in a section (section inlet and every
    /// segment end) with the MAOP of the criteria, when one is given. The
    /// maximum is not always at the inlet: downhill stretches can regain
    /// pressure. Pressures are absolute on both sides.
    /// </summary>
    private void AddMaopWarning(string name, double start, double inlet, List<SegmentResult> segs, List<string> warnings)
    {
        if (_problem.Criteria.MaximumAllowableOperatingPressure is not Pressure maop)
        {
            return;
        }

        double limit = PlanningGuard.RequirePositive(maop.Pascals, nameof(StationPlanningCriteria.MaximumAllowableOperatingPressure));
        double peak = inlet;
        double peakAt = start;
        double x = start;

        foreach (SegmentResult seg in segs)
        {
            if (seg.InletPressure.Pascals > peak)
            {
                peak = seg.InletPressure.Pascals;
                peakAt = x;
            }

            x += seg.Segment.Length.Meters;

            if (seg.OutletPressure.Pascals > peak)
            {
                peak = seg.OutletPressure.Pascals;
                peakAt = x;
            }
        }

        if (peak > limit + 1.0)
        {
            warnings.Add(
                $"{name}: pressure {peak / 1e6:F4} MPa(a) at km {peakAt / 1e3:F3} exceeds the MAOP {limit / 1e6:F4} MPa(a).");
        }
    }

    /// <summary>
    /// Segment warnings of one section, one line per kind. Warnings that differ
    /// only in their numbers (e.g. "Z model used outside its validated range at
    /// P2 = 19580.1 kPa" on every 1 km step) are collapsed into the first one
    /// plus a count, so a long section does not produce hundreds of lines.
    /// </summary>
    internal static IEnumerable<string> SummariseSegmentWarnings(IEnumerable<SegmentResult> segments)
    {
        var groups = new List<(string Key, string First, int Count, bool Identical)>();
        foreach (string w in segments.SelectMany(s => s.Warnings))
        {
            string key = System.Text.RegularExpressions.Regex.Replace(w, @"[-+]?\d+(\.\d+)?", "#");
            int i = groups.FindIndex(g => g.Key == key);
            if (i < 0)
            {
                groups.Add((key, w, 1, true));
            }
            else
            {
                (string k, string first, int count, bool identical) = groups[i];
                groups[i] = (k, first, count + 1, identical && w == first);
            }
        }

        // Identical repeats collapse silently (as Distinct did); repeats with other numbers keep a count.
        return groups.Select(g => g.Count == 1 || g.Identical ? g.First : $"{g.First} (and {g.Count - 1} more like it in this section)");
    }

    /// <summary>
    /// Velocity and erosional ratio at every segment end of a section; the
    /// maxima are stored on the section and checked against the criteria.
    ///
    /// Density at a segment end: ρ = ρ̄·P/P̄, i.e. the segment's average Z
    /// is kept over the segment (ρ = P·M/(Z·R·T)). Over a marching step
    /// (≤ 1 km by default) Z changes by well under 0.1 %, so this error is
    /// negligible against the velocity limits being checked. The segment
    /// inlet is covered by the previous segment's end, and the section inlet
    /// is the highest-pressure (lowest-velocity) point, so ends suffice.
    /// </summary>
    private PipelineSection AddVelocityCheck(PipelineSection section, List<SegmentResult> segs, List<string> warnings)
    {
        if (segs.Count == 0)
        {
            return section;
        }

        double c = _problem.Criteria.ErosionalVelocityConstant;
        double maxV = 0.0, maxAt = section.StartChainage.Meters, maxRatio = 0.0, ratioAt = maxAt;
        double x = section.StartChainage.Meters;

        foreach (SegmentResult seg in segs)
        {
            x += seg.Segment.Length.Meters;

            var rho = Density.FromKilogramsPerCubicMeter(
                seg.AverageDensity.KilogramsPerCubicMeter * seg.OutletPressure.Pascals / seg.AveragePressure.Pascals);
            double v = GasVelocityCalculator.Velocity(seg.MassFlow, rho, seg.Segment.InternalDiameter).MetersPerSecond;
            double ratio = v / GasVelocityCalculator.ErosionalVelocity(rho, c).MetersPerSecond;

            if (v > maxV)
            {
                maxV = v;
                maxAt = x;
            }

            if (ratio > maxRatio)
            {
                maxRatio = ratio;
                ratioAt = x;
            }
        }

        if (_problem.Criteria.MaximumGasVelocity is Speed limit && maxV > limit.MetersPerSecond + 1e-9)
        {
            warnings.Add(
                $"{section.Name}: gas velocity {maxV:F2} m/s at km {maxAt / 1e3:F3} exceeds the limit {limit.MetersPerSecond:F2} m/s.");
        }

        if (maxRatio > 1.0)
        {
            warnings.Add(
                $"{section.Name}: gas velocity at km {ratioAt / 1e3:F3} is {maxRatio:F2} × the API RP 14E erosional velocity (C = {c:F0}).");
        }

        return section with
        {
            MaximumVelocity = Speed.FromMetersPerSecond(maxV),
            MaximumVelocityChainage = Length.FromMeters(maxAt),
            MaximumErosionalRatio = maxRatio
        };
    }

    /// <summary>
    /// Assembles a plan. <paramref name="sections"/> has one entry more than
    /// <paramref name="stations"/> (A → first station, …, last station → B).
    /// </summary>
    public StationPlan Build(
        string method,
        List<(double X, double Suction, double Discharge)> stations,
        List<List<SegmentResult>> sections,
        List<string> warnings)
    {
        var planned = new List<PlannedStation>(stations.Count);
        double previous = 0.0;

        for (int i = 0; i < stations.Count; i++)
        {
            (double sx, double suction, double discharge) = stations[i];
            CompressionResult ideal = _isentropic.Compress(
                Pressure.FromPascals(suction), _temperature, Pressure.FromPascals(discharge), _flow);

            planned.Add(new PlannedStation
            {
                Name = $"CS-{i + 1}",
                Chainage = Length.FromMeters(sx),
                Elevation = Length.FromMeters(Route.ElevationAt(sx)),
                DistanceFromPrevious = Length.FromMeters(sx - previous),
                SuctionPressure = Pressure.FromPascals(suction),
                SuctionTemperature = _temperature,
                DischargePressure = Pressure.FromPascals(discharge),
                PressureRatio = discharge / suction,
                IsentropicDischargeTemperature = ideal.DischargeTemperature,
                IsentropicHead = ideal.Head,
                IsentropicPower = ideal.GasPower
            });

            previous = sx;
        }

        var pipeSections = new List<PipelineSection>(sections.Count);

        for (int i = 0; i < sections.Count; i++)
        {
            List<SegmentResult> segs = sections[i];
            double start = i == 0 ? 0.0 : stations[i - 1].X;
            double end = i < stations.Count ? stations[i].X : Route.Length;
            double inlet = i == 0 ? InletPressure : stations[i - 1].Discharge;
            string name = $"{(i == 0 ? "A" : planned[i - 1].Name)} → {(i < planned.Count ? planned[i].Name : "B")}";

            pipeSections.Add(new PipelineSection
            {
                Name = name,
                StartChainage = Length.FromMeters(start),
                EndChainage = Length.FromMeters(end),
                InletPressure = segs.Count > 0 ? segs[0].InletPressure : Pressure.FromPascals(inlet),
                OutletPressure = segs.Count > 0 ? segs[^1].OutletPressure : Pressure.FromPascals(inlet),
                FrictionPressureDrop = Pressure.FromPascals(segs.Sum(s => s.FrictionPressureDrop.Pascals)),
                ElevationPressureDrop = Pressure.FromPascals(segs.Sum(s => s.ElevationPressureDrop.Pascals)),
                Segments = segs
            });

            warnings.AddRange(SummariseSegmentWarnings(segs).Select(w => $"{name}: {w}"));
            AddMaopWarning(name, start, inlet, segs, warnings);
            pipeSections[^1] = AddVelocityCheck(pipeSections[^1], segs, warnings);
        }

        return new StationPlan
        {
            Method = method,
            MassFlow = _flow,
            StandardFlow = _problem.StandardFlow,
            FlowingTemperature = _temperature,
            InletPressure = _problem.Criteria.InletPressure,
            DeliveryPressureTarget = _problem.Criteria.DeliveryPressure,
            DeliveryPressure = pipeSections[^1].OutletPressure,
            RouteLength = Length.FromMeters(Route.Length),
            Stations = planned,
            Sections = pipeSections,
            TotalIsentropicPower = Power.FromWatts(planned.Sum(s => s.IsentropicPower.Watts)),
            Warnings = warnings
        };
    }
}

/// <summary>Route profile with linear elevation between surveyed points (chainage from 0 at A).</summary>
internal sealed class RouteProfile
{
    private readonly double[] _x;
    private readonly double[] _z;
    private readonly Length _diameter;
    private readonly Length _roughness;

    public RouteProfile(IReadOnlyList<ProfilePoint> points, Length diameter, Length roughness)
    {
        // Validates chainage order and slopes.
        PipelineProfile.ToSegments(points, diameter, roughness);

        double x0 = points[0].Chainage.Meters;
        _x = points.Select(pt => pt.Chainage.Meters - x0).ToArray();
        _z = points.Select(pt => pt.Elevation.Meters).ToArray();
        _diameter = diameter;
        _roughness = roughness;
    }

    public double Length => _x[^1];

    public double ElevationAt(double x)
    {
        int i = Leg(x);
        double t = (x - _x[i]) / (_x[i + 1] - _x[i]);
        return _z[i] + t * (_z[i + 1] - _z[i]);
    }

    /// <summary>Segment from x over length (must not cross a survey point).</summary>
    public PipeSegment Segment(double x, double length) =>
        new(_diameter, UnitsNet.Length.FromMeters(length), _roughness,
            UnitsNet.Length.FromMeters(ElevationAt(x + length) - ElevationAt(x)));

    /// <summary>Next step from x towards target, ending at the next survey point at the latest.</summary>
    public double StepLength(double x, double target, double maxStep, bool forward)
    {
        if (forward)
        {
            double nextPoint = _x.First(px => px > x + 1e-6);
            return System.Math.Min(System.Math.Min(maxStep, target - x), nextPoint - x);
        }

        double previousPoint = _x.Last(px => px < x - 1e-6);
        return System.Math.Min(System.Math.Min(maxStep, x - target), x - previousPoint);
    }

    private int Leg(double x)
    {
        for (int i = 0; i < _x.Length - 2; i++)
        {
            if (x <= _x[i + 1])
            {
                return i;
            }
        }

        return _x.Length - 2;
    }
}
