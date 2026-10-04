using UnitsNet;
using Xunit;
using Xunit.Abstractions;
using static NaturalGasPipeline.Tests.Thermodynamics.CompressibilityTestHelpers;

namespace NaturalGasPipeline.Tests.Thermodynamics;

/// <summary>
/// LEVEL 2 — model accuracy against the real gas.
///
/// Compares Peng-Robinson and SRK against NIST reference data
/// (see <see cref="NistReferenceData"/>) for 273–313 K and 1–10 MPa.
///
/// The allowed deviations below are NOT targets of the model: they record
/// the accuracy this implementation actually achieves (measured 2026-10-04).
/// - Tightening a band requires improving the model (e.g. volume
///   translation, AGA8 / GERG-2008).
/// - Loosening a band requires a documented reason; an unexpected failure
///   here means a change has degraded the physics.
///
/// Measured maximum |ΔZ/Z| (2026-10-04):
///   Methane:  PR 2.82 %  (always under-predicts Z, i.e. over-predicts ρ)
///             SRK 2.21 % (over-predicts Z at high pressure)
///   Nitrogen: PR 1.73 %, SRK 1.40 %
/// </summary>
public class CompressibilityAccuracyTests
{
    private const double R = 8.31446261815324; // J/(mol·K)

    private static double MaxAllowedRelativeError(string fluid, string eos) =>
        (fluid, eos) switch
        {
            ("Methane", "PR") => 0.030,
            ("Methane", "SRK") => 0.025,
            ("Nitrogen", "PR") => 0.020,
            ("Nitrogen", "SRK") => 0.015,
            _ => throw new ArgumentOutOfRangeException(nameof(fluid))
        };

    private readonly ITestOutputHelper _output;

    public CompressibilityAccuracyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public static IEnumerable<object[]> Cases()
    {
        foreach (object[] row in NistReferenceData.PureFluidDensities)
        {
            yield return new[] { row[0], row[1], row[2], row[3], "PR" };
            yield return new[] { row[0], row[1], row[2], row[3], "SRK" };
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Z_ShouldStayWithinDocumentedAccuracyOfNistReference(
        string fluid,
        double temperatureKelvins,
        double pressureMegapascals,
        double nistDensity,
        string eos)
    {
        // Z of the real gas from the NIST density: Z = P·M / (ρ·R·T)
        double p = pressureMegapascals * 1.0e6;
        double zReference =
            p * NistReferenceData.MolarMassOf(fluid)
            / (nistDensity * R * temperatureKelvins);

        double z = ModelFor(eos, MixtureOf(fluid)).Calculate(
            Pressure.FromMegapascals(pressureMegapascals),
            Temperature.FromKelvins(temperatureKelvins));

        double error = RelativeError(z, zReference);
        double allowed = MaxAllowedRelativeError(fluid, eos);

        _output.WriteLine($"{fluid}, {eos}, T = {temperatureKelvins} K, P = {pressureMegapascals} MPa");
        _output.WriteLine($"Z NIST:  {zReference:F6}");
        _output.WriteLine($"Z model: {z:F6}");
        _output.WriteLine($"Deviation: {error * 100:F2} % (allowed ±{allowed * 100:F1} %)");

        Assert.InRange(error, -allowed, allowed);
    }

    [Fact]
    public void PengRobinson_ShouldUnderPredictMethaneZ()
    {
        // Documents the known bias of PR for methane: Z too low, density too high.
        // If this ever fails, the model has changed and the bands above must be re-measured.
        foreach (object[] row in NistReferenceData.PureFluidDensities)
        {
            if ((string)row[0] != "Methane")
            {
                continue;
            }

            double t = (double)row[1];
            double pMpa = (double)row[2];
            double rho = (double)row[3];

            double zReference =
                pMpa * 1.0e6 * NistReferenceData.MethaneMolarMass / (rho * R * t);

            double z = ModelFor("PR", MixtureOf("Methane")).Calculate(
                Pressure.FromMegapascals(pMpa),
                Temperature.FromKelvins(t));

            _output.WriteLine($"T = {t} K, P = {pMpa} MPa: Z_PR - Z_NIST = {z - zReference:+0.000000;-0.000000}");

            Assert.True(z < zReference);
        }
    }
}
