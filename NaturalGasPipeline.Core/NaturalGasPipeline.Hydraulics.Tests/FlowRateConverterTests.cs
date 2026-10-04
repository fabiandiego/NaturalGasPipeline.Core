using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

    [Fact]
    public void ActualVolumeAndMassFlowConversions_ShouldBeConsistent()
    {
        // Arrange
        VolumeFlow actualFlow =
            VolumeFlow.FromCubicMetersPerSecond(10.0);

        Density density =
            Density.FromKilogramsPerCubicMeter(20.0);

        // Act
        MassFlow massFlow =
            FlowRateConverter.ConvertActualVolumeToMass(
                actualFlow,
                density);

        VolumeFlow convertedFlow =
            FlowRateConverter.ConvertMassToActualVolume(
                massFlow,
                density);

        // Output
        _output.WriteLine("TEST 1: Actual volume and mass flow");
        _output.WriteLine(
            $"Original actual flow: {actualFlow.CubicMetersPerSecond:F6} m³/s");

        _output.WriteLine(
            $"Gas density: {density.KilogramsPerCubicMeter:F6} kg/m³");

        _output.WriteLine(
            $"Calculated mass flow: {massFlow.KilogramsPerSecond:F6} kg/s");

        _output.WriteLine(
            $"Converted actual flow: {convertedFlow.CubicMetersPerSecond:F6} m³/s");

        // Assert
        Assert.Equal(
            200.0,
            massFlow.KilogramsPerSecond,
            10);

        Assert.Equal(
            actualFlow.CubicMetersPerSecond,
            convertedFlow.CubicMetersPerSecond,
            10);
    }

    [Fact]
    public void MassFlowToStandardVolume_ShouldCalculateCorrectly()
    {
        // Arrange
        MassFlow massFlow =
            MassFlow.FromKilogramsPerSecond(200.0);

        double molarMass = 0.020; // kg/mol

        StandardFlowConditions conditions = new()
        {
            Temperature =
                Temperature.FromKelvins(288.15),

            Pressure =
                Pressure.FromPascals(101325.0),

            CompressibilityFactor = 1.0
        };

        // Act
        VolumeFlow standardFlow =
            FlowRateConverter.ConvertMassToStandardVolume(
                massFlow,
                molarMass,
                conditions);

        // Output
        _output.WriteLine("TEST 2: Mass flow to standard volume");
        _output.WriteLine(
            $"Mass flow: {massFlow.KilogramsPerSecond:F6} kg/s");

        _output.WriteLine(
            $"Molar mass: {molarMass:F6} kg/mol");

        _output.WriteLine(
            $"Standard temperature: {conditions.Temperature.Kelvins:F2} K");

        _output.WriteLine(
            $"Standard pressure: {conditions.Pressure.Pascals:F2} Pa");

        _output.WriteLine(
            $"Standard compressibility factor Z: {conditions.CompressibilityFactor:F4}");

        _output.WriteLine(
            $"Calculated standard volumetric flow: {standardFlow.CubicMetersPerSecond:F6} m³/s");

        // Assert
        // Expected value calculated from the ideal-gas equation.
        Assert.Equal(
            236.4,
            standardFlow.CubicMetersPerSecond,
            1);
    }
}