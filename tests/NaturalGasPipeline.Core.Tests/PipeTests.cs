using UnitsNet;
using Xunit;
using Xunit.Abstractions;
using NaturalGasPipeline.Core.Pipeline;

namespace NaturalGasPipeline.Core.Tests;

public class PipeTests
{
    private readonly ITestOutputHelper _output;

    public PipeTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static Pipe Nps36() => new()
    {
        // NPS 36, 0.500 in wall (API 5L)
        OuterDiameter = Length.FromInches(36.0),
        WallThickness = Length.FromInches(0.5),
        Length = Length.FromKilometers(50.0),
        Roughness = Length.FromMillimeters(0.045)
    };

    [Fact]
    public void InternalDiameter_ShouldBeOuterDiameterMinusTwoWalls()
    {
        Length id = Nps36().CalculateInternalDiameter();

        _output.WriteLine($"ID = {id.Inches:F3} in = {id.Meters:F4} m");

        // 36 − 2·0.5 = 35 in = 0.889 m
        Assert.Equal(35.0, id.Inches, 9);
        Assert.Equal(0.889, id.Meters, 9);
    }

    [Fact]
    public void ToSegment_ShouldCarryGeometryAndElevation()
    {
        PipeSegment segment = Nps36().ToSegment(Length.FromMeters(120.0));

        _output.WriteLine($"D = {segment.InternalDiameter.Meters} m, L = {segment.Length.Kilometers} km, " +
                          $"ε = {segment.AbsoluteRoughness.Millimeters} mm, Δz = {segment.ElevationChange.Meters} m");

        Assert.Equal(0.889, segment.InternalDiameter.Meters, 9);
        Assert.Equal(50_000.0, segment.Length.Meters, 6);
        Assert.Equal(0.045, segment.AbsoluteRoughness.Millimeters, 9);
        Assert.Equal(120.0, segment.ElevationChange.Meters, 9);
    }

    [Theory]
    [InlineData(0.0, 0.5)]   // no outer diameter
    [InlineData(36.0, 0.0)]  // no wall
    [InlineData(36.0, 18.0)] // wall fills the pipe
    public void InvalidDimensions_ShouldThrow(double outerInches, double wallInches)
    {
        var pipe = new Pipe
        {
            OuterDiameter = Length.FromInches(outerInches),
            WallThickness = Length.FromInches(wallInches),
            Length = Length.FromKilometers(1.0),
            Roughness = Length.Zero
        };

        Assert.Throws<InvalidOperationException>(() => pipe.CalculateInternalDiameter());
    }
}
