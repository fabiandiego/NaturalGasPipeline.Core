using NaturalGasPipeline.Core.Common;
using NaturalGasPipeline.Thermodynamics;
using UnitsNet;

namespace NaturalGasPipeline.Compression;

/// <summary>How the stage efficiency is defined.</summary>
public enum CompressionEfficiencyBasis
{
    /// <summary>Isentropic (adiabatic) efficiency ηs: T2 = T1 + T1·(r^((k−1)/k) − 1)/ηs.</summary>
    Isentropic,

    /// <summary>Polytropic efficiency ηp: (n−1)/n = (k−1)/(k·ηp), T2 = T1·r^((n−1)/n).</summary>
    Polytropic
}

/// <summary>
/// One compression stage of a gas with explicit heat-capacity ratio k = cp/cv.
///
///   isentropic:  x = (k−1)/k,        H = Z_avg·(R/M)·T1·(r^x − 1)/x,  P_gas = ṁ·H/ηs
///   polytropic:  m = (k−1)/(k·ηp),   H = Z_avg·(R/M)·T1·(r^m − 1)/m,  P_gas = ṁ·H/ηp
///   Z_avg = (Z(P1, T1) + Z(P2, T2)) / 2,   P_shaft = P_gas / η_mechanical
///
/// This is the usual pipeline-engineering form (e.g. Menon, Gas Pipeline
/// Hydraulics; GPSA): ideal-gas temperature relations with a mean Z
/// correction on the head. For an ideal gas both bases give the same power
/// when ηs and ηp describe the same discharge temperature. k is an input
/// (no default): for natural gas it is typically 1.25–1.32 at pipeline
/// conditions.
/// Validated against CED course P06-001 (Menon), Example 14 — see tests.
/// </summary>
public sealed class CompressorStage
{
    private const double R = PhysicalConstants.MolarGasConstant;

    public CompressorStage(
        MolarMass molarMass,
        ICompressibilityModel compressibility,
        double heatCapacityRatio,
        CompressionEfficiencyBasis efficiencyBasis,
        double efficiency,
        double mechanicalEfficiency)
    {
        double m = molarMass.KilogramsPerMole;

        if (!double.IsFinite(m) || m <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(molarMass), m, "Molar mass must be finite and greater than zero.");
        }

        if (!double.IsFinite(heatCapacityRatio) || heatCapacityRatio <= 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(heatCapacityRatio), heatCapacityRatio, "k = cp/cv must be greater than 1.");
        }

        if (!Enum.IsDefined(efficiencyBasis))
        {
            throw new ArgumentOutOfRangeException(nameof(efficiencyBasis), efficiencyBasis, "Unknown efficiency basis.");
        }

        RequireFraction(efficiency, nameof(efficiency));
        RequireFraction(mechanicalEfficiency, nameof(mechanicalEfficiency));

        MolarMass = molarMass;
        Compressibility = compressibility ?? throw new ArgumentNullException(nameof(compressibility));
        HeatCapacityRatio = heatCapacityRatio;
        EfficiencyBasis = efficiencyBasis;
        Efficiency = efficiency;
        MechanicalEfficiency = mechanicalEfficiency;
    }

    public MolarMass MolarMass { get; }

    public ICompressibilityModel Compressibility { get; }

    public double HeatCapacityRatio { get; }

    public CompressionEfficiencyBasis EfficiencyBasis { get; }

    public double Efficiency { get; }

    public double MechanicalEfficiency { get; }

    public CompressionResult Compress(
        Pressure suctionPressure,
        Temperature suctionTemperature,
        Pressure dischargePressure,
        MassFlow massFlow)
    {
        double p1 = RequirePositive(suctionPressure.Pascals, nameof(suctionPressure));
        double t1 = RequirePositive(suctionTemperature.Kelvins, nameof(suctionTemperature));
        double p2 = RequirePositive(dischargePressure.Pascals, nameof(dischargePressure));
        double mdot = massFlow.KilogramsPerSecond;

        if (!double.IsFinite(mdot) || mdot < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(massFlow), mdot, "Mass flow must be finite and non-negative.");
        }

        if (p2 < p1)
        {
            throw new ArgumentException("Discharge pressure must not be below suction pressure.", nameof(dischargePressure));
        }

        double k = HeatCapacityRatio;
        double r = p2 / p1;

        // Exponent of the temperature / head relation.
        double exponent = EfficiencyBasis == CompressionEfficiencyBasis.Isentropic
            ? (k - 1.0) / k
            : (k - 1.0) / (k * Efficiency);

        double t2 = EfficiencyBasis == CompressionEfficiencyBasis.Isentropic
            ? t1 + t1 * (System.Math.Pow(r, exponent) - 1.0) / Efficiency
            : t1 * System.Math.Pow(r, exponent);

        double z1 = Compressibility.Calculate(suctionPressure, suctionTemperature);
        double z2 = Compressibility.Calculate(dischargePressure, Temperature.FromKelvins(t2));
        double zAvg = 0.5 * (z1 + z2);

        double head = zAvg * R / MolarMass.KilogramsPerMole * t1 * (System.Math.Pow(r, exponent) - 1.0) / exponent;
        double gasPower = mdot * head / Efficiency;

        return new CompressionResult
        {
            SuctionPressure = suctionPressure,
            SuctionTemperature = suctionTemperature,
            DischargePressure = dischargePressure,
            DischargeTemperature = Temperature.FromKelvins(t2),
            PressureRatio = r,
            SuctionCompressibilityFactor = z1,
            DischargeCompressibilityFactor = z2,
            Head = SpecificEnergy.FromJoulesPerKilogram(head),
            GasPower = Power.FromWatts(gasPower),
            ShaftPower = Power.FromWatts(gasPower / MechanicalEfficiency),
            MassFlow = massFlow
        };
    }

    private static void RequireFraction(double value, string name)
    {
        if (!double.IsFinite(value) || value <= 0.0 || value > 1.0)
        {
            throw new ArgumentOutOfRangeException(name, value, "Efficiency must be in (0, 1].");
        }
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

/// <summary>Result of one compression stage (or a whole station, see <see cref="CompressorStationResult"/>).</summary>
public sealed record CompressionResult
{
    public required Pressure SuctionPressure { get; init; }

    public required Temperature SuctionTemperature { get; init; }

    public required Pressure DischargePressure { get; init; }

    public required Temperature DischargeTemperature { get; init; }

    public required double PressureRatio { get; init; }

    public required double SuctionCompressibilityFactor { get; init; }

    public required double DischargeCompressibilityFactor { get; init; }

    /// <summary>Isentropic or polytropic head, per the stage's efficiency basis.</summary>
    public required SpecificEnergy Head { get; init; }

    /// <summary>Power delivered to the gas, ṁ·H/η.</summary>
    public required Power GasPower { get; init; }

    /// <summary>Power at the driver shaft (brake power), gas power / η_mechanical.</summary>
    public required Power ShaftPower { get; init; }

    public required MassFlow MassFlow { get; init; }
}
