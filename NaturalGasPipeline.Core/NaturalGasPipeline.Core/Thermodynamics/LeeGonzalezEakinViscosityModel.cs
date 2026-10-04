using UnitsNet;

namespace NaturalGasPipeline.Core.Thermodynamics;

/// <summary>
/// Lee, Gonzalez &amp; Eakin (1966), "The Viscosity of Natural Gases",
/// J. Pet. Technol. 18(8), 997-1000.
///
///   μ [cP] = 1e-4 · K · exp(X · ρ^Y)
///   K = (9.4 + 0.02·M) · T^1.5 / (209 + 19·M + T)
///   X = 3.5 + 986 / T + 0.01·M
///   Y = 2.4 − 0.2·X
/// with T in °R, M in g/mol and ρ in g/cm³ (the correlation's own units;
/// the conversion is done here, explicitly, and nowhere else).
///
/// Applicability: hydrocarbon natural gases. Measured against NIST
/// (methane, 273–313 K, 1–10 MPa) it over-predicts by +1.2 % to +4.1 %.
/// It is NOT valid for non-hydrocarbon gases: for pure nitrogen it is
/// off by about −45 %. Gases with a large N₂/CO₂/H₂S content need a
/// correction (e.g. Carr–Kobayashi–Burrows) or a different model.
/// </summary>
public sealed class LeeGonzalezEakinViscosityModel : IViscosityModel
{
    public DynamicViscosity Calculate(
        Temperature temperature,
        Density density,
        MolarMass molarMass)
    {
        double tKelvin = temperature.Kelvins;
        double rhoKgPerM3 = density.KilogramsPerCubicMeter;
        double mKgPerMol = molarMass.KilogramsPerMole;

        if (!double.IsFinite(tKelvin) || tKelvin <= 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(temperature), tKelvin, "Temperature must be finite and greater than 0 K.");
        }

        if (!double.IsFinite(rhoKgPerM3) || rhoKgPerM3 <= 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(density), rhoKgPerM3, "Density must be finite and greater than zero.");
        }

        if (!double.IsFinite(mKgPerMol) || mKgPerMol <= 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(molarMass), mKgPerMol, "Molar mass must be finite and greater than zero.");
        }

        // Correlation units
        double tRankine = tKelvin * 1.8;
        double mGramsPerMol = mKgPerMol * 1000.0;
        double rhoGramsPerCm3 = rhoKgPerM3 / 1000.0;

        double k =
            (9.4 + 0.02 * mGramsPerMol)
            * System.Math.Pow(tRankine, 1.5)
            / (209.0 + 19.0 * mGramsPerMol + tRankine);

        double x =
            3.5
            + 986.0 / tRankine
            + 0.01 * mGramsPerMol;

        double y =
            2.4 - 0.2 * x;

        double viscosityCentipoise =
            1.0e-4 * k * System.Math.Exp(x * System.Math.Pow(rhoGramsPerCm3, y));

        return DynamicViscosity.FromPascalSeconds(viscosityCentipoise * 1.0e-3);
    }
}
