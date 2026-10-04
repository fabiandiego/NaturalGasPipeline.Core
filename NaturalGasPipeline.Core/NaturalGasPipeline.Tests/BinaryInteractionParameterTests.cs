using NaturalGasPipeline.Core.Thermodynamics;
using Xunit;
using Xunit.Abstractions;

namespace NaturalGasPipeline.Tests.Thermodynamics;

public class BinaryInteractionParameterTests
{
    private readonly ITestOutputHelper _output;

    public BinaryInteractionParameterTests(
        ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void BinaryInteractionParameter_ShouldStoreValues()
    {
        var parameter = new BinaryInteractionParameter
        {
            ComponentA = "Methane",
            ComponentB = "Ethane",
            Value = 0.0
        };

        _output.WriteLine(
            $"Component A = {parameter.ComponentA}");

        _output.WriteLine(
            $"Component B = {parameter.ComponentB}");

        _output.WriteLine(
            $"kij = {parameter.Value}");

        Assert.Equal(
            "Methane",
            parameter.ComponentA);

        Assert.Equal(
            "Ethane",
            parameter.ComponentB);

        Assert.Equal(
            0.0,
            parameter.Value);
    }
}