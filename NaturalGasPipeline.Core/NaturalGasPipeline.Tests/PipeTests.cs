using NaturalGasPipeline.Core;
using UnitsNet;

namespace NaturalGasPipeline.Tests;

public class PipeTests
{
    [Fact]
    public void Pipe_ShouldStoreItsDimensions()
    {
        var pipe = new Pipe
        {
            OuterDiameter = Length.FromMeters(0.9144),
            WallThickness = Length.FromMeters(0.0127),
            Length = Length.FromMeters(1000),
            Roughness = Length.FromMeters(0.000045)
        };

        Assert.Equal(0.9144, pipe.OuterDiameter.Meters);
        Assert.Equal(0.0127, pipe.WallThickness.Meters);
        Assert.Equal(1000, pipe.Length.Meters);
        Assert.Equal(0.000045, pipe.Roughness.Meters);
    }
}