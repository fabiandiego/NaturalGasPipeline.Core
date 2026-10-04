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
}

/// <summary>State of one solved segment.</summary>
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

    public required IReadOnlyList<string> Warnings { get; init; }

    public Pressure PressureDrop => InletPressure - OutletPressure;
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
        ArgumentNullException.ThrowIfNull(equation);
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
        Warnings = s.Warnings
    };
}
