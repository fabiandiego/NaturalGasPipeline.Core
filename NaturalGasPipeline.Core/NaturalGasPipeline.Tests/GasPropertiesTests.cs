using NaturalGasPipeline.Core;
using UnitsNet;
namespace NaturalGasPipeline.Tests;

public class GasPropertiesTests
{
    [Fact]
    public void GasProperties_ShouldStoreValues()
    {
        var gas = new GasProperties
        {
            Temperature = Temperature.FromKelvins(288.15),
            Pressure = Pressure.FromPascals(7_000_000),
            Density = UnitsNet.Density.FromKilogramsPerCubicMeter(50),
            Viscosity = DynamicViscosity.FromPascalSeconds(1.1e-5),
            CompressibilityFactor = 0.90
        };
        Assert.Equal(288.15, gas.Temperature.Kelvins);
        Assert.Equal(7_000_000, gas.Pressure.Pascals);
        Assert.Equal(50, gas.Density.KilogramsPerCubicMeter);
        Assert.Equal(1.1e-5, gas.Viscosity.PascalSeconds);
        Assert.Equal(0.90, gas.CompressibilityFactor);
    }
}