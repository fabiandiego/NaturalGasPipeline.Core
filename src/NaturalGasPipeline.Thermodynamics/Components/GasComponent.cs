using UnitsNet;

namespace NaturalGasPipeline.Thermodynamics;

public class GasComponent
{
    public string Name { get; set; } = string.Empty;

    public double MoleFraction { get; set; }

    public MolarMass MolarMass { get; set; }

    public Temperature CriticalTemperature { get; set; }

    public Pressure CriticalPressure { get; set; }

    public double AcentricFactor { get; set; }
}
// Gas density (real gas), as computed by GasState:
//   ρ = P·M / (Z·R·T)
// P pressure, M molar mass, Z compressibility factor,
// R universal gas constant, T absolute temperature.

