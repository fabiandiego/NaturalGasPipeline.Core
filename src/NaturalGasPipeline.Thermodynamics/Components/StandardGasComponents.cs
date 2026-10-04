using UnitsNet;

namespace NaturalGasPipeline.Thermodynamics;

public static class StandardGasComponents
{
    public static GasComponent Methane => new()
    {
        Name = "Methane",
        MoleFraction = 1.0,
        MolarMass = MolarMass.FromKilogramsPerMole(0.016043),
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
        MolarMass = MolarMass.FromKilogramsPerMole(0.030070),
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
        MolarMass = MolarMass.FromKilogramsPerMole(0.044097),
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
        MolarMass = MolarMass.FromKilogramsPerMole(0.0280134),
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
        MolarMass = MolarMass.FromKilogramsPerMole(0.0440095),
        CriticalTemperature =
            Temperature.FromKelvins(304.13),
        CriticalPressure =
            Pressure.FromPascals(7_377_300),
        AcentricFactor = 0.22394
    };

    // Reference-equation critical points, molar masses and acentric factors
    // as tabulated by CoolProp (retrieved 2026-10-04):
    // n-butane and isobutane: Bücker & Wagner, J. Phys. Chem. Ref. Data 35(2), 929 (2006);
    // hydrogen sulfide: Lemmon & Span, J. Chem. Eng. Data 51, 785 (2006).

    public static GasComponent IsoButane => new()
    {
        Name = "IsoButane",
        MoleFraction = 1.0,
        MolarMass = MolarMass.FromKilogramsPerMole(0.0581222),
        CriticalTemperature =
            Temperature.FromKelvins(407.81),
        CriticalPressure =
            Pressure.FromPascals(3_629_000),
        AcentricFactor = 0.1835
    };

    public static GasComponent NormalButane => new()
    {
        Name = "NormalButane",
        MoleFraction = 1.0,
        MolarMass = MolarMass.FromKilogramsPerMole(0.0581222),
        CriticalTemperature =
            Temperature.FromKelvins(425.125),
        CriticalPressure =
            Pressure.FromPascals(3_796_000),
        AcentricFactor = 0.2008
    };

    public static GasComponent HydrogenSulfide => new()
    {
        Name = "HydrogenSulfide",
        MoleFraction = 1.0,
        MolarMass = MolarMass.FromKilogramsPerMole(0.03408088),
        CriticalTemperature =
            Temperature.FromKelvins(373.1),
        CriticalPressure =
            Pressure.FromPascals(8_998_900),
        AcentricFactor = 0.1005
    };
}
