using UnitsNet;

namespace NaturalGasPipeline.Core;

public class GasProperties
{
    public Temperature Temperature { get; set; }

    public Pressure Pressure { get; set; }

    public Density Density { get; set; }

    public DynamicViscosity Viscosity { get; set; }

    public double CompressibilityFactor { get; set; }
}