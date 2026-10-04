using UnitsNet;

namespace NaturalGasPipeline.Hydraulics;

/// <summary>
/// Converged result of an isothermal gas-pipe calculation, including every
/// intermediate quantity so the result can be audited.
/// </summary>
public sealed record GasFlowSolution
{
    public required Pressure InletPressure { get; init; }

    public required Pressure OutletPressure { get; init; }

    public required MassFlow MassFlow { get; init; }

    /// <summary>P_avg = 2/3 · (P1 + P2 − P1·P2 / (P1 + P2))</summary>
    public required Pressure AveragePressure { get; init; }

    /// <summary>Compressibility factor at P_avg and T.</summary>
    public required double AverageCompressibilityFactor { get; init; }

    public required Density AverageDensity { get; init; }

    public required DynamicViscosity AverageViscosity { get; init; }

    public required double ReynoldsNumber { get; init; }

    public required double DarcyFrictionFactor { get; init; }

    /// <summary>Name of the friction factor model that produced <see cref="DarcyFrictionFactor"/>.</summary>
    public required string FrictionModel { get; init; }

    public required Speed InletVelocity { get; init; }

    public required Speed OutletVelocity { get; init; }

    /// <summary>Elevation parameter s = 2·g·Δz·M / (Z·R·T).</summary>
    public required double ElevationParameter { get; init; }

    public required int Iterations { get; init; }

    public required IReadOnlyList<string> Warnings { get; init; }

    public Pressure PressureDrop =>
        Pressure.FromPascals(InletPressure.Pascals - OutletPressure.Pascals);
}
