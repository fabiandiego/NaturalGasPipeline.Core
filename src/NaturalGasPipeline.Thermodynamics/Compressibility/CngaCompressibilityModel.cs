using UnitsNet;

namespace NaturalGasPipeline.Thermodynamics;

/// <summary>
/// California Natural Gas Association (CNGA) correlation, explicit, as
/// given by Menon, Gas Pipeline Hydraulics (Eq. 1.12):
///
///   Z = 1 / (1 + Pg · 344400 · 10^(1.785·G) / Tf^3.825)
///
///   Pg  gauge pressure, psig
///   Tf  temperature, °R
///   G   gas specific gravity (air = 1)
///
/// The formula works with GAUGE pressure, so the atmospheric pressure is
/// an explicit input (no silent 14.7 psia). Menon states it is valid for
/// Pg &gt; 100 psig and takes Z = 1 below; this implementation always
/// evaluates the formula (Z → 1 smoothly as Pg → 0, with no jump at
/// 100 psig) and reports Pg ≤ 100 psig as outside the validated range.
///
/// A candidate for the "PCS (closed-form)" Z option of the original GTHPS
/// program, together with <see cref="PapayModel"/>; the report does not say
/// which one it used.
/// </summary>
public sealed class CngaCompressibilityModel : ICompressibilityCorrelation
{
    private const double ValidityLowerGaugePsi = 100.0;

    private readonly double _specificGravity;
    private readonly double _atmosphericPascals;

    public CngaCompressibilityModel(double specificGravity, Pressure atmosphericPressure)
    {
        if (!double.IsFinite(specificGravity) || specificGravity <= 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(specificGravity), specificGravity, "Specific gravity must be finite and greater than zero.");
        }

        double patm = atmosphericPressure.Pascals;

        if (!double.IsFinite(patm) || patm <= 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(atmosphericPressure), patm, "Atmospheric pressure must be finite and greater than zero.");
        }

        _specificGravity = specificGravity;
        _atmosphericPascals = patm;
        AtmosphericPressure = atmosphericPressure;
    }

    public double SpecificGravity => _specificGravity;

    public Pressure AtmosphericPressure { get; }

    public string Name => "CNGA";

    public double Calculate(Pressure pressure, Temperature temperature)
    {
        double p = pressure.Pascals;
        double t = temperature.Kelvins;

        if (!double.IsFinite(p) || p <= 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pressure), p, "Pressure must be finite and greater than zero (absolute).");
        }

        if (!double.IsFinite(t) || t <= 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(temperature), t, "Temperature must be finite and greater than zero (K).");
        }

        double gaugePsi = GaugePsi(pressure);
        double rankine = temperature.DegreesRankine;

        return 1.0 / (1.0
                      + gaugePsi * 344400.0 * System.Math.Pow(10.0, 1.785 * _specificGravity)
                      / System.Math.Pow(rankine, 3.825));
    }

    public bool IsWithinValidatedRange(Pressure pressure, Temperature temperature) =>
        GaugePsi(pressure) > ValidityLowerGaugePsi;

    private double GaugePsi(Pressure absolutePressure) =>
        Pressure.FromPascals(absolutePressure.Pascals - _atmosphericPascals).PoundsForcePerSquareInch;
}
