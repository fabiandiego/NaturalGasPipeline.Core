using UnitsNet;
using Xunit;
using Xunit.Abstractions;
using NaturalGasPipeline.Core.Flow;

namespace NaturalGasPipeline.Hydraulics.Tests;

public class FlowRateConverterTests
{
    private readonly ITestOutputHelper _output;

    public FlowRateConverterTests(
        ITestOutputHelper output)
    {
        _output = output;
    }

    private static StandardFlowConditions IdealIsoConditions() => new()
    {
        Temperature = Temperature.FromKelvins(288.15),
        Pressure = Pressure.FromPascals(101325.0),
        CompressibilityFactor = 1.0
    };

    [Fact]
    public void ActualVolumeAndMassFlowConversions_ShouldBeConsistent()
    {
        // Arrange
        ActualVolumetricFlow actualFlow =
            ActualVolumetricFlow.FromCubicMetersPerSecond(10.0);

        Density density =
            Density.FromKilogramsPerCubicMeter(20.0);

        // Act
        MassFlow massFlow =
            FlowRateConverter.ConvertActualVolumeToMass(
                actualFlow,
                density);

        ActualVolumetricFlow convertedFlow =
            FlowRateConverter.ConvertMassToActualVolume(
                massFlow,
                density);

        // Output
        _output.WriteLine("Actual volume <-> mass flow");
        _output.WriteLine($"Original actual flow: {actualFlow}");
        _output.WriteLine($"Gas density: {density.KilogramsPerCubicMeter:F6} kg/m³");
        _output.WriteLine($"Calculated mass flow: {massFlow.KilogramsPerSecond:F6} kg/s");
        _output.WriteLine($"Converted actual flow: {convertedFlow}");

        // Assert: hand calculation 10 m³/s · 20 kg/m³ = 200 kg/s
        Assert.Equal(200.0, massFlow.KilogramsPerSecond, 10);

        Assert.Equal(
            actualFlow.Value.CubicMetersPerSecond,
            convertedFlow.Value.CubicMetersPerSecond,
            10);
    }

    [Fact]
    public void MassFlowToStandardVolume_ShouldMatchIdealGasReference()
    {
        // Arrange
        MassFlow massFlow =
            MassFlow.FromKilogramsPerSecond(200.0);

        MolarMass molarMass =
            MolarMass.FromKilogramsPerMole(0.020);

        StandardFlowConditions conditions = IdealIsoConditions();

        // Independent reference (Python, double precision):
        // Q = 200 · 1 · 8.31446261815324 · 288.15 / (101325 · 0.020)
        const double expected = 236.448300362;

        // Act
        StandardVolumetricFlow standardFlow =
            FlowRateConverter.ConvertMassToStandardVolume(
                massFlow,
                molarMass,
                conditions);

        // Output
        _output.WriteLine("Mass flow -> standard volume");
        _output.WriteLine($"Mass flow: {massFlow.KilogramsPerSecond:F6} kg/s");
        _output.WriteLine($"Molar mass: {molarMass.KilogramsPerMole:F6} kg/mol");
        _output.WriteLine($"Standard flow: {standardFlow}");
        _output.WriteLine($"Expected: {expected:F9} m³/s");

        // Assert
        Assert.Equal(expected, standardFlow.Value.CubicMetersPerSecond, 8);
        Assert.Same(conditions, standardFlow.Conditions);
    }

    [Fact]
    public void StandardVolumeAndMassFlowConversions_ShouldBeConsistent()
    {
        MassFlow original =
            MassFlow.FromKilogramsPerSecond(123.456);

        MolarMass molarMass =
            MolarMass.FromKilogramsPerMole(0.0177651);

        StandardFlowConditions conditions = new()
        {
            Temperature = Temperature.FromKelvins(288.15),
            Pressure = Pressure.FromPascals(101325.0),
            CompressibilityFactor = 0.9976
        };

        StandardVolumetricFlow standardFlow =
            FlowRateConverter.ConvertMassToStandardVolume(
                original, molarMass, conditions);

        MassFlow roundTrip =
            FlowRateConverter.ConvertStandardVolumeToMass(
                standardFlow, molarMass);

        _output.WriteLine($"Original mass flow: {original.KilogramsPerSecond:R} kg/s");
        _output.WriteLine($"Standard flow: {standardFlow}");
        _output.WriteLine($"Round-trip mass flow: {roundTrip.KilogramsPerSecond:R} kg/s");

        Assert.Equal(original.KilogramsPerSecond, roundTrip.KilogramsPerSecond, 9);
    }

    [Fact]
    public void ConvertMassToActualVolume_ShouldRejectZeroDensity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            FlowRateConverter.ConvertMassToActualVolume(
                MassFlow.FromKilogramsPerSecond(10.0),
                Density.Zero));
    }

    [Fact]
    public void ConvertMassToStandardVolume_ShouldRejectZeroMolarMass()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            FlowRateConverter.ConvertMassToStandardVolume(
                MassFlow.FromKilogramsPerSecond(10.0),
                MolarMass.Zero,
                IdealIsoConditions()));
    }

    [Fact]
    public void ConvertMassToStandardVolume_ShouldRejectUndefinedConditions()
    {
        // Conditions created but never filled in: T = 0 K, P = 0 Pa, Z = 0.
        var undefined = new StandardFlowConditions();

        Assert.Throws<InvalidOperationException>(() =>
            FlowRateConverter.ConvertMassToStandardVolume(
                MassFlow.FromKilogramsPerSecond(10.0),
                MolarMass.FromKilogramsPerMole(0.020),
                undefined));
    }
}
