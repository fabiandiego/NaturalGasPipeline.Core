using UnitsNet;

namespace NaturalGasPipeline.Core.Gas;

/// <summary>
/// Immutable snapshot of the gas properties at one state (P, T).
///
/// Produced by the thermodynamic layer (GasState.ToGasProperties) and meant
/// to be passed between modules or to a front end without exposing the
/// calculation objects. A null viscosity means it was not calculated.
/// </summary>
public sealed record GasProperties
{
    public required Temperature Temperature { get; init; }

    public required Pressure Pressure { get; init; }

    public required MolarMass MolarMass { get; init; }

    public required double CompressibilityFactor { get; init; }

    public required Density Density { get; init; }

    public DynamicViscosity? Viscosity { get; init; }
}
