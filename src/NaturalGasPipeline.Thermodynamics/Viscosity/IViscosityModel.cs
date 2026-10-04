using UnitsNet;

namespace NaturalGasPipeline.Thermodynamics;

/// <summary>
/// Dynamic viscosity of a gas at a given state.
/// The density must be the flowing density at (P, T), normally from
/// <see cref="GasState"/>; the model does not compute it.
/// </summary>
public interface IViscosityModel
{
    DynamicViscosity Calculate(
        Temperature temperature,
        Density density,
        MolarMass molarMass);
}
