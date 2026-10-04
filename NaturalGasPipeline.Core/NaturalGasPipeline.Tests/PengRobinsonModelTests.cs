using NaturalGasPipeline.Core.Thermodynamics;
using UnitsNet;
using Xunit.Abstractions;

namespace NaturalGasPipeline.Tests;

public class PengRobinsonModelTests
{
    private readonly ITestOutputHelper _output;

    public PengRobinsonModelTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Methane_ShouldMatchIndependentImplementation()
    {
        // Independent Python implementation (numpy.roots), same component data.
        const double expected = 0.8492349032782932;

        var model = new PengRobinsonModel(StandardGasComponents.Methane);

        double z = model.Calculate(
            Pressure.FromPascals(7_000_000),
            Temperature.FromKelvins(288.15));

        _output.WriteLine($"Z (PR, methane, 288.15 K, 7 MPa): {z:R}");
        _output.WriteLine($"Reference: {expected:R}");

        Assert.Equal(expected, z, 9);
    }
}
