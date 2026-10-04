using UnitsNet;
using Xunit;
using Xunit.Abstractions;
using static NaturalGasPipeline.Thermodynamics.Tests.CompressibilityTestHelpers;

namespace NaturalGasPipeline.Thermodynamics.Tests;

/// <summary>
/// LEVEL 2 — corresponding-states correlations against the real gas.
///
/// NIST reference data (see <see cref="NistReferenceData"/>) for pure
/// methane and nitrogen, 273–313 K, 1–10 MPa. For a pure fluid Kay's
/// pseudo-criticals are the true critical point, so this measures the
/// correlations themselves, without characterization error.
///
/// Measured maximum |ΔZ/Z| (2026-10-04); all under-predict Z:
///   Methane  (Tpr 1.43–1.64, Ppr ≤ 2.2):  DAK 1.49 %, HY 1.52 %, Papay 1.40 %
///   Nitrogen (Tpr 2.28,      Ppr ≤ 2.9):  DAK 2.42 %, HY 2.03 %, Papay 2.44 %
/// For comparison, Peng-Robinson: methane 2.82 %, nitrogen 1.73 %
/// (<see cref="CompressibilityAccuracyTests"/>). The bands record what is
/// achieved; changing one needs a documented reason.
/// </summary>
public class CorrespondingStatesAccuracyTests
{
    private const double R = 8.31446261815324; // J/(mol·K)

    private readonly ITestOutputHelper _output;

    public CorrespondingStatesAccuracyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static double MaxAllowedRelativeError(string fluid, CompressibilityModelType model) =>
        (fluid, model) switch
        {
            ("Methane", CompressibilityModelType.DranchukAbouKassem) => 0.016,
            ("Methane", CompressibilityModelType.HallYarborough) => 0.016,
            ("Methane", CompressibilityModelType.Papay) => 0.015,
            ("Nitrogen", CompressibilityModelType.DranchukAbouKassem) => 0.025,
            ("Nitrogen", CompressibilityModelType.HallYarborough) => 0.021,
            ("Nitrogen", CompressibilityModelType.Papay) => 0.025,
            _ => throw new ArgumentOutOfRangeException(nameof(fluid))
        };

    public static IEnumerable<object[]> Cases()
    {
        foreach (object[] row in NistReferenceData.PureFluidDensities)
        {
            yield return new[] { row[0], row[1], row[2], row[3], CompressibilityModelType.DranchukAbouKassem };
            yield return new[] { row[0], row[1], row[2], row[3], CompressibilityModelType.HallYarborough };
            yield return new[] { row[0], row[1], row[2], row[3], CompressibilityModelType.Papay };
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Z_ShouldStayWithinDocumentedAccuracyOfNistReference(
        string fluid,
        double temperatureKelvins,
        double pressureMegapascals,
        double nistDensity,
        CompressibilityModelType modelType)
    {
        double p = pressureMegapascals * 1.0e6;
        double zReference =
            p * NistReferenceData.MolarMassOf(fluid)
            / (nistDensity * R * temperatureKelvins);

        var model = (CorrespondingStatesCompressibilityModel)CompressibilityModelFactory.Create(modelType, MixtureOf(fluid));

        var pressure = Pressure.FromMegapascals(pressureMegapascals);
        var temperature = Temperature.FromKelvins(temperatureKelvins);

        double z = model.Calculate(pressure, temperature);
        double error = RelativeError(z, zReference);
        double allowed = MaxAllowedRelativeError(fluid, modelType);

        _output.WriteLine($"{fluid}, {model.Name}, T = {temperatureKelvins} K, P = {pressureMegapascals} MPa");
        _output.WriteLine($"Tpr = {model.PseudoCritical.ReducedTemperature(temperature):F4}, Ppr = {model.PseudoCritical.ReducedPressure(pressure):F4}");
        _output.WriteLine($"Z NIST:  {zReference:F6}");
        _output.WriteLine($"Z model: {z:F6}");
        _output.WriteLine($"Error:   {error * 100:+0.000;-0.000} % (allowed ±{allowed * 100:F1} %)");

        Assert.True(model.IsWithinValidatedRange(pressure, temperature), "Reference point outside the model range.");
        Assert.InRange(error, -allowed, allowed);
    }
}
