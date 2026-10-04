using NaturalGasPipeline.Core.Thermodynamics;

namespace NaturalGasPipeline.Tests;

public class StandardGasComponentsTests
{
    [Fact]
    public void Methane_ShouldHaveCorrectCriticalProperties()
    {
        var methane = StandardGasComponents.Methane;

        Assert.Equal("Methane", methane.Name);
        Assert.Equal(190.56,methane.CriticalTemperature.Kelvins);
        Assert.Equal(4_599_000,methane.CriticalPressure.Pascals);
        Assert.Equal(0.01142,methane.AcentricFactor);
        Assert.Equal(0.016043,methane.MolarMass.KilogramsPerMole);
    }
}