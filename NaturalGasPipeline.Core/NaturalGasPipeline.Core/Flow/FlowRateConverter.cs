using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnitsNet;

namespace NaturalGasPipeline.Core.Flow;

public static class FlowRateConverter
{
    public static MassFlow ConvertActualVolumeToMass(
        VolumeFlow actualVolumetricFlow,
        Density density)
    {
        double volumetricFlow =
            actualVolumetricFlow.CubicMetersPerSecond;

        double gasDensity =
            density.KilogramsPerCubicMeter;

        double massFlow =
            volumetricFlow * gasDensity;

        return MassFlow.FromKilogramsPerSecond(
            massFlow);
    }
    public static VolumeFlow ConvertMassToActualVolume(
    MassFlow massFlow,
    Density density)
    {
        double massFlowValue =
            massFlow.KilogramsPerSecond;

        double gasDensity =
            density.KilogramsPerCubicMeter;

        double volumetricFlow =
            massFlowValue / gasDensity;

        return VolumeFlow.FromCubicMetersPerSecond(
            volumetricFlow);
    }
    public static VolumeFlow ConvertMassToStandardVolume(
    MassFlow massFlow,
    double molarMass,
    StandardFlowConditions standardConditions)
    {
        const double R = 8.31446261815324;

        double massFlowValue =
            massFlow.KilogramsPerSecond;

        double temperature =
            standardConditions.Temperature.Kelvins;

        double pressure =
            standardConditions.Pressure.Pascals;

        double z =
            standardConditions.CompressibilityFactor;

        double standardVolumetricFlow =
            massFlowValue
            * z
            * R
            * temperature
            /
            (pressure * molarMass);

        return VolumeFlow.FromCubicMetersPerSecond(
            standardVolumetricFlow);
    }
}