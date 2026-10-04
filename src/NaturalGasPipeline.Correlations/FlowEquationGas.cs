using NaturalGasPipeline.Thermodynamics;
using UnitsNet;

namespace NaturalGasPipeline.Correlations;

/// <summary>
/// The gas as the pipeline flow equations see it: specific gravity G
/// (air = 1), the air molar mass that defines G, a Z model and, for the
/// equations that need a Reynolds number, a viscosity model.
///
/// M = G · M_air is used wherever physics needs the molar mass (elevation
/// term, mass flow, standard density). The air molar mass is explicit
/// because each standard or textbook uses its own (<see cref="DryAirMolarMass"/>).
/// </summary>
public sealed record FlowEquationGas
{
    public FlowEquationGas(
        double specificGravity,
        MolarMass airMolarMass,
        ICompressibilityModel compressibility,
        IViscosityModel? viscosity = null)
    {
        if (!double.IsFinite(specificGravity) || specificGravity <= 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(specificGravity), specificGravity, "Specific gravity must be finite and greater than zero.");
        }

        double air = airMolarMass.KilogramsPerMole;

        if (!double.IsFinite(air) || air <= 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(airMolarMass), air, "Air molar mass must be finite and greater than zero.");
        }

        SpecificGravity = specificGravity;
        AirMolarMass = airMolarMass;
        Compressibility = compressibility ?? throw new ArgumentNullException(nameof(compressibility));
        Viscosity = viscosity;
    }

    public double SpecificGravity { get; }

    public MolarMass AirMolarMass { get; }

    /// <summary>M = G · M_air.</summary>
    public MolarMass MolarMass => MolarMass.FromKilogramsPerMole(SpecificGravity * AirMolarMass.KilogramsPerMole);

    public ICompressibilityModel Compressibility { get; }

    /// <summary>Needed only by Reynolds-dependent equations (General Flow, AGA).</summary>
    public IViscosityModel? Viscosity { get; }
}
