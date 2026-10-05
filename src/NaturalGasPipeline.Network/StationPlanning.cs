using NaturalGasPipeline.Compression;
using NaturalGasPipeline.Core.Common;
using NaturalGasPipeline.Core.Flow;
using NaturalGasPipeline.Core.Numerics;
using NaturalGasPipeline.Core.Pipeline;
using NaturalGasPipeline.Thermodynamics;
using UnitsNet;

namespace NaturalGasPipeline.Network;

/// <summary>
/// Pressure criteria for placing compressor stations along a route.
/// </summary>
public sealed record StationPlanningCriteria
{
    /// <summary>Pressure at the start of the route (point A).</summary>
    public required Pressure InletPressure { get; init; }

    /// <summary>Pressure required at the end of the route (point B).</summary>
    public required Pressure DeliveryPressure { get; init; }

    /// <summary>
    /// Discharge pressure of every station. null: equal to
    /// <see cref="InletPressure"/> (each station restores the pressure at A).
    /// The last station discharges only what is needed to deliver exactly
    /// <see cref="DeliveryPressure"/>.
    /// </summary>
    public Pressure? StationDischargePressure { get; init; }

    /// <summary>
    /// A station is placed where the pressure falls to this value.
    /// null: equal to <see cref="DeliveryPressure"/>.
    /// </summary>
    public Pressure? MinimumSuctionPressure { get; init; }

    /// <summary>
    /// Longest pipe segment used while marching. Z, μ and f are evaluated
    /// once per segment, so shorter steps are more accurate (error ∝ 1/N²,
    /// see SeriesSystemTests); station positions are then located exactly
    /// inside a step by root finding.
    /// </summary>
    public Length MaximumStepLength { get; init; } = Length.FromKilometers(1.0);

    internal double Discharge => (StationDischargePressure ?? InletPressure).Pascals;

    internal double MinimumSuction => (MinimumSuctionPressure ?? DeliveryPressure).Pascals;
}

/// <summary>
/// Answers "how many compressor stations, and how far apart?" for a route
/// with a real elevation profile, by the classical marching method (e.g.
/// Menon, Gas Pipeline Hydraulics, ch. 7):
///
/// 1. start at A with the inlet pressure;
/// 2. march along the route until the pressure falls to the minimum suction
///    pressure; place a station there, which restores the discharge pressure;
/// 3. repeat until B;
/// 4. the last station discharges exactly what is needed to deliver the
///    target pressure at B (backward march). If the gas would arrive below
///    the target without falling to the minimum suction, one more station is
///    placed where the backward profile from B reaches the discharge pressure.
///
/// Stated assumptions:
/// - pipe flow is isothermal at the flowing (ground) temperature; the gas
///   leaves each station cooled to that temperature (aftercooler duty is not
///   computed here);
/// - station power is reported as the isentropic power (η = 1) from the
///   fluid's heat-capacity ratio: the minimum any machine needs. Real power
///   follows from the hardware chosen later.
/// </summary>
public sealed class CompressorStationPlanner
{
    private const int MaximumStations = 500;

    private readonly ISegmentModel _model;
    private readonly Temperature _temperature;
    private readonly CompressorStage _isentropicReference;

    /// <param name="segmentModel">Pipe segment calculation.</param>
    /// <param name="flowingTemperature">Gas temperature in the pipe (isothermal).</param>
    /// <param name="molarMass">Molar mass of the gas (for the isentropic power).</param>
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
        RequirePositive(flowingTemperature.Kelvins, nameof(flowingTemperature));

        _model = segmentModel;
        _temperature = flowingTemperature;
        _isentropicReference = new CompressorStage(
            molarMass, compressibility, heatCapacityRatio, CompressionEfficiencyBasis.Isentropic, 1.0, 1.0);
    }

    /// <summary>Plan for a standard volumetric flow, converted explicitly to mass flow.</summary>
    public StationPlan Plan(
        IReadOnlyList<ProfilePoint> route,
        Length internalDiameter,
        Length absoluteRoughness,
        StandardVolumetricFlow standardFlow,
        StationPlanningCriteria criteria)
    {
        Guard.NotNull(standardFlow);
        MassFlow massFlow = FlowRateConverter.ConvertStandardVolumeToMass(standardFlow, _isentropicReference.MolarMass);
        return Plan(route, internalDiameter, absoluteRoughness, massFlow, criteria) with { StandardFlow = standardFlow };
    }

    public StationPlan Plan(
        IReadOnlyList<ProfilePoint> route,
        Length internalDiameter,
        Length absoluteRoughness,
        MassFlow massFlow,
        StationPlanningCriteria criteria)
    {
        Guard.NotNull(route);
        Guard.NotNull(criteria);

        var path = new Route(route, internalDiameter, absoluteRoughness);
        double m = RequirePositive(massFlow.KilogramsPerSecond, nameof(massFlow));
        double pA = RequirePositive(criteria.InletPressure.Pascals, nameof(criteria.InletPressure));
        double pB = RequirePositive(criteria.DeliveryPressure.Pascals, nameof(criteria.DeliveryPressure));
        double pDischarge = RequirePositive(criteria.Discharge, nameof(criteria.StationDischargePressure));
        double pMin = RequirePositive(criteria.MinimumSuction, nameof(criteria.MinimumSuctionPressure));
        double step = RequirePositive(criteria.MaximumStepLength.Meters, nameof(criteria.MaximumStepLength));

        if (pA <= pMin)
        {
            throw new ArgumentException("The inlet pressure must be above the minimum suction pressure.", nameof(criteria));
        }

        if (pDischarge <= pMin)
        {
            throw new ArgumentException("The station discharge pressure must be above the minimum suction pressure.", nameof(criteria));
        }

        var flow = MassFlow.FromKilogramsPerSecond(m);
        var warnings = new List<string>();
        var stationsAt = new List<(double X, double Suction, double Discharge)>();
        var sections = new List<List<SegmentResult>>();

        // ---- forward march, placing a station wherever P reaches pMin
        double x = 0.0;
        double p = pA;
        List<SegmentResult> current;

        while (true)
        {
            March forward = MarchForward(path, x, p, flow, step, pMin);
            current = forward.Segments;

            if (!forward.Stopped)
            {
                p = forward.Pressure;
                break;
            }

            sections.Add(current);
            stationsAt.Add((forward.Position, forward.Pressure, pDischarge));

            if (stationsAt.Count > MaximumStations)
            {
                throw new InvalidOperationException(
                    $"More than {MaximumStations} stations would be needed: the flow is too large for this pipe and these pressures.");
            }

            x = forward.Position;
            p = pDischarge;
        }

        // ---- delivery at B
        double sectionStart = stationsAt.Count == 0 ? 0.0 : stationsAt[^1].X;
        double sectionInlet = stationsAt.Count == 0 ? pA : pDischarge;

        if (p >= pB)
        {
            if (stationsAt.Count == 0)
            {
                if (p > pB)
                {
                    warnings.Add(
                        $"No compression needed: the gas arrives at {p / 1e6:F3} MPa, " +
                        $"{(p - pB) / 1e3:F1} kPa above the delivery pressure.");
                }

                sections.Add(current);
            }
            else
            {
                // Trim the last station: discharge only what B needs.
                March backward = MarchBackward(path, path.Length, pB, flow, step, sectionStart, stopAt: null);
                double required = backward.Pressure;
                (double sx, double suction, _) = stationsAt[^1];

                if (required < suction)
                {
                    warnings.Add(
                        $"The last station is not needed for delivery (B would receive {pB / 1e6:F3} MPa without it); " +
                        "it is kept only to hold the minimum suction pressure along the way.");
                    required = suction;
                    current = MarchForward(path, sx, suction, flow, step, stopAt: null).Segments;
                }
                else
                {
                    current = backward.Segments;
                }

                stationsAt[^1] = (sx, suction, required);
                sections.Add(current);
            }
        }
        else
        {
            // Arrives below the target (but not below pMin): one more station,
            // where the backward profile from B reaches the discharge pressure.
            March backward = MarchBackward(path, path.Length, pB, flow, step, sectionStart, stopAt: pDischarge);

            if (!backward.Stopped)
            {
                throw new InvalidOperationException(
                    "Cannot deliver the target pressure: even the discharge pressure right after the last station is not enough.");
            }

            March upToStation = MarchForward(path, sectionStart, sectionInlet, flow, step, stopAt: null, end: backward.Position);
            double suction = upToStation.Pressure;

            if (suction < pMin)
            {
                warnings.Add(
                    $"Station at km {backward.Position / 1e3:F2}: suction {suction / 1e6:F3} MPa is below the minimum " +
                    $"suction pressure {pMin / 1e6:F3} MPa (terrain between the stations).");
            }

            sections.Add(upToStation.Segments);
            stationsAt.Add((backward.Position, suction, pDischarge));
            sections.Add(backward.Segments);
        }

        return Build(path, flow, criteria, stationsAt, sections, warnings);
    }

    // ------------------------------------------------------------ marching

    private sealed record March(List<SegmentResult> Segments, double Position, double Pressure, bool Stopped);

    /// <summary>
    /// From (x, p) towards the end (or <paramref name="end"/>); stops where
    /// the pressure reaches <paramref name="stopAt"/>.
    /// </summary>
    private March MarchForward(Route path, double x, double p, MassFlow flow, double step, double? stopAt, double? end = null)
    {
        double xEnd = end ?? path.Length;
        var results = new List<SegmentResult>();

        while (xEnd - x > 1e-6)
        {
            double length = path.StepLength(x, xEnd, step, forward: true);
            SegmentResult? r = TryOutlet(path, x, length, p, flow);

            if (stopAt is double floor && (r is null || r.OutletPressure.Pascals <= floor))
            {
                double pIn = p;
                double xIn = x;
                double l = BrentRootSolver.FindRoot(
                    len => (TryOutlet(path, xIn, len, pIn, flow)?.OutletPressure.Pascals ?? 0.0) - floor,
                    1e-9 * length,
                    length,
                    absoluteTolerance: 1e-3);

                SegmentResult last = TryOutlet(path, x, l, p, flow)
                    ?? throw new InvalidOperationException("Segment capacity exceeded at the located station.");

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
    /// the required upstream pressure reaches <paramref name="stopAt"/>.
    /// Segments are returned in flow order.
    /// </summary>
    private March MarchBackward(Route path, double xEnd, double pEnd, MassFlow flow, double step, double start, double? stopAt)
    {
        var results = new List<SegmentResult>();
        double x = xEnd;
        double p = pEnd;

        while (x - start > 1e-6)
        {
            double length = path.StepLength(x, start, step, forward: false);
            SegmentResult r = _model.SolveInletPressure(path.Segment(x - length, length), _temperature, Pressure.FromPascals(p), flow);

            if (stopAt is double ceiling && r.InletPressure.Pascals >= ceiling)
            {
                double pOut = p;
                double xOut = x;
                double l = BrentRootSolver.FindRoot(
                    len => _model.SolveInletPressure(path.Segment(xOut - len, len), _temperature, Pressure.FromPascals(pOut), flow)
                        .InletPressure.Pascals - ceiling,
                    1e-9 * length,
                    length,
                    absoluteTolerance: 1e-3);

                SegmentResult first = _model.SolveInletPressure(path.Segment(x - l, l), _temperature, Pressure.FromPascals(p), flow);
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

    private SegmentResult? TryOutlet(Route path, double x, double length, double p, MassFlow flow)
    {
        try
        {
            return _model.SolveOutletPressure(path.Segment(x, length), _temperature, Pressure.FromPascals(p), flow);
        }
        catch (FlowCapacityExceededException)
        {
            return null;
        }
    }

    // ------------------------------------------------------------ result

    private StationPlan Build(
        Route path,
        MassFlow flow,
        StationPlanningCriteria criteria,
        List<(double X, double Suction, double Discharge)> stationsAt,
        List<List<SegmentResult>> sections,
        List<string> warnings)
    {
        var stations = new List<PlannedStation>(stationsAt.Count);
        double previous = 0.0;

        for (int i = 0; i < stationsAt.Count; i++)
        {
            (double sx, double suction, double discharge) = stationsAt[i];
            CompressionResult ideal = _isentropicReference.Compress(
                Pressure.FromPascals(suction), _temperature, Pressure.FromPascals(discharge), flow);

            stations.Add(new PlannedStation
            {
                Name = $"CS-{i + 1}",
                Chainage = Length.FromMeters(sx),
                Elevation = Length.FromMeters(path.ElevationAt(sx)),
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
            double start = i == 0 ? 0.0 : stationsAt[i - 1].X;
            double endX = i < stationsAt.Count ? stationsAt[i].X : path.Length;

            pipeSections.Add(new PipelineSection
            {
                Name = $"{(i == 0 ? "A" : stations[i - 1].Name)} → {(i < stations.Count ? stations[i].Name : "B")}",
                StartChainage = Length.FromMeters(start),
                EndChainage = Length.FromMeters(endX),
                InletPressure = segs[0].InletPressure,
                OutletPressure = segs[^1].OutletPressure,
                FrictionPressureDrop = Pressure.FromPascals(segs.Sum(s => s.FrictionPressureDrop.Pascals)),
                ElevationPressureDrop = Pressure.FromPascals(segs.Sum(s => s.ElevationPressureDrop.Pascals)),
                Segments = segs
            });

            warnings.AddRange(segs.SelectMany(s => s.Warnings).Distinct().Select(w => $"{pipeSections[^1].Name}: {w}"));
        }

        return new StationPlan
        {
            MassFlow = flow,
            StandardFlow = null,
            FlowingTemperature = _temperature,
            InletPressure = criteria.InletPressure,
            DeliveryPressureTarget = criteria.DeliveryPressure,
            DeliveryPressure = pipeSections[^1].OutletPressure,
            RouteLength = Length.FromMeters(path.Length),
            Stations = stations,
            Sections = pipeSections,
            TotalIsentropicPower = Power.FromWatts(stations.Sum(s => s.IsentropicPower.Watts)),
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

    // ------------------------------------------------------------ route

    /// <summary>Route profile with linear elevation between surveyed points.</summary>
    private sealed class Route
    {
        private readonly double[] _x;
        private readonly double[] _z;
        private readonly Length _diameter;
        private readonly Length _roughness;

        public Route(IReadOnlyList<ProfilePoint> points, Length diameter, Length roughness)
        {
            // Validates chainage order, slopes and builds nothing we keep.
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
}

/// <summary>A compressor station located by the planner.</summary>
public sealed record PlannedStation
{
    public required string Name { get; init; }

    /// <summary>Distance from A along the pipe.</summary>
    public required Length Chainage { get; init; }

    public required Length Elevation { get; init; }

    /// <summary>From A or from the previous station.</summary>
    public required Length DistanceFromPrevious { get; init; }

    public required Pressure SuctionPressure { get; init; }

    public required Temperature SuctionTemperature { get; init; }

    public required Pressure DischargePressure { get; init; }

    public required double PressureRatio { get; init; }

    /// <summary>Discharge temperature of ideal (η = 1) single-stage compression; real machines run hotter.</summary>
    public required Temperature IsentropicDischargeTemperature { get; init; }

    public required SpecificEnergy IsentropicHead { get; init; }

    /// <summary>ṁ·H_s: the minimum power; the real value follows from the hardware chosen.</summary>
    public required Power IsentropicPower { get; init; }
}

/// <summary>Pipe between two consecutive points of the plan (A, stations, B).</summary>
public sealed record PipelineSection
{
    public required string Name { get; init; }

    public required Length StartChainage { get; init; }

    public required Length EndChainage { get; init; }

    public Length Length => EndChainage - StartChainage;

    public required Pressure InletPressure { get; init; }

    public required Pressure OutletPressure { get; init; }

    public Pressure PressureDrop => InletPressure - OutletPressure;

    /// <summary>Sum of the segments' friction drops.</summary>
    public required Pressure FrictionPressureDrop { get; init; }

    /// <summary>Sum of the segments' elevation drops (negative if the section descends overall).</summary>
    public required Pressure ElevationPressureDrop { get; init; }

    public required IReadOnlyList<SegmentResult> Segments { get; init; }
}

public sealed record StationPlan
{
    public required MassFlow MassFlow { get; init; }

    /// <summary>The standard flow the plan was requested for, if it was given that way.</summary>
    public required StandardVolumetricFlow? StandardFlow { get; init; }

    public required Temperature FlowingTemperature { get; init; }

    public required Pressure InletPressure { get; init; }

    public required Pressure DeliveryPressureTarget { get; init; }

    /// <summary>Pressure actually delivered at B (equals the target when stations are needed).</summary>
    public required Pressure DeliveryPressure { get; init; }

    public required Length RouteLength { get; init; }

    public required IReadOnlyList<PlannedStation> Stations { get; init; }

    /// <summary>A → first station, …, last station → B.</summary>
    public required IReadOnlyList<PipelineSection> Sections { get; init; }

    public required Power TotalIsentropicPower { get; init; }

    public required IReadOnlyList<string> Warnings { get; init; }
}
