using NaturalGasPipeline.Core.Thermodynamics;
using UnitsNet;

namespace NaturalGasPipeline.Tests;

public class PengRobinsonModelTests
{
    [Fact]
    public void Methane_ShouldReturnReasonableCompressibility()
    {
        var methane = StandardGasComponents.Methane;

        var model = new PengRobinsonModel(methane);

        double z = model.Calculate(
            Pressure.FromPascals(7_000_000),
            Temperature.FromKelvins(288.15));

        Assert.InRange(z, 0.7, 1.0);
    }
}