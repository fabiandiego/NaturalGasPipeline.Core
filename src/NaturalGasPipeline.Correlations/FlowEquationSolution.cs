using NaturalGasPipeline.Core.Flow;
using UnitsNet;

namespace NaturalGasPipeline.Correlations;

/// <summary>Complete state of a solved segment, whatever the unknown was.</summary>
public sealed record FlowEquationSolution
{
    public required string Equation { get; init; }

    public required Pressure InletPressure { get; init; }

    public required Pressure OutletPressure { get; init; }

    public required StandardVolumetricFlow StandardFlow { get; init; }

    public required MassFlow MassFlow { get; init; }

    public required Length InternalDiameter { get; init; }

    public required Temperature FlowingTemperature { get; init; }

    public required Pressure AveragePressure { get; init; }

    public required double AverageCompressibilityFactor { get; init; }

    public required double ElevationParameter { get; init; }

    public required Length EquivalentLength { get; init; }

    /// <summary>F = 2/√f; null for equations with built-in friction (Weymouth, Panhandle).</summary>
    public required double? TransmissionFactor { get; init; }

    /// <summary>null for equations that do not use it.</summary>
    public required double? ReynoldsNumber { get; init; }

    public required int Iterations { get; init; }

    public required IReadOnlyList<string> Warnings { get; init; }

    public Pressure PressureDrop => InletPressure - OutletPressure;
}
