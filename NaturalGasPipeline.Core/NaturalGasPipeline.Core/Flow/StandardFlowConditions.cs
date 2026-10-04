using UnitsNet;

namespace NaturalGasPipeline.Core.Flow;

/// <summary>
/// Reference (base) conditions for standard volumetric flow.
/// The compressibility factor at these conditions is stated explicitly:
/// for natural gas at 15 °C and 101.325 kPa it is close to, but not exactly, 1.
/// </summary>
public class StandardFlowConditions
{
    public Temperature Temperature { get; set; }

    public Pressure Pressure { get; set; }

    public double CompressibilityFactor { get; set; }

    /// <summary>
    /// Throws if any reference value is not physically valid.
    /// </summary>
    public void Validate()
    {
        double t = Temperature.Kelvins;
        double p = Pressure.Pascals;
        double z = CompressibilityFactor;

        if (!double.IsFinite(t) || t <= 0.0)
        {
            throw new InvalidOperationException(
                $"Standard temperature must be finite and greater than 0 K. Value: {t} K");
        }

        if (!double.IsFinite(p) || p <= 0.0)
        {
            throw new InvalidOperationException(
                $"Standard pressure must be finite and greater than 0 Pa. Value: {p} Pa");
        }

        if (!double.IsFinite(z) || z <= 0.0)
        {
            throw new InvalidOperationException(
                $"Standard compressibility factor must be finite and greater than 0. Value: {z}");
        }
    }
}
