using UnitsNet;

namespace NaturalGasPipeline.Thermodynamics;

/// <summary>
/// User-defined, state-independent viscosity
/// (e.g. the 0.012–0.013 cP often assumed for facility gas piping).
/// </summary>
public sealed class ConstantViscosityModel : IViscosityModel
{
    private readonly DynamicViscosity _viscosity;

    public ConstantViscosityModel(DynamicViscosity viscosity)
    {
        double mu = viscosity.PascalSeconds;

        if (!double.IsFinite(mu) || mu <= 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(viscosity), mu, "Viscosity must be finite and greater than zero.");
        }

        _viscosity = viscosity;
    }

    public DynamicViscosity Calculate(
        Temperature temperature,
        Density density,
        MolarMass molarMass) => _viscosity;
}
