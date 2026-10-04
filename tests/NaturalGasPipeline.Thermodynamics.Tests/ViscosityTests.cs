using NaturalGasPipeline.Thermodynamics;
using UnitsNet;
using Xunit;
using Xunit.Abstractions;

namespace NaturalGasPipeline.Thermodynamics.Tests;

/// <summary>
/// Viscosity models.
///
/// Level 1 (implementation): Lee-Gonzalez-Eakin against an independent
/// Python implementation, given the same T, ρ and M.
///
/// Level 2 (accuracy): LGE fed with the NIST density, compared with the
/// NIST viscosity, so the error belongs to the viscosity correlation only.
/// Measured 2026-10-04 for methane, 273–313 K, 1–10 MPa:
/// +1.19 % to +4.08 % (always over-predicts).
///
/// Engineering relevance: in turbulent pipeline flow μ only enters through
/// Re, and f depends weakly on Re (Re ≈ 1e7 here), so a 4 % viscosity
/// error changes the pressure drop by well under 1 %.
/// </summary>
public class ViscosityTests
{
    private const double MethaneMolarMass = 0.016043; // kg/mol, StandardGasComponents

    private readonly ITestOutputHelper _output;

    public ViscosityTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [InlineData(288.15, 53.79515, 1.2653239009243092e-05)]
    [InlineData(273.15, 90.04018, 1.3784283692821339e-05)]
    [InlineData(313.15, 6.250718, 1.2154950043803837e-05)]
    public void LeeGonzalezEakin_ShouldMatchIndependentImplementation(
        double temperatureKelvins,
        double densityKgPerM3,
        double expectedPascalSeconds)
    {
        var model = new LeeGonzalezEakinViscosityModel();

        double mu = model.Calculate(
            Temperature.FromKelvins(temperatureKelvins),
            Density.FromKilogramsPerCubicMeter(densityKgPerM3),
            MolarMass.FromKilogramsPerMole(MethaneMolarMass)).PascalSeconds;

        double error = System.Math.Abs(mu - expectedPascalSeconds) / expectedPascalSeconds;

        _output.WriteLine($"T = {temperatureKelvins} K, ρ = {densityKgPerM3} kg/m³");
        _output.WriteLine($"μ calculated: {mu:R} Pa·s");
        _output.WriteLine($"μ reference:  {expectedPascalSeconds:R} Pa·s");

        Assert.True(error < 1e-12, $"Relative error {error:E2}");
    }

    public static IEnumerable<object[]> MethaneViscosities() =>
        NistReferenceData.PureFluidViscosities.Where(r => (string)r[0] == "Methane");

    [Theory]
    [MemberData(nameof(MethaneViscosities))]
    public void LeeGonzalezEakin_Methane_ShouldStayWithinDocumentedAccuracyOfNist(
        string fluid,
        double temperatureKelvins,
        double pressureMegapascals,
        double nistDensity,
        double nistViscosityMicroPascalSeconds)
    {
        const double maxAllowedRelativeError = 0.05; // measured max +4.08 %

        double mu = new LeeGonzalezEakinViscosityModel().Calculate(
            Temperature.FromKelvins(temperatureKelvins),
            Density.FromKilogramsPerCubicMeter(nistDensity),
            MolarMass.FromKilogramsPerMole(NistReferenceData.MolarMassOf(fluid)))
            .MicropascalSeconds;

        double error = (mu - nistViscosityMicroPascalSeconds) / nistViscosityMicroPascalSeconds;

        _output.WriteLine($"{fluid}, T = {temperatureKelvins} K, P = {pressureMegapascals} MPa");
        _output.WriteLine($"μ NIST: {nistViscosityMicroPascalSeconds:F4} µPa·s");
        _output.WriteLine($"μ LGE:  {mu:F4} µPa·s");
        _output.WriteLine($"Deviation: {error * 100:F2} % (allowed ±{maxAllowedRelativeError * 100:F0} %)");

        Assert.InRange(error, -maxAllowedRelativeError, maxAllowedRelativeError);

        // Known bias: LGE over-predicts methane viscosity in this range.
        Assert.True(error > 0.0);
    }

    [Fact]
    public void LeeGonzalezEakin_IsNotValidForNitrogen()
    {
        // Documents a limitation, not a goal: LGE is a hydrocarbon-gas
        // correlation. If this test ever fails, the model changed and the
        // XML documentation of LeeGonzalezEakinViscosityModel must be revised.
        foreach (object[] row in NistReferenceData.PureFluidViscosities)
        {
            if ((string)row[0] != "Nitrogen")
            {
                continue;
            }

            double t = (double)row[1];
            double rho = (double)row[3];
            double nist = (double)row[4];

            double mu = new LeeGonzalezEakinViscosityModel().Calculate(
                Temperature.FromKelvins(t),
                Density.FromKilogramsPerCubicMeter(rho),
                MolarMass.FromKilogramsPerMole(NistReferenceData.NitrogenMolarMass))
                .MicropascalSeconds;

            double error = (mu - nist) / nist;

            _output.WriteLine($"N₂ at {row[2]} MPa: LGE {mu:F3} vs NIST {nist:F3} µPa·s ({error * 100:F1} %)");

            Assert.True(error < -0.25);
        }
    }

    [Fact]
    public void GasState_ShouldCalculateViscosityFromItsOwnDensity()
    {
        // Full chain: PR density (55.1954 kg/m³) → LGE viscosity.
        // Independent Python value: 1.2708293728790203e-05 Pa·s,
        // i.e. +2.66 % vs NIST (12.37903 µPa·s), combining PR and LGE errors.
        const double expected = 1.2708293728790203e-05;

        var state = new GasState
        {
            Pressure = Pressure.FromMegapascals(7.0),
            Temperature = Temperature.FromKelvins(288.15),
            Mixture = new GasMixture { Components = { StandardGasComponents.Methane } }
        };

        state.CalculateState(CompressibilityModelType.PengRobinson);
        state.CalculateViscosity(new LeeGonzalezEakinViscosityModel());

        _output.WriteLine($"Density:   {state.Density.KilogramsPerCubicMeter:F4} kg/m³");
        _output.WriteLine($"Viscosity: {state.Viscosity.MicropascalSeconds:F4} µPa·s");

        double error =
            System.Math.Abs(state.Viscosity.PascalSeconds - expected) / expected;

        Assert.True(error < 1e-9, $"Relative error {error:E2}");
    }

    [Fact]
    public void GasState_CalculateViscosityBeforeDensity_ShouldThrow()
    {
        var state = new GasState
        {
            Pressure = Pressure.FromMegapascals(7.0),
            Temperature = Temperature.FromKelvins(288.15),
            Mixture = NaturalGasMixtures.TypicalNaturalGas
        };

        Assert.Throws<InvalidOperationException>(
            () => state.CalculateViscosity(new LeeGonzalezEakinViscosityModel()));
    }

    [Fact]
    public void ConstantViscosityModel_ShouldRejectZero()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ConstantViscosityModel(DynamicViscosity.Zero));
    }
}
