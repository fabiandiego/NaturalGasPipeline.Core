using NaturalGasPipeline.Core.Common;
using NaturalGasPipeline.Core.Flow;
using NaturalGasPipeline.Core.Pipeline;
using NaturalGasPipeline.Correlations;
using NaturalGasPipeline.Hydraulics;
using NaturalGasPipeline.Thermodynamics;
using UnitsNet;

namespace NaturalGasPipeline.Network;

/// <summary>
/// One pipe segment as seen by a network or a series system: pressures and
/// mass flow (mass is what is conserved at a node). Any segment calculation
/// can take part by implementing this interface.
/// </summary>
public interface ISegmentModel
{
    /// <summary>Short, human-readable name for reports.</summary>
    string Name { get; }

    SegmentResult SolveOutletPressure(PipeSegment segment, Temperature temperature, Pressure inletPressure, MassFlow massFlow);

    SegmentResult SolveInletPressure(PipeSegment segment, Temperature temperature, Pressure outletPressure, MassFlow massFlow);

    /// <summary>
    /// Mass flow from inlet to outlet for given end pressures. Throws
    /// <see cref="NoForwardFlowException"/> when the pressures do not drive
    /// flow in that direction.
    /// </summary>
    SegmentResult SolveMassFlow(PipeSegment segment, Temperature temperature, Pressure inletPressure, Pressure outletPressure);
}

/// <summary>
/// State of one solved segment.
///
/// The total pressure drop is split into an elevation (hydrostatic) part and
/// a friction part:
///   ΔP_elevation = ρ_lm·g·Δz,   ρ_lm = ρ̄·P_lm/P_avg,   P_lm = (P1 − P2)/ln(P1/P2)
///   ΔP_friction  = ΔP_total − ΔP_elevation
/// ρ̄ is the density at the solver's average pressure; scaling it to the
/// log-mean pressure makes the elevation part exact for a static gas column
/// at constant Z (P2 = P1·e^(−s/2)). The split is a definition for
/// reporting: the momentum balance itself is integrated as a whole (P² form).
/// Uphill ΔP_elevation &gt; 0; downhill it is negative (the gas regains pressure).
/// </summary>
public sealed record SegmentResult
{
    public required string Model { get; init; }

    public required PipeSegment Segment { get; init; }

    public required Temperature Temperature { get; init; }

    public required Pressure InletPressure { get; init; }

    public required Pressure OutletPressure { get; init; }

    public required MassFlow MassFlow { get; init; }

    public required Pressure AveragePressure { get; init; }

    public required double AverageCompressibilityFactor { get; init; }

    /// <summary>Gas density at <see cref="AveragePressure"/> and <see cref="Temperature"/>.</summary>
    public required Density AverageDensity { get; init; }

    public required IReadOnlyList<string> Warnings { get; init; }

    public Pressure PressureDrop => InletPressure - OutletPressure;

    /// <summary>ρ_lm·g·Δz (negative downhill).</summary>
    public Pressure ElevationPressureDrop
    {
        get
        {
            double p1 = InletPressure.Pascals;
            double p2 = OutletPressure.Pascals;
            double logMean = System.Math.Abs(p1 - p2) <= 1e-12 * p1 ? p1 : (p1 - p2) / System.Math.Log(p1 / p2);
            double density = AverageDensity.KilogramsPerCubicMeter * logMean / AveragePressure.Pascals;
            return Pressure.FromPascals(density * PhysicalConstants.StandardGravity * Segment.ElevationChange.Meters);
        }
    }

    /// <summary>ΔP_total − ΔP_elevation.</summary>
    public Pressure FrictionPressureDrop => PressureDrop - ElevationPressureDrop;
}

/// <summary>
/// The SI isothermal solver of the Hydraulics module
/// (<see cref="IsothermalGasFlowSolver"/>): Darcy friction from any
/// <see cref="IFrictionFactorModel"/>, exact physical constants.
/// </summary>
public sealed class IsothermalSegmentModel : ISegmentModel
{
    private readonly IsothermalGasFlowSolver _solver;

    public IsothermalSegmentModel(
        MolarMass molarMass,
        ICompressibilityModel compressibility,
        IViscosityModel viscosity,
        IFrictionFactorModel? friction = null)
    {
        _solver = new IsothermalGasFlowSolver(molarMass, compressibility, viscosity, friction);
        Name = $"Isothermal ({friction?.Name ?? "Colebrook-White"})";
    }

    public string Name { get; }

    public SegmentResult SolveOutletPressure(PipeSegment segment, Temperature temperature, Pressure inletPressure, MassFlow massFlow) =>
        Convert(segment, temperature, _solver.SolveOutletPressure(segment, temperature, inletPressure, massFlow));

    public SegmentResult SolveInletPressure(PipeSegment segment, Temperature temperature, Pressure outletPressure, MassFlow massFlow) =>
        Convert(segment, temperature, _solver.SolveInletPressure(segment, temperature, outletPressure, massFlow));

    public SegmentResult SolveMassFlow(PipeSegment segment, Temperature temperature, Pressure inletPressure, Pressure outletPressure) =>
        Convert(segment, temperature, _solver.SolveMassFlow(segment, temperature, inletPressure, outletPressure));

    private SegmentResult Convert(PipeSegment segment, Temperature temperature, GasFlowSolution s) => new()
    {
        Model = Name,
        Segment = segment,
        Temperature = temperature,
        InletPressure = s.InletPressure,
        OutletPressure = s.OutletPressure,
        MassFlow = s.MassFlow,
        AveragePressure = s.AveragePressure,
        AverageCompressibilityFactor = s.AverageCompressibilityFactor,
        AverageDensity = s.AverageDensity,
        Warnings = s.Warnings
    };
}

/// <summary>
/// A pipeline flow equation of the Correlations module (Weymouth,
/// Panhandle, General Flow / AGA). Mass flow is converted to standard flow
/// at the equation's base conditions with M = G·M_air and the stated base Z.
/// </summary>
public sealed class FlowEquationSegmentModel : ISegmentModel
{
    private readonly GasFlowEquationSolver _solver;
    private readonly FlowEquationGas _gas;
    private readonly StandardFlowConditions _base;

    public FlowEquationSegmentModel(IGasFlowEquation equation, FlowEquationGas gas, StandardFlowConditions baseConditions)
    {
        Guard.NotNull(equation);
        _solver = new GasFlowEquationSolver(equation, gas, baseConditions);
        _gas = gas;
        _base = baseConditions;
        Name = equation.Name;
    }

    public string Name { get; }

    public SegmentResult SolveOutletPressure(PipeSegment segment, Temperature temperature, Pressure inletPressure, MassFlow massFlow) =>
        Convert(segment, _solver.SolveOutletPressure(segment, temperature, inletPressure, ToStandard(massFlow)));

    public SegmentResult SolveInletPressure(PipeSegment segment, Temperature temperature, Pressure outletPressure, MassFlow massFlow) =>
        Convert(segment, _solver.SolveInletPressure(segment, temperature, outletPressure, ToStandard(massFlow)));

    public SegmentResult SolveMassFlow(PipeSegment segment, Temperature temperature, Pressure inletPressure, Pressure outletPressure) =>
        Convert(segment, _solver.SolveFlow(segment, temperature, inletPressure, outletPressure));

    private StandardVolumetricFlow ToStandard(MassFlow massFlow) =>
        FlowRateConverter.ConvertMassToStandardVolume(massFlow, _gas.MolarMass, _base);

    private SegmentResult Convert(PipeSegment segment, FlowEquationSolution s) => new()
    {
        Model = Name,
        Segment = segment,
        Temperature = s.FlowingTemperature,
        InletPressure = s.InletPressure,
        OutletPressure = s.OutletPressure,
        MassFlow = s.MassFlow,
        AveragePressure = s.AveragePressure,
        AverageCompressibilityFactor = s.AverageCompressibilityFactor,
        AverageDensity = Density.FromKilogramsPerCubicMeter(
            s.AveragePressure.Pascals * _gas.MolarMass.KilogramsPerMole
            / (s.AverageCompressibilityFactor * PhysicalConstants.MolarGasConstant * s.FlowingTemperature.Kelvins)),
        Warnings = s.Warnings
    };
}
