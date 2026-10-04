using UnitsNet;

namespace NaturalGasPipeline.Compression;

/// <summary>
/// A compressor station: N identical stages in series with equal pressure
/// ratio per stage, r_stage = r^(1/N), and optional intercooling of the gas
/// to a stated temperature between stages. Aftercooling is a separate,
/// explicit element of the system model, not part of the station.
/// </summary>
public sealed class CompressorStation
{
    public CompressorStation(
        string name,
        CompressorStage stage,
        int numberOfStages,
        Temperature? intercoolerOutletTemperature = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A name is required.", nameof(name));
        }

        if (numberOfStages < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(numberOfStages), numberOfStages, "At least one stage is required.");
        }

        if (intercoolerOutletTemperature is Temperature t && (!double.IsFinite(t.Kelvins) || t.Kelvins <= 0.0))
        {
            throw new ArgumentOutOfRangeException(nameof(intercoolerOutletTemperature), t.Kelvins, "Temperature must be positive.");
        }

        Name = name;
        Stage = stage ?? throw new ArgumentNullException(nameof(stage));
        NumberOfStages = numberOfStages;
        IntercoolerOutletTemperature = intercoolerOutletTemperature;
    }

    public string Name { get; }

    public CompressorStage Stage { get; }

    public int NumberOfStages { get; }

    /// <summary>null: no intercooling (each stage takes the previous discharge temperature).</summary>
    public Temperature? IntercoolerOutletTemperature { get; }

    public CompressorStationResult Compress(
        Pressure suctionPressure,
        Temperature suctionTemperature,
        Pressure dischargePressure,
        MassFlow massFlow)
    {
        double ratio = dischargePressure.Pascals / suctionPressure.Pascals;

        if (!double.IsFinite(ratio) || ratio < 1.0)
        {
            throw new ArgumentException("Discharge pressure must not be below suction pressure.", nameof(dischargePressure));
        }

        double stageRatio = System.Math.Pow(ratio, 1.0 / NumberOfStages);
        var stages = new List<CompressionResult>(NumberOfStages);

        Pressure p = suctionPressure;
        Temperature t = suctionTemperature;

        for (int i = 0; i < NumberOfStages; i++)
        {
            Pressure next = i == NumberOfStages - 1
                ? dischargePressure
                : Pressure.FromPascals(p.Pascals * stageRatio);

            CompressionResult stage = Stage.Compress(p, t, next, massFlow);
            stages.Add(stage);

            p = next;
            t = i < NumberOfStages - 1 && IntercoolerOutletTemperature is Temperature cooled
                ? cooled
                : stage.DischargeTemperature;
        }

        return new CompressorStationResult
        {
            Name = Name,
            Stages = stages,
            SuctionPressure = suctionPressure,
            SuctionTemperature = suctionTemperature,
            DischargePressure = dischargePressure,
            DischargeTemperature = stages[^1].DischargeTemperature,
            PressureRatio = ratio,
            GasPower = Power.FromWatts(stages.Sum(s => s.GasPower.Watts)),
            ShaftPower = Power.FromWatts(stages.Sum(s => s.ShaftPower.Watts))
        };
    }
}

public sealed record CompressorStationResult
{
    public required string Name { get; init; }

    public required IReadOnlyList<CompressionResult> Stages { get; init; }

    public required Pressure SuctionPressure { get; init; }

    public required Temperature SuctionTemperature { get; init; }

    public required Pressure DischargePressure { get; init; }

    public required Temperature DischargeTemperature { get; init; }

    public required double PressureRatio { get; init; }

    public required Power GasPower { get; init; }

    public required Power ShaftPower { get; init; }
}
