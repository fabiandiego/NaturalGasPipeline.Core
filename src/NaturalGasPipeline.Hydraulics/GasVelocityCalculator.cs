using UnitsNet;

namespace NaturalGasPipeline.Hydraulics;

/// <summary>
/// Gas velocity and the API RP 14E erosional velocity.
///
/// Mean velocity from mass flow: V = ṁ / (ρ·A), A = π·D²/4. With ṁ fixed,
/// V ∝ 1/ρ ∝ Z/P, so in a pipe section the highest velocity is at the
/// lowest pressure (station suction or delivery point).
///
/// Erosional velocity, API RP 14E as given by Arnold &amp; Stewart, Surface
/// Production Operations (2016), Eq. 6.15:
///   V_e = C / √ρ   with V_e in ft/s and ρ in lbm/ft³,
///   C = 100 continuous service, 125 intermittent, 60 corrosive.
/// Converted exactly to SI here: V_e[m/s] = C·0.3048·√(16.018463)/√ρ[kg/m³]
/// ≈ 1.2199·C/√ρ. RP 14E was written for production-facility piping and is
/// generally regarded as conservative for clean, dry transmission gas; its
/// use as a pipeline limit is a design choice, not a code requirement.
/// </summary>
public static class GasVelocityCalculator
{
    /// <summary>API RP 14E constant for continuous service.</summary>
    public const double ContinuousServiceConstant = 100.0;

    /// <summary>API RP 14E constant for intermittent service.</summary>
    public const double IntermittentServiceConstant = 125.0;

    /// <summary>API RP 14E constant for corrosive service.</summary>
    public const double CorrosiveServiceConstant = 60.0;

    // 1 lbm/ft³ = 0.45359237 kg / 0.3048³ m³ (exact definitions).
    private const double KilogramsPerCubicMeterPerPoundPerCubicFoot = 0.45359237 / (0.3048 * 0.3048 * 0.3048);

    /// <summary>Mean velocity V = ṁ / (ρ·π·D²/4).</summary>
    public static Speed Velocity(MassFlow massFlow, Density density, Length internalDiameter)
    {
        double rho = RequirePositive(density.KilogramsPerCubicMeter, nameof(density));
        double d = RequirePositive(internalDiameter.Meters, nameof(internalDiameter));
        double area = System.Math.PI * d * d / 4.0;
        return Speed.FromMetersPerSecond(massFlow.KilogramsPerSecond / (rho * area));
    }

    /// <summary>
    /// Erosional velocity V_e = C/√ρ (API RP 14E, US units), returned in SI.
    /// </summary>
    /// <param name="density">Gas density at flowing conditions.</param>
    /// <param name="constant">Empirical C in (ft/s)·(lbm/ft³)^½; default 100 (continuous service).</param>
    public static Speed ErosionalVelocity(Density density, double constant = ContinuousServiceConstant)
    {
        double rho = RequirePositive(density.KilogramsPerCubicMeter, nameof(density));
        double c = RequirePositive(constant, nameof(constant));
        double rhoUs = rho / KilogramsPerCubicMeterPerPoundPerCubicFoot;
        return Speed.FromFeetPerSecond(c / System.Math.Sqrt(rhoUs));
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
