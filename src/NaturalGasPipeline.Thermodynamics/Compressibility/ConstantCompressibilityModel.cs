using UnitsNet;

namespace NaturalGasPipeline.Thermodynamics;

/// <summary>
/// User-defined, state-independent compressibility factor
/// (the "user-defined Z" option of the GTHPS specification).
/// </summary>
public sealed class ConstantCompressibilityModel : ICompressibilityModel
{
    private readonly double _z;

    public ConstantCompressibilityModel(double compressibilityFactor)
    {
        if (!double.IsFinite(compressibilityFactor) || compressibilityFactor <= 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(compressibilityFactor),
                compressibilityFactor,
                "Compressibility factor must be finite and greater than zero.");
        }

        _z = compressibilityFactor;
    }

    public double Calculate(
        Pressure pressure,
        Temperature temperature) => _z;
}
