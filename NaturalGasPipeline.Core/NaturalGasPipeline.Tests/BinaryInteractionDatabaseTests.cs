using NaturalGasPipeline.Core.Thermodynamics;
using Xunit;
using Xunit.Abstractions;

namespace NaturalGasPipeline.Tests.Thermodynamics;

public class BinaryInteractionDatabaseTests
{
    private readonly ITestOutputHelper _output;

    public BinaryInteractionDatabaseTests(
        ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Database_ShouldStoreAndRetrieveInteractionParameter()
    {
        var database =
            new BinaryInteractionDatabase();

        database.Add(
            "Methane",
            "Ethane",
            0.01);

        double kij =
            database.Get(
                "Methane",
                "Ethane");

        _output.WriteLine(
            $"kij Methane-Ethane = {kij}");

        Assert.Equal(
            0.01,
            kij);
    }

    [Fact]
    public void Database_ShouldTreatPairAsSymmetric()
    {
        var database =
            new BinaryInteractionDatabase();

        database.Add(
            "Methane",
            "Ethane",
            0.01);

        double kij =
            database.Get(
                "Ethane",
                "Methane");

        _output.WriteLine(
            $"kij Ethane-Methane = {kij}");

        Assert.Equal(
            0.01,
            kij);
    }

    [Fact]
    public void Database_ShouldReturnZeroForUnknownPair()
    {
        var database =
            new BinaryInteractionDatabase();

        double kij =
            database.Get(
                "Methane",
                "CarbonDioxide");

        _output.WriteLine(
            $"Unknown pair kij = {kij}");

        Assert.Equal(
            0.0,
            kij);
    }
}