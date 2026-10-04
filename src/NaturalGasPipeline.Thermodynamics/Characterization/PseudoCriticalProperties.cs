using UnitsNet;

namespace NaturalGasPipeline.Thermodynamics;

/// <summary>
/// Pseudo-critical temperature and pressure of a gas, the reducing
/// parameters of the corresponding-states Z correlations
/// (Tpr = T / Tpc, Ppr = P / Ppc).
///
/// They are not the true critical point of the mixture: they are whatever
/// values make the generalized Z chart (Standing–Katz) fit the gas. Where
/// they come from is a modelling choice and must be stated by the caller:
/// - <see cref="FromKay"/>: mole-fraction average of the component critical
///   properties (Kay 1936), when the composition is known;
/// - <see cref="IPseudoCriticalCorrelation"/>: from specific gravity
///   (Sutton, Standing), when only γ is known;
/// - <see cref="WichertAzizCorrection"/>: adjustment for CO₂ and H₂S.
/// </summary>
public sealed record PseudoCriticalProperties
{
    public PseudoCriticalProperties(Temperature temperature, Pressure pressure)
    {
        double t = temperature.Kelvins;
        double p = pressure.Pascals;

        if (!double.IsFinite(t) || t <= 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(temperature), t, "Pseudo-critical temperature must be finite and greater than zero (K).");
        }

        if (!double.IsFinite(p) || p <= 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pressure), p, "Pseudo-critical pressure must be finite and greater than zero (Pa).");
        }

        Temperature = temperature;
        Pressure = pressure;
    }

    public Temperature Temperature { get; }

    public Pressure Pressure { get; }

    /// <summary>Tpr = T / Tpc (absolute temperatures).</summary>
    public double ReducedTemperature(Temperature temperature) =>
        temperature.Kelvins / Temperature.Kelvins;

    /// <summary>Ppr = P / Ppc (absolute pressures).</summary>
    public double ReducedPressure(Pressure pressure) =>
        pressure.Pascals / Pressure.Pascals;

    /// <summary>
    /// Kay's rule: Tpc = Σ yᵢ·Tcᵢ, Ppc = Σ yᵢ·Pcᵢ.
    /// </summary>
    public static PseudoCriticalProperties FromKay(GasMixture mixture)
    {
        ArgumentNullException.ThrowIfNull(mixture);

        return new PseudoCriticalProperties(
            mixture.CalculateCriticalTemperature(),
            mixture.CalculateCriticalPressure());
    }
}
