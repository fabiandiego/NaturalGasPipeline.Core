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
            $"Molar Mass = {state.MolarMass} kg/mol");

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
            $"Molar Mass = {state.MolarMass} kg/mol");

        _output.WriteLine(
            $"Z = {state.CompressibilityFactor}");

        _output.WriteLine(
            $"Density = " +
            $"{state.Density.KilogramsPerCubicMeter} kg/m³");

        Assert.True(
            state.CompressibilityFactor > 0);

        Assert.True(
            state.Density.KilogramsPerCubicMeter > 0);
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
                        MolarMass = 0.016043,
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
                        MolarMass = 0.030070,
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
            $"Molar Mass = {state.MolarMass} kg/mol");

        Assert.Equal(
            0.0174457,
            state.MolarMass,
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
                        MolarMass = 0.016043,
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
                        MolarMass = 0.030070,
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
            $"Molar Mass = {state.MolarMass} kg/mol");

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
                        MolarMass = 0.016043,
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
                        MolarMass = 0.030070,
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
            $"Molar Mass = {state.MolarMass} kg/mol");

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
            state.MolarMass > 0);

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

        Assert.True(
            prState.CompressibilityFactor > 0);

        Assert.True(
            srkState.CompressibilityFactor > 0);

        Assert.True(
            prState.Density.KilogramsPerCubicMeter > 0);

        Assert.True(
            srkState.Density.KilogramsPerCubicMeter > 0);
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
}