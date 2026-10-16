using NaturalGasPipeline.Core.Flow;
using UnitsNet;

namespace NaturalGasPipeline.Network;

// Data model of compressor-station planning: the problem, the criteria, the
// placement-method contract and the results. The calculations live in
// CompressorStationPlanner (marching, layout evaluation) and in the
// placement methods (StationPlacementMethods.cs).

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
    /// Discharge pressure of the stations (their upper pressure limit).
    /// null: equal to <see cref="InletPressure"/>.
    /// </summary>
    public Pressure? StationDischargePressure { get; init; }

    /// <summary>
    /// Lowest acceptable suction pressure of a station.
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

    /// <summary>
    /// Maximum allowable operating pressure of the line pipe, ABSOLUTE
    /// (convert a code design pressure with
    /// <c>NaturalGasPipeline.Mechanics.SteelPipeDesign.ToAbsolute</c>).
    /// null: not checked. When set, every point of every section is compared
    /// with it, so pressure regained in downhill stretches is caught too;
    /// exceedances are reported as warnings, not exceptions.
    /// </summary>
    public Pressure? MaximumAllowableOperatingPressure { get; init; }

    /// <summary>
    /// Design limit on the mean gas velocity. null: not checked (the
    /// velocity is still reported per section). Typical long-distance
    /// transmission velocities are 4.6–12 m/s (White et al., Compression
    /// Machinery for Oil and Gas, 2019, ch. 9); the limit is the user's choice.
    /// </summary>
    public Speed? MaximumGasVelocity { get; init; }

    /// <summary>
    /// API RP 14E constant C for the erosional-velocity check V ≤ C/√ρ
    /// (US units; 100 continuous, 125 intermittent, 60 corrosive service).
    /// Always checked; see <c>GasVelocityCalculator</c>.
    /// </summary>
    public double ErosionalVelocityConstant { get; init; } = 100.0;

    /// <summary>
    /// Factory for callers compiled with C# 9–10 (Unity 6): constructors of
    /// types with <c>required</c> members are blocked for compilers that do
    /// not support them, so object initialisers cannot be used there.
    /// </summary>
    /// <param name="inletPressure">Pressure at A.</param>
    /// <param name="deliveryPressure">Target pressure at B.</param>
    /// <param name="stationDischargePressure">null: equal to the inlet pressure.</param>
    /// <param name="minimumSuctionPressure">null: equal to the delivery pressure.</param>
    /// <param name="maximumStepLength">null: 1 km.</param>
    /// <param name="maximumAllowableOperatingPressure">Absolute MAOP; null: not checked.</param>
    /// <param name="maximumGasVelocity">Velocity limit; null: not checked.</param>
    /// <param name="erosionalVelocityConstant">API RP 14E C; default 100 (continuous service).</param>
    public static StationPlanningCriteria Create(
        Pressure inletPressure,
        Pressure deliveryPressure,
        Pressure? stationDischargePressure = null,
        Pressure? minimumSuctionPressure = null,
        Length? maximumStepLength = null,
        Pressure? maximumAllowableOperatingPressure = null,
        Speed? maximumGasVelocity = null,
        double erosionalVelocityConstant = 100.0) => new()
    {
        InletPressure = inletPressure,
        DeliveryPressure = deliveryPressure,
        StationDischargePressure = stationDischargePressure,
        MinimumSuctionPressure = minimumSuctionPressure,
        MaximumStepLength = maximumStepLength ?? Length.FromKilometers(1.0),
        MaximumAllowableOperatingPressure = maximumAllowableOperatingPressure,
        MaximumGasVelocity = maximumGasVelocity,
        ErosionalVelocityConstant = erosionalVelocityConstant
    };

    internal double Discharge => (StationDischargePressure ?? InletPressure).Pascals;

    internal double MinimumSuction => (MinimumSuctionPressure ?? DeliveryPressure).Pascals;
}

/// <summary>
/// Everything a placement method needs: the route, the pipe, the design flow
/// and the pressure criteria. The gas and the pipe-flow model belong to the
/// <see cref="CompressorStationPlanner"/>.
/// </summary>
public sealed record StationPlanningProblem
{
    /// <summary>Surveyed route profile from A (first point) to B (last point).</summary>
    public required IReadOnlyList<ProfilePoint> Route { get; init; }

    public required Length InternalDiameter { get; init; }

    public required Length AbsoluteRoughness { get; init; }

    /// <summary>Design mass flow (what the solver conserves).</summary>
    public required MassFlow MassFlow { get; init; }

    /// <summary>
    /// The standard flow the design was specified in, when it was; kept for
    /// reporting. Use <see cref="CompressorStationPlanner.ProblemFor"/> to
    /// build a problem from a standard flow with an explicit conversion.
    /// </summary>
    public StandardVolumetricFlow? StandardFlow { get; init; }

    public required StationPlanningCriteria Criteria { get; init; }
}

/// <summary>A station imposed on a layout: where it is and what it discharges.</summary>
public readonly record struct StationSetting(Length Chainage, Pressure DischargePressure);

/// <summary>
/// A way of deciding how many stations there are and where they go.
/// Implemented: <see cref="MaximumSpacingPlacement"/> (method 1) and
/// <see cref="EqualRatioPlacement"/> (method 2). Optimisation methods
/// (minimum power, minimum cost, dynamic programming over candidate sites)
/// implement this interface too and score candidate layouts with
/// <see cref="CompressorStationPlanner.EvaluateLayout"/>.
/// </summary>
public interface IStationPlacementMethod
{
    /// <summary>Short name for reports.</summary>
    string Name { get; }

    StationPlan Place(CompressorStationPlanner planner, StationPlanningProblem problem);
}

/// <summary>A compressor station of a plan.</summary>
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

/// <summary>Pipe between two consecutive points of a plan (A, stations, B).</summary>
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

    /// <summary>
    /// Highest mean gas velocity in the section (at its lowest-pressure
    /// point). Zero for a section without segments.
    /// </summary>
    public Speed MaximumVelocity { get; init; }

    /// <summary>Chainage where <see cref="MaximumVelocity"/> occurs.</summary>
    public Length MaximumVelocityChainage { get; init; }

    /// <summary>
    /// Highest V / V_erosional in the section (API RP 14E with the criteria's
    /// C). Above 1 the erosional limit is exceeded.
    /// </summary>
    public double MaximumErosionalRatio { get; init; }
}

/// <summary>A solved station layout.</summary>
public sealed record StationPlan
{
    /// <summary>Name of the placement method (or "Evaluated layout").</summary>
    public required string Method { get; init; }

    public required MassFlow MassFlow { get; init; }

    /// <summary>The standard flow the design was specified in, if it was.</summary>
    public required StandardVolumetricFlow? StandardFlow { get; init; }

    public required Temperature FlowingTemperature { get; init; }

    public required Pressure InletPressure { get; init; }

    public required Pressure DeliveryPressureTarget { get; init; }

    /// <summary>Pressure actually delivered at B.</summary>
    public required Pressure DeliveryPressure { get; init; }

    public required Length RouteLength { get; init; }

    public required IReadOnlyList<PlannedStation> Stations { get; init; }

    /// <summary>A → first station, …, last station → B.</summary>
    public required IReadOnlyList<PipelineSection> Sections { get; init; }

    public required Power TotalIsentropicPower { get; init; }

    public required IReadOnlyList<string> Warnings { get; init; }

    public int StationCount => Stations.Count;
}

/// <summary>One row of the station-count comparison (method 2 with N stations).</summary>
public sealed record StationCountOption
{
    public required int StationCount { get; init; }

    /// <summary>False when N stations cannot meet the criteria; <see cref="Plan"/> is then null.</summary>
    public required bool Feasible { get; init; }

    public required StationPlan? Plan { get; init; }

    /// <summary>Why the option is infeasible (null when feasible).</summary>
    public required string? Reason { get; init; }

    /// <summary>Pressure ratio shared by every station (NaN when infeasible).</summary>
    public double PressureRatio => Plan is null || Plan.Stations.Count == 0 ? double.NaN : Plan.Stations[0].PressureRatio;

    public Power TotalIsentropicPower => Plan?.TotalIsentropicPower ?? Power.Zero;
}

/// <summary>
/// Number of stations against power: the minimum-station plan (method 1) and
/// equal-ratio plans (method 2) for N = N_min, N_min + 1, …
/// More stations mean lower ratios and less power, but more stations to build.
/// </summary>
public sealed record StationCountComparison
{
    public required StationPlan MaximumSpacing { get; init; }

    public required IReadOnlyList<StationCountOption> Options { get; init; }

    public int MinimumStationCount => MaximumSpacing.StationCount;
}

internal static class PlanningGuard
{
    public static double RequirePositive(double value, string name)
    {
        if (!double.IsFinite(value) || value <= 0.0)
        {
            throw new ArgumentOutOfRangeException(name, value, "Value must be finite and greater than zero.");
        }

        return value;
    }
}
