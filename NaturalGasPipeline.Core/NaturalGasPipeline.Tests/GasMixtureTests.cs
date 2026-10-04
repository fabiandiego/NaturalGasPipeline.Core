using NaturalGasPipeline.Core.Thermodynamics;
using UnitsNet;
using Xunit;
using Xunit.Abstractions;

namespace NaturalGasPipeline.Tests.Thermodynamics;

public class GasMixtureTests
{
    private readonly ITestOutputHelper _output;

    public GasMixtureTests(
        ITestOutputHelper output)
    {
        _output = output;
    }
    [Fact]
    public void GasMixture_ShouldCalculateMolarMass()
    {
        var mixture = new GasMixture
        {
            Components =
        {
            new GasComponent
            {
                Name = "Methane",
                MoleFraction = 0.90,
                MolarMass = MolarMass.FromKilogramsPerMole(0.016043)
            },
            new GasComponent
            {
                Name = "Ethane",
                MoleFraction = 0.10,
                MolarMass = MolarMass.FromKilogramsPerMole(0.030070)
            }
        }
        };

        MolarMass molarMass = mixture.CalculateMolarMass();

        _output.WriteLine(
            $"Molar mass = {molarMass.KilogramsPerMole} kg/mol");

        // 0.90 · 0.016043 + 0.10 · 0.030070
        Assert.Equal(0.0174457, molarMass.KilogramsPerMole, 7);
    }
    [Fact]
    public void ValidComposition_ShouldPassValidation()
    {
        var mixture = new GasMixture
        {
            Components =
            {
                StandardGasComponents.Methane,
                StandardGasComponents.Ethane
            }
        };

        mixture.Components[0].MoleFraction = 0.90;
        mixture.Components[1].MoleFraction = 0.10;

        _output.WriteLine($"Total mole fraction = {mixture.TotalMoleFraction():R}");

        Assert.Equal(1.0, mixture.TotalMoleFraction(), 10);

        var exception = Record.Exception(() => mixture.ValidateComposition());

        Assert.Null(exception);
    }

    [Fact]
    public void GasMixture_ShouldRejectInvalidComposition()
    {
        var mixture = new GasMixture
        {
            Components =
        {
            StandardGasComponents.Methane,
            StandardGasComponents.Ethane
        }
        };

        mixture.Components[0].MoleFraction = 0.80;
        mixture.Components[1].MoleFraction = 0.10;

        double total =
            mixture.TotalMoleFraction();

        _output.WriteLine(
            $"Total mole fraction = {total}");

        Assert.Throws<InvalidOperationException>(
            () => mixture.ValidateComposition());
    }
    [Fact]
    public void GasMixture_ShouldRejectNegativeMoleFraction()
    {
        var mixture = new GasMixture
        {
            Components =
        {
            StandardGasComponents.Methane,
            StandardGasComponents.Ethane
        }
        };

        mixture.Components[0].MoleFraction = 1.10;
        mixture.Components[1].MoleFraction = -0.10;

        _output.WriteLine(
            $"Methane mole fraction = {mixture.Components[0].MoleFraction}");

        _output.WriteLine(
            $"Ethane mole fraction = {mixture.Components[1].MoleFraction}");

        Assert.Throws<InvalidOperationException>(
            () => mixture.ValidateComposition());
    }
    [Fact]
    public void GasMixture_ShouldRejectEmptyMixture()
    {
        var mixture = new GasMixture();

        _output.WriteLine(
            $"Number of components = {mixture.Components.Count}");

        Assert.Throws<InvalidOperationException>(
            () => mixture.ValidateComposition());
    }
    [Fact]
    public void GasMixture_ShouldRejectMoleFractionGreaterThanOne()
    {
        var mixture = new GasMixture
        {
            Components =
        {
            StandardGasComponents.Methane
        }
        };

        mixture.Components[0].MoleFraction = 1.10;

        _output.WriteLine(
            $"Methane mole fraction = {mixture.Components[0].MoleFraction}");

        Assert.Throws<InvalidOperationException>(
            () => mixture.ValidateComposition());
    }
    [Fact]
    public void GasMixture_ShouldRejectComponentWithEmptyName()
    {
        var mixture = new GasMixture
        {
            Components =
        {
            new GasComponent
            {
                Name = "",
                MoleFraction = 1.0
            }
        }
        };

        _output.WriteLine(
            $"Component name = '{mixture.Components[0].Name}'");

        Assert.Throws<InvalidOperationException>(
            () => mixture.ValidateComposition());
    }
    [Fact]
    public void GasMixture_ShouldRejectComponentWithInvalidMolarMass()
    {
        var mixture = new GasMixture
        {
            Components =
        {
            new GasComponent
            {
                Name = "Methane",
                MoleFraction = 1.0,
                MolarMass = MolarMass.FromKilogramsPerMole(0.0)
            }
        }
        };

        _output.WriteLine(
            $"Molar mass = {mixture.Components[0].MolarMass.KilogramsPerMole} kg/mol");

        Assert.Throws<InvalidOperationException>(
            () => mixture.ValidateComposition());
    }
    [Fact]
    public void GasMixture_ShouldRejectInvalidCriticalTemperature()
    {
        var mixture = new GasMixture
        {
            Components =
        {
            new GasComponent
            {
                Name = "Methane",
                MoleFraction = 1.0,
                MolarMass = MolarMass.FromKilogramsPerMole(0.016043),
                CriticalTemperature =
                    Temperature.FromKelvins(0.0)
            }
        }
        };

        _output.WriteLine(
            $"Critical temperature = " +
            $"{mixture.Components[0].CriticalTemperature.Kelvins} K");

        Assert.Throws<InvalidOperationException>(
            () => mixture.ValidateComposition());
    }
    [Fact]
    public void GasMixture_ShouldRejectInvalidCriticalPressure()
    {
        var mixture = new GasMixture
        {
            Components =
        {
            new GasComponent
            {
                Name = "Methane",
                MoleFraction = 1.0,
                MolarMass = MolarMass.FromKilogramsPerMole(0.016043),
                CriticalTemperature =
                    Temperature.FromKelvins(190.56),
                CriticalPressure =
                    Pressure.FromPascals(0.0)
            }
        }
        };

        _output.WriteLine(
            $"Critical pressure = " +
            $"{mixture.Components[0].CriticalPressure.Pascals} Pa");

        Assert.Throws<InvalidOperationException>(
            () => mixture.ValidateComposition());
    }
    [Fact]
    
    public void GasMixture_ShouldRejectInvalidAcentricFactor()
    {
        var mixture = new GasMixture
        {
            Components =
        {
            new GasComponent
            {
                Name = "Methane",
                MoleFraction = 1.0,
                MolarMass = MolarMass.FromKilogramsPerMole(0.016043),
                CriticalTemperature =
                    Temperature.FromKelvins(190.56),
                CriticalPressure =
                    Pressure.FromPascals(4_599_000),
                AcentricFactor = -1.0
            }
        }
        };

        _output.WriteLine(
            $"Acentric factor = " +
            $"{mixture.Components[0].AcentricFactor}");

        Assert.Throws<InvalidOperationException>(
            () => mixture.ValidateComposition());
    }
}