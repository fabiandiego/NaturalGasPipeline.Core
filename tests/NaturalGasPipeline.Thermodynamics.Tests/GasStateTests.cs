using NaturalGasPipeline.Core.Gas;
using NaturalGasPipeline.Thermodynamics;
using UnitsNet;
using Xunit;
using Xunit.Abstractions;

namespace NaturalGasPipeline.Thermodynamics.Tests;

/// <summary>
/// GasState: the object that ties composition, Z model and density together.
/// Reference values come from an independent Python implementation
/// (numpy.roots for the cubic EOS, kij = 0) unless stated otherwise.
/// </summary>
public class GasStateTests
{
    private const double R = 8.31446261815324; // J/(mol·K)

    private readonly ITestOutputHelper _output;

    public GasStateTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static GasMixture MethaneEthane90_10() => new()
    {
        Components =
        {
            new GasComponent
            {
                Name = "Methane",
                MoleFraction = 0.90,
                MolarMass = MolarMass.FromKilogramsPerMole(0.016043),
                CriticalTemperature = Temperature.FromKelvins(190.56),
                CriticalPressure = Pressure.FromPascals(4_599_000),
                AcentricFactor = 0.01142
            },
            new GasComponent
            {
                Name = "Ethane",
                MoleFraction = 0.10,
                MolarMass = MolarMass.FromKilogramsPerMole(0.030070),
                CriticalTemperature = Temperature.FromKelvins(305.32),
                CriticalPressure = Pressure.FromPascals(4_872_000),
                AcentricFactor = 0.0995
            }
        }
    };

    private static GasState TypicalGasAt7MPa() => new()
    {
        Pressure = Pressure.FromMegapascals(7.0),
        Temperature = Temperature.FromKelvins(288.15),
        Mixture = NaturalGasMixtures.TypicalNaturalGas
    };

    private void Print(GasState state)
    {
        _output.WriteLine($"P = {state.Pressure.Megapascals} MPa, T = {state.Temperature.Kelvins} K");
        _output.WriteLine($"M = {state.MolarMass.KilogramsPerMole:R} kg/mol");
        _output.WriteLine($"Tpc = {state.CriticalTemperature.Kelvins:R} K, Ppc = {state.CriticalPressure.Pascals:R} Pa");
        _output.WriteLine($"Tr = {state.ReducedTemperature:R}, Pr = {state.ReducedPressure:R}");
        _output.WriteLine($"Z = {state.CompressibilityFactor:R}");
        _output.WriteLine($"ρ = {state.Density.KilogramsPerCubicMeter:R} kg/m³");
    }

    // ------------------------------------------------------------ values

    [Fact]
    public void IdealGasDensity_ShouldMatchHandCalculation()
    {
        // ρ = P·M / (R·T) = 1e6 · 0.016043 / (8.31446261815324 · 288.15)
        const double expected = 6.696267194;

        var state = new GasState
        {
            Pressure = Pressure.FromPascals(1_000_000),
            Temperature = Temperature.FromKelvins(288.15),
            Mixture = new GasMixture { Components = { StandardGasComponents.Methane } }
        };

        state.CalculateState(new IdealGasModel());

        Print(state);

        Assert.Equal(1.0, state.CompressibilityFactor);
        Assert.Equal(expected, state.Density.KilogramsPerCubicMeter, 6);
    }

    [Fact]
    public void MethanePengRobinson_ShouldMatchReferenceZAndDensity()
    {
        var mixture = new GasMixture { Components = { StandardGasComponents.Methane } };

        var state = new GasState
        {
            Pressure = Pressure.FromPascals(7_000_000),
            Temperature = Temperature.FromKelvins(288.15),
            Mixture = mixture
        };

        state.CalculateState(new PengRobinsonModel(mixture));

        Print(state);

        // ρ = P·M / (Z·R·T) with M = 0.016043 kg/mol
        Assert.Equal(0.8492349032782932, state.CompressibilityFactor, 9);
        Assert.Equal(55.195411984408054, state.Density.KilogramsPerCubicMeter, 6);
    }

    [Fact]
    public void MixtureMolarMass_ShouldMatchHandCalculation()
    {
        var state = new GasState
        {
            Pressure = Pressure.FromPascals(7_000_000),
            Temperature = Temperature.FromKelvins(288.15),
            Mixture = MethaneEthane90_10()
        };

        state.CalculateMolarMass();

        _output.WriteLine($"M = {state.MolarMass.KilogramsPerMole:R} kg/mol");

        // 0.90 · 0.016043 + 0.10 · 0.030070
        Assert.Equal(0.0174457, state.MolarMass.KilogramsPerMole, 7);
    }

    [Fact]
    public void TypicalNaturalGas_ShouldMatchReferenceState()
    {
        GasState state = TypicalGasAt7MPa();

        state.CalculateState(new PengRobinsonModel(NaturalGasMixtures.TypicalNaturalGas));

        Print(state);

        // Composition-weighted values (Kay's rule for the pseudo-criticals).
        Assert.Equal(0.0177650885, state.MolarMass.KilogramsPerMole, 10);
        Assert.Equal(200.63345, state.CriticalTemperature.Kelvins, 6);
        Assert.Equal(4_604_203.5, state.CriticalPressure.Pascals, 3);
        Assert.Equal(1.4362011917753494, state.ReducedTemperature, 10);
        Assert.Equal(1.520349828151601, state.ReducedPressure, 10);

        // Peng-Robinson with van der Waals mixing rules, kij = 0.
        Assert.Equal(0.8162522850766638, state.CompressibilityFactor, 9);
        Assert.Equal(63.5899079473415, state.Density.KilogramsPerCubicMeter, 6);
    }

    [Fact]
    public void ModelSelectedByType_ShouldMatchModelPassedDirectly()
    {
        // CalculateState(CompressibilityModelType) goes through the factory;
        // it must give exactly the same Z as passing the model instance.
        GasState byTypePR = TypicalGasAt7MPa();
        GasState byTypeSRK = TypicalGasAt7MPa();

        byTypePR.CalculateState(CompressibilityModelType.PengRobinson);
        byTypeSRK.CalculateState(CompressibilityModelType.SoaveRedlichKwong);

        _output.WriteLine($"PR  Z = {byTypePR.CompressibilityFactor:R}");
        _output.WriteLine($"SRK Z = {byTypeSRK.CompressibilityFactor:R}");
        _output.WriteLine(
            $"SRK − PR: ΔZ = {(byTypeSRK.CompressibilityFactor - byTypePR.CompressibilityFactor) / byTypePR.CompressibilityFactor * 100:F2} %");

        Assert.Equal(0.8162522850766638, byTypePR.CompressibilityFactor, 9);
        Assert.Equal(0.8473996626963167, byTypeSRK.CompressibilityFactor, 9);
    }

    [Fact]
    public void Density_ShouldBeConsistentWithZ()
    {
        // ρ·Z·R·T / (P·M) = 1 for any model: checks the density formula itself.
        GasState state = TypicalGasAt7MPa();
        state.CalculateState(CompressibilityModelType.SoaveRedlichKwong);

        double identity =
            state.Density.KilogramsPerCubicMeter
            * state.CompressibilityFactor * R * state.Temperature.Kelvins
            / (state.Pressure.Pascals * state.MolarMass.KilogramsPerMole);

        _output.WriteLine($"ρ·Z·R·T / (P·M) = {identity:R}");

        Assert.Equal(1.0, identity, 12);
    }

    [Fact]
    public void ToGasProperties_ShouldSnapshotTheCalculatedState()
    {
        GasState state = TypicalGasAt7MPa();
        state.CalculateState(CompressibilityModelType.PengRobinson);

        GasProperties withoutViscosity = state.ToGasProperties();

        state.CalculateViscosity(new LeeGonzalezEakinViscosityModel());

        GasProperties withViscosity = state.ToGasProperties();

        _output.WriteLine(withViscosity.ToString());

        Assert.Equal(state.CompressibilityFactor, withViscosity.CompressibilityFactor);
        Assert.Equal(state.Density, withViscosity.Density);
        Assert.Equal(state.MolarMass, withViscosity.MolarMass);
        Assert.Null(withoutViscosity.Viscosity);
        Assert.NotNull(withViscosity.Viscosity);
    }

    [Fact]
    public void ToGasProperties_BeforeCalculation_ShouldThrow()
    {
        Assert.Throws<InvalidOperationException>(
            () => TypicalGasAt7MPa().ToGasProperties());
    }

    // ------------------------------------------------------------ guards

    [Fact]
    public void CalculateDensity_WithoutCompressibility_ShouldThrow()
    {
        // Previously this silently produced an infinite density (Z = 0).
        GasState state = TypicalGasAt7MPa();

        var exception =
            Assert.Throws<InvalidOperationException>(
                () => state.CalculateDensity());

        _output.WriteLine(exception.Message);
    }

    [Fact]
    public void CalculateDensity_WithoutMixture_ShouldThrow()
    {
        // Previously this silently produced a density of 0 (M = 0).
        var state = new GasState
        {
            Pressure = Pressure.FromPascals(7_000_000),
            Temperature = Temperature.FromKelvins(288.15)
        };

        state.CalculateCompressibility(new IdealGasModel());

        var exception =
            Assert.Throws<InvalidOperationException>(
                () => state.CalculateDensity());

        _output.WriteLine(exception.Message);
    }

    [Theory]
    [InlineData(0.0, 288.15)]
    [InlineData(-1.0, 288.15)]
    [InlineData(7_000_000.0, 0.0)]
    public void CalculateState_WithNonPhysicalPressureOrTemperature_ShouldThrow(
        double pressurePascals,
        double temperatureKelvins)
    {
        var state = new GasState
        {
            Pressure = Pressure.FromPascals(pressurePascals),
            Temperature = Temperature.FromKelvins(temperatureKelvins),
            Mixture = NaturalGasMixtures.TypicalNaturalGas
        };

        var exception =
            Assert.Throws<InvalidOperationException>(
                () => state.CalculateState(
                    CompressibilityModelType.PengRobinson));

        _output.WriteLine(exception.Message);
    }
}
