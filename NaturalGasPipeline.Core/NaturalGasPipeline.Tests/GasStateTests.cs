using UnitsNet;
using Xunit;
using Xunit.Abstractions;
using NaturalGasPipeline.Core.Thermodynamics;

namespace NaturalGasPipeline.Tests.Thermodynamics;

public class GasStateTests
{
    private readonly ITestOutputHelper _output;

    public GasStateTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void GasState_ShouldCalculateIdealGasCompressibility()
    {
        var state = new GasState
        {
            Pressure =
                Pressure.FromPascals(1_000_000),

            Temperature =
                Temperature.FromKelvins(288.15),

            Mixture =
                new GasMixture
                {
                    Components =
                    {
                        StandardGasComponents.Methane
                    }
                }
        };

        var model = new IdealGasModel();

        state.CalculateState(model);

        _output.WriteLine(
            $"Molar Mass = {state.MolarMass.KilogramsPerMole} kg/mol");

        _output.WriteLine(
            $"Z = {state.CompressibilityFactor}");

        _output.WriteLine(
            $"Density = " +
            $"{state.Density.KilogramsPerCubicMeter} kg/m³");

        Assert.Equal(
            1.0,
            state.CompressibilityFactor,
            10);
    }

    [Fact]
    public void GasState_ShouldCalculateMethanePengRobinson()
    {
        var mixture =
            new GasMixture
            {
                Components =
                {
                    StandardGasComponents.Methane
                }
            };

        var state = new GasState
        {
            Pressure =
                Pressure.FromPascals(7_000_000),

            Temperature =
                Temperature.FromKelvins(288.15),

            Mixture = mixture
        };

        var model =
            new PengRobinsonModel(mixture);

        state.CalculateState(model);

        _output.WriteLine(
            $"Molar Mass = {state.MolarMass.KilogramsPerMole} kg/mol");

        _output.WriteLine(
            $"Z = {state.CompressibilityFactor}");

        _output.WriteLine(
            $"Density = " +
            $"{state.Density.KilogramsPerCubicMeter} kg/m³");

        // Independent Python implementation of PR (numpy.roots);
        // ρ = P·M / (Z·R·T) with M = 0.016043 kg/mol.
        Assert.Equal(
            0.8492349032782932,
            state.CompressibilityFactor,
            9);

        Assert.Equal(
            55.195411984408054,
            state.Density.KilogramsPerCubicMeter,
            6);
    }

    [Fact]
    public void GasState_ShouldCalculateMethaneDensity()
    {
        var mixture =
            new GasMixture
            {
                Components =
                {
                    StandardGasComponents.Methane
                }
            };

        var state = new GasState
        {
            Pressure =
                Pressure.FromPascals(7_000_000),

            Temperature =
                Temperature.FromKelvins(288.15),

            Mixture = mixture
        };

        var model =
            new PengRobinsonModel(mixture);

        state.CalculateState(model);

        _output.WriteLine(
            $"Z = {state.CompressibilityFactor}");

        _output.WriteLine(
            $"Density = " +
            $"{state.Density.KilogramsPerCubicMeter} kg/m³");

        Assert.True(
            state.Density.KilogramsPerCubicMeter > 0);
    }

    [Fact]
    public void GasState_ShouldCalculateMixtureMolarMass()
    {
        var mixture =
            new GasMixture
            {
                Components =
                {
                    new GasComponent
                    {
                        Name = "Methane",
                        MoleFraction = 0.90,
                        MolarMass = MolarMass.FromKilogramsPerMole(0.016043),
                        CriticalTemperature =
                            Temperature.FromKelvins(190.56),
                        CriticalPressure =
                            Pressure.FromPascals(4_599_000),
                        AcentricFactor = 0.01142
                    },

                    new GasComponent
                    {
                        Name = "Ethane",
                        MoleFraction = 0.10,
                        MolarMass = MolarMass.FromKilogramsPerMole(0.030070),
                        CriticalTemperature =
                            Temperature.FromKelvins(305.32),
                        CriticalPressure =
                            Pressure.FromPascals(4_872_000),
                        AcentricFactor = 0.0995
                    }
                }
            };

        var state = new GasState
        {
            Pressure =
                Pressure.FromPascals(7_000_000),

            Temperature =
                Temperature.FromKelvins(288.15),

            Mixture = mixture
        };

        state.CalculateMolarMass();

        _output.WriteLine(
            $"Molar Mass = {state.MolarMass.KilogramsPerMole} kg/mol");

        Assert.Equal(
            0.0174457,
            state.MolarMass.KilogramsPerMole,
            7);
    }

    [Fact]
    public void GasState_ShouldCalculateMixtureDensity()
    {
        var mixture =
            new GasMixture
            {
                Components =
                {
                    new GasComponent
                    {
                        Name = "Methane",
                        MoleFraction = 0.90,
                        MolarMass = MolarMass.FromKilogramsPerMole(0.016043),
                        CriticalTemperature =
                            Temperature.FromKelvins(190.56),
                        CriticalPressure =
                            Pressure.FromPascals(4_599_000),
                        AcentricFactor = 0.01142
                    },

                    new GasComponent
                    {
                        Name = "Ethane",
                        MoleFraction = 0.10,
                        MolarMass = MolarMass.FromKilogramsPerMole(0.030070),
                        CriticalTemperature =
                            Temperature.FromKelvins(305.32),
                        CriticalPressure =
                            Pressure.FromPascals(4_872_000),
                        AcentricFactor = 0.0995
                    }
                }
            };

        var state = new GasState
        {
            Pressure =
                Pressure.FromPascals(7_000_000),

            Temperature =
                Temperature.FromKelvins(288.15),

            Mixture = mixture
        };

        var model =
            new PengRobinsonModel(mixture);

        state.CalculateState(model);

        _output.WriteLine(
            $"Molar Mass = {state.MolarMass.KilogramsPerMole} kg/mol");

        _output.WriteLine(
            $"Z = {state.CompressibilityFactor}");

        _output.WriteLine(
            $"Density = " +
            $"{state.Density.KilogramsPerCubicMeter} kg/m³");

        Assert.True(
            state.Density.KilogramsPerCubicMeter > 0);
    }

    [Fact]
    public void GasState_ShouldCalculateMixturePengRobinson()
    {
        var mixture =
            new GasMixture
            {
                Components =
                {
                    new GasComponent
                    {
                        Name = "Methane",
                        MoleFraction = 0.90,
                        MolarMass = MolarMass.FromKilogramsPerMole(0.016043),
                        CriticalTemperature =
                            Temperature.FromKelvins(190.56),
                        CriticalPressure =
                            Pressure.FromPascals(4_599_000),
                        AcentricFactor = 0.01142
                    },

                    new GasComponent
                    {
                        Name = "Ethane",
                        MoleFraction = 0.10,
                        MolarMass = MolarMass.FromKilogramsPerMole(0.030070),
                        CriticalTemperature =
                            Temperature.FromKelvins(305.32),
                        CriticalPressure =
                            Pressure.FromPascals(4_872_000),
                        AcentricFactor = 0.0995
                    }
                }
            };

        var state = new GasState
        {
            Pressure =
                Pressure.FromPascals(7_000_000),

            Temperature =
                Temperature.FromKelvins(288.15),

            Mixture = mixture
        };

        var model =
            new PengRobinsonModel(mixture);

        state.CalculateState(model);

        _output.WriteLine(
            $"Z = {state.CompressibilityFactor}");

        Assert.True(
            state.CompressibilityFactor > 0);
    }

    [Fact]
    public void GasState_ShouldCalculateTypicalNaturalGas()
    {
        var mixture =
            NaturalGasMixtures.TypicalNaturalGas;

        var state = new GasState
        {
            Pressure =
                Pressure.FromPascals(7_000_000),

            Temperature =
                Temperature.FromKelvins(288.15),

            Mixture = mixture
        };

        var model =
            new PengRobinsonModel(mixture);

        state.CalculateState(model);

        _output.WriteLine(
            $"Molar Mass = {state.MolarMass.KilogramsPerMole} kg/mol");

        _output.WriteLine(
            $"Critical Temperature = " +
            $"{state.CriticalTemperature.Kelvins} K");

        _output.WriteLine(
            $"Critical Pressure = " +
            $"{state.CriticalPressure.Megapascals} MPa");

        _output.WriteLine(
            $"Reduced Temperature = " +
            $"{state.ReducedTemperature}");

        _output.WriteLine(
            $"Reduced Pressure = " +
            $"{state.ReducedPressure}");

        _output.WriteLine(
            $"Z = {state.CompressibilityFactor}");

        _output.WriteLine(
            $"Density = " +
            $"{state.Density.KilogramsPerCubicMeter} kg/m³");

        Assert.True(
            state.MolarMass.KilogramsPerMole > 0);

        Assert.True(
            state.CriticalTemperature.Kelvins > 0);

        Assert.True(
            state.CriticalPressure.Pascals > 0);

        Assert.True(
            state.ReducedTemperature > 0);

        Assert.True(
            state.ReducedPressure > 0);

        Assert.True(
            state.CompressibilityFactor > 0);

        Assert.True(
            state.Density.KilogramsPerCubicMeter > 0);
    }
    [Fact]
    public void GasState_ShouldComparePengRobinsonAndSRK()
    {
        var mixture =
            NaturalGasMixtures.TypicalNaturalGas;

        var pressure =
            Pressure.FromPascals(7_000_000);

        var temperature =
            Temperature.FromKelvins(288.15);

        var prModel =
            new PengRobinsonModel(mixture);

        var srkModel =
            new SoaveRedlichKwongModel(mixture);

        var prState = new GasState
        {
            Pressure = pressure,
            Temperature = temperature,
            Mixture = mixture
        };

        var srkState = new GasState
        {
            Pressure = pressure,
            Temperature = temperature,
            Mixture = mixture
        };

        prState.CalculateState(prModel);

        srkState.CalculateState(srkModel);

        double zDifference =
            srkState.CompressibilityFactor -
            prState.CompressibilityFactor;

        double densityDifference =
            srkState.Density.KilogramsPerCubicMeter -
            prState.Density.KilogramsPerCubicMeter;

        double zDifferencePercent =
            zDifference /
            prState.CompressibilityFactor
            * 100.0;

        double densityDifferencePercent =
            densityDifference /
            prState.Density.KilogramsPerCubicMeter
            * 100.0;

        _output.WriteLine(
            "=== Peng-Robinson ===");

        _output.WriteLine(
            $"Z = {prState.CompressibilityFactor}");

        _output.WriteLine(
            $"Density = " +
            $"{prState.Density.KilogramsPerCubicMeter} kg/m³");

        _output.WriteLine(
            "");

        _output.WriteLine(
            "=== Soave-Redlich-Kwong ===");

        _output.WriteLine(
            $"Z = {srkState.CompressibilityFactor}");

        _output.WriteLine(
            $"Density = " +
            $"{srkState.Density.KilogramsPerCubicMeter} kg/m³");

        _output.WriteLine(
            "");

        _output.WriteLine(
            "=== Difference SRK - PR ===");

        _output.WriteLine(
            $"ΔZ = {zDifference}");

        _output.WriteLine(
            $"ΔZ = {zDifferencePercent}%");

        _output.WriteLine(
            $"ΔDensity = {densityDifference} kg/m³");

        _output.WriteLine(
            $"ΔDensity = {densityDifferencePercent}%");

        // Independent Python implementation (numpy.roots), kij = 0.
        Assert.Equal(0.8162522850766638, prState.CompressibilityFactor, 9);
        Assert.Equal(0.8473996626963167, srkState.CompressibilityFactor, 9);
    }
    [Fact]
    public void GasState_ShouldCalculateUsingSelectedModel()
    {
        var mixture =
            NaturalGasMixtures.TypicalNaturalGas;

        var statePR = new GasState
        {
            Pressure =
                Pressure.FromPascals(7_000_000),

            Temperature =
                Temperature.FromKelvins(288.15),

            Mixture = mixture
        };

        var stateSRK = new GasState
        {
            Pressure =
                Pressure.FromPascals(7_000_000),

            Temperature =
                Temperature.FromKelvins(288.15),

            Mixture = mixture
        };

        statePR.CalculateState(
            CompressibilityModelType.PengRobinson);

        stateSRK.CalculateState(
            CompressibilityModelType.SoaveRedlichKwong);

        _output.WriteLine(
            "=== Selected Models ===");

        _output.WriteLine(
            $"Peng-Robinson Z = " +
            $"{statePR.CompressibilityFactor}");

        _output.WriteLine(
            $"SRK Z = " +
            $"{stateSRK.CompressibilityFactor}");

        Assert.True(
            statePR.CompressibilityFactor > 0);

        Assert.True(
            stateSRK.CompressibilityFactor > 0);
    }

    [Fact]
    public void CalculateDensity_WithoutCompressibility_ShouldThrow()
    {
        // Previously this silently produced an infinite density (Z = 0).
        var state = new GasState
        {
            Pressure = Pressure.FromPascals(7_000_000),
            Temperature = Temperature.FromKelvins(288.15),
            Mixture = NaturalGasMixtures.TypicalNaturalGas
        };

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

    [Fact]
    public void IdealGasDensity_ShouldMatchHandCalculation()
    {
        // ρ = P·M / (R·T) = 1e6 · 0.016043 / (8.31446261815324 · 288.15)
        const double expected = 6.696267194;

        var state = new GasState
        {
            Pressure = Pressure.FromPascals(1_000_000),
            Temperature = Temperature.FromKelvins(288.15),
            Mixture = new GasMixture
            {
                Components = { StandardGasComponents.Methane }
            }
        };

        state.CalculateState(new IdealGasModel());

        _output.WriteLine(
            $"Density = {state.Density.KilogramsPerCubicMeter:F7} kg/m³");

        Assert.Equal(
            expected,
            state.Density.KilogramsPerCubicMeter,
            6);
    }
}
