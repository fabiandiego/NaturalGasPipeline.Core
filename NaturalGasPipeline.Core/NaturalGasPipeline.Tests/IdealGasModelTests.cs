using NaturalGasPipeline.Core.Thermodynamics;
using UnitsNet;

namespace NaturalGasPipeline.Tests;

public class IdealGasModelTests
{
    [Fact]
    public void IdealGas_ShouldReturnOne()
    {
        var model = new IdealGasModel();

        double z = model.Calculate(
            Pressure.FromPascals(7_000_000),
            Temperature.FromKelvins(288.15));

        Assert.Equal(1.0, z);
    }
}