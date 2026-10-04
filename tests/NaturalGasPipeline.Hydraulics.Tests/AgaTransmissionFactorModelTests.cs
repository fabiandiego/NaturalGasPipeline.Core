using Xunit;
using Xunit.Abstractions;

namespace NaturalGasPipeline.Hydraulics.Tests;

/// <summary>
/// AGA transmission factor against the AGA table printed by Arnold &amp;
/// Stewart, Surface Production Operations, Figure 6.16 ("Transmission
/// factors for partially turbulent flow regime for use in the AGA
/// equation", courtesy of AGA; Re in millions, F to 3 decimals).
/// </summary>
public class AgaTransmissionFactorModelTests
{
    private readonly ITestOutputHelper _output;

    public AgaTransmissionFactorModelTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [InlineData(10.0e6, 22.028)]
    [InlineData(10.5e6, 22.107)]
    [InlineData(20.0e6, 23.146)]
    [InlineData(30.0e6, 23.802)]
    [InlineData(40.0e6, 24.268)]
    [InlineData(50.0e6, 24.630)]
    public void SmoothPipeFactor_ShouldReproduceAgaTable(double reynolds, double published)
    {
        double ft = AgaTransmissionFactorModel.SmoothPipeTransmissionFactor(reynolds);

        _output.WriteLine($"Re = {reynolds:E2}: Ft = {ft:F5} (table {published:F3})");

        Assert.InRange(ft - published, -0.0005, 0.0005);
    }

    [Fact]
    public void TransmissionFactor_ShouldTakeTheLowerOfBothZones()
    {
        var model = new AgaTransmissionFactorModel(1.0);
        double rr = 0.001158 / 11.0; // Arnold & Stewart Example 6.7

        double full = AgaFullyTurbulentModel.TransmissionFactor(rr);

        // Well above Re_c (9.03·10⁵): fully turbulent governs.
        Assert.Equal(full, model.TransmissionFactor(1e8, rr), 12);

        // Well below: the smooth-pipe law governs; with Df = 1 it equals Ft.
        double low = model.TransmissionFactor(1e5, rr);
        Assert.True(low < full);
        Assert.InRange(low - AgaTransmissionFactorModel.SmoothPipeTransmissionFactor(1e5), 0.0, 1e-3);

        // Drag factor scales the partially turbulent value.
        double withDrag = new AgaTransmissionFactorModel(0.95).TransmissionFactor(1e5, rr);
        _output.WriteLine($"Re = 1e5: F (Df 1) = {low:F4}, F (Df 0.95) = {withDrag:F4}, fully turbulent = {full:F4}");
        Assert.Equal(0.95 * low, withDrag, 3);

        // Darcy f = 4 / F².
        Assert.Equal(4.0 / (full * full), model.CalculateDarcyFrictionFactor(1e8, rr), 14);
    }

    [Fact]
    public void InvalidInputs_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AgaTransmissionFactorModel(0.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AgaTransmissionFactorModel(1.1));
        Assert.Throws<ArgumentOutOfRangeException>(() => AgaTransmissionFactorModel.SmoothPipeTransmissionFactor(3000.0));
    }
}
