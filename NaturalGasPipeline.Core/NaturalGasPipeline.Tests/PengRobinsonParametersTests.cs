using NaturalGasPipeline.Core.Thermodynamics;
using UnitsNet;

namespace NaturalGasPipeline.Tests;

public class PengRobinsonParametersTests
{
    [Fact]
    public void MethaneParameters_ShouldBePositive()
    {
        var methane = StandardGasComponents.Methane;

        var parameters =
            new PengRobinsonParameters(
                methane,
                Temperature.FromKelvins(288.15));

        Assert.True(parameters.A > 0);
        Assert.True(parameters.B > 0);
        Assert.True(parameters.Alpha > 0);
        Assert.True(parameters.Kappa > 0);
    }
}