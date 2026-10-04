using UnitsNet;

namespace NaturalGasPipeline.Core.Thermodynamics;

public static class StandardGasComponents
{
    public static GasComponent Methane => new()
    {
        Name = "Methane",
        MoleFraction = 1.0,
        MolarMass = 0.016043,
        CriticalTemperature =
            Temperature.FromKelvins(190.56),
        CriticalPressure =
            Pressure.FromPascals(4_599_000),
        AcentricFactor = 0.01142
    };

    public static GasComponent Ethane => new()
    {
        Name = "Ethane",
        MoleFraction = 1.0,
        MolarMass = 0.030070,
        CriticalTemperature =
            Temperature.FromKelvins(305.32),
        CriticalPressure =
            Pressure.FromPascals(4_872_000),
        AcentricFactor = 0.0995
    };

    public static GasComponent Propane => new()
    {
        Name = "Propane",
        MoleFraction = 1.0,
        MolarMass = 0.044097,
        CriticalTemperature =
            Temperature.FromKelvins(369.83),
        CriticalPressure =
            Pressure.FromPascals(4_248_000),
        AcentricFactor = 0.1524
    };

    public static GasComponent Nitrogen => new()
    {
        Name = "Nitrogen",
        MoleFraction = 1.0,
        MolarMass = 0.0280134,
        CriticalTemperature =
            Temperature.FromKelvins(126.20),
        CriticalPressure =
            Pressure.FromPascals(3_395_800),
        AcentricFactor = 0.0372
    };

    public static GasComponent CarbonDioxide => new()
    {
        Name = "CarbonDioxide",
        MoleFraction = 1.0,
        MolarMass = 0.0440095,
        CriticalTemperature =
            Temperature.FromKelvins(304.13),
        CriticalPressure =
            Pressure.FromPascals(7_377_300),
        AcentricFactor = 0.22394
    };
}