using UnitsNet;

namespace NaturalGasPipeline.Thermodynamics;

/// <summary>
/// Molar mass of dry air used to define the specific gravity (relative
/// density) of a gas, γ = M_gas / M_air (ideal-gas definition).
///
/// There is no single value: each standard or textbook fixes its own, and
/// the choice changes γ by up to 0.14 %. The engine never picks one
/// silently; the caller passes the reference that matches the source of
/// its data.
/// </summary>
public static class DryAirMolarMass
{
    /// <summary>ISO 6976:2016, 28.96546 g/mol.</summary>
    public static MolarMass Iso6976_2016 => MolarMass.FromGramsPerMole(28.96546);

    /// <summary>GPA Standard 2145-09, 28.9625 g/mol.</summary>
    public static MolarMass Gpa2145_09 => MolarMass.FromGramsPerMole(28.9625);

    /// <summary>
    /// 28.96 g/mol, the value of the original GTHPS program
    /// (G = MW_gas / 28.96, Final Report).
    /// </summary>
    public static MolarMass Gthps => MolarMass.FromGramsPerMole(28.96);

    /// <summary>
    /// 29 g/mol, the rounded value of Arnold &amp; Stewart, Surface Production
    /// Operations, Eq. 6.5 (S = MW / 29); use it to reproduce book examples.
    /// </summary>
    public static MolarMass Rounded29 => MolarMass.FromGramsPerMole(29.0);
}

/// <summary>Specific gravity (relative density to air) of a gas, ideal-gas definition.</summary>
public static class SpecificGravity
{
    /// <summary>γ = M_gas / M_air.</summary>
    public static double FromMolarMass(MolarMass gasMolarMass, MolarMass airMolarMass)
    {
        double gas = RequirePositive(gasMolarMass.KilogramsPerMole, nameof(gasMolarMass));
        double air = RequirePositive(airMolarMass.KilogramsPerMole, nameof(airMolarMass));

        return gas / air;
    }

    /// <summary>M_gas = γ · M_air.</summary>
    public static MolarMass ToMolarMass(double specificGravity, MolarMass airMolarMass)
    {
        RequirePositive(specificGravity, nameof(specificGravity));
        double air = RequirePositive(airMolarMass.KilogramsPerMole, nameof(airMolarMass));

        return MolarMass.FromKilogramsPerMole(specificGravity * air);
    }

    /// <summary>γ of a mixture of known composition, γ = Σ yᵢ·Mᵢ / M_air.</summary>
    public static double OfMixture(GasMixture mixture, MolarMass airMolarMass)
    {
        ArgumentNullException.ThrowIfNull(mixture);

        mixture.ValidateComposition();

        return FromMolarMass(mixture.CalculateMolarMass(), airMolarMass);
    }

    private static double RequirePositive(double value, string name)
    {
        if (!double.IsFinite(value) || value <= 0.0)
        {
            throw new ArgumentOutOfRangeException(name, value, "Value must be finite and greater than zero.");
        }

        return value;
    }
}
