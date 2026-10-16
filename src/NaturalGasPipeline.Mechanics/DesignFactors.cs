using UnitsNet;

namespace NaturalGasPipeline.Mechanics;

// Factors of the steel-pipe design formula P = (2·S·t/D)·F·E·T.
//
// Source used (public, verified 2026-10-16 on ecfr.gov):
//   US 49 CFR Part 192, Subpart C — §192.105 (formula), §192.111 (F),
//   §192.113 (E), §192.115 (T).
// ASME B31.8 §841.1.1 uses the same formula; the edition-specific B31.8
// tables (841.1.6-1/-2, 841.1.7-1, 841.1.8-1) were NOT available (paywalled)
// and are review item M-1.

/// <summary>
/// Location class of the right-of-way (building density along a 1-mile
/// sliding section, ASME B31.8 §840.2 / 49 CFR §192.5).
/// </summary>
public enum LocationClass
{
    /// <summary>
    /// ASME B31.8 Class 1 Division 1 (F up to 0.80). Not in 49 CFR 192 —
    /// taken from secondary sources; see review item M-2.
    /// </summary>
    Class1Division1,

    /// <summary>Class 1 (B31.8 Division 2), F = 0.72.</summary>
    Class1Division2,

    /// <summary>Class 2, F = 0.60.</summary>
    Class2,

    /// <summary>Class 3, F = 0.50.</summary>
    Class3,

    /// <summary>Class 4, F = 0.40.</summary>
    Class4
}

/// <summary>Longitudinal seam of API 5L line pipe (49 CFR §192.113, "API Spec 5L" rows).</summary>
public enum LongitudinalJoint
{
    Seamless,
    ElectricResistanceWelded,
    ElectricFlashWelded,
    SubmergedArcWelded,
    FurnaceButtWelded
}

/// <summary>
/// Design factor F, longitudinal joint factor E and temperature derating
/// factor T. All three are dimensionless.
/// </summary>
public static class DesignFactors
{
    /// <summary>
    /// Basic design factor F for line pipe in the given location class.
    /// 49 CFR §192.111(a): Class 1 0.72, Class 2 0.60, Class 3 0.50, Class 4 0.40.
    ///
    /// Not modelled (the caller must pass a lower F explicitly): crossings,
    /// bridges, fabricated assemblies, and pipe inside compressor, regulating
    /// or metering stations, which take F ≤ 0.50 in Classes 1–2
    /// (49 CFR §192.111(b)–(d); B31.8 Table 841.1.6-2).
    /// </summary>
    [PendingReview("M-2", "Class 1 Division 1 = 0.80 comes from secondary sources on ASME B31.8; confirm against the edition adopted and its hydrotest condition.")]
    public static double BasicDesignFactor(LocationClass locationClass) => locationClass switch
    {
        LocationClass.Class1Division1 => 0.80,
        LocationClass.Class1Division2 => 0.72,
        LocationClass.Class2 => 0.60,
        LocationClass.Class3 => 0.50,
        LocationClass.Class4 => 0.40,
        _ => throw new ArgumentOutOfRangeException(nameof(locationClass), locationClass, "Unknown location class.")
    };

    /// <summary>
    /// Design factor for pipe inside compressor, regulating or metering
    /// stations in Class 1 and 2 locations: F ≤ 0.50 (49 CFR §192.111(d)(1)).
    /// </summary>
    public const double StationPipingDesignFactor = 0.50;

    /// <summary>
    /// Longitudinal joint factor E for API 5L pipe (49 CFR §192.113):
    /// seamless, ERW, electric flash welded, SAW = 1.00; furnace butt welded = 0.60.
    /// </summary>
    public static double JointFactor(LongitudinalJoint joint) => joint switch
    {
        LongitudinalJoint.Seamless => 1.00,
        LongitudinalJoint.ElectricResistanceWelded => 1.00,
        LongitudinalJoint.ElectricFlashWelded => 1.00,
        LongitudinalJoint.SubmergedArcWelded => 1.00,
        LongitudinalJoint.FurnaceButtWelded => 0.60,
        _ => throw new ArgumentOutOfRangeException(nameof(joint), joint, "Unknown longitudinal joint.")
    };

    // 49 CFR §192.115 table, in °F as published: 250 °F or less → 1.000, then
    // 300 → 0.967, 350 → 0.933, 400 → 0.900, 450 → 0.867; linear interpolation
    // between the listed temperatures.
    private static readonly double[] DeratingFahrenheit = { 250.0, 300.0, 350.0, 400.0, 450.0 };
    private static readonly double[] DeratingFactor = { 1.000, 0.967, 0.933, 0.900, 0.867 };

    /// <summary>
    /// Temperature derating factor T at the design (gas) temperature,
    /// 49 CFR §192.115, interpolated linearly in °F. T = 1 at or below
    /// 250 °F (121 °C). Above 450 °F (232 °C) the table ends and an exception
    /// is thrown rather than extrapolating.
    /// </summary>
    public static double TemperatureDerating(Temperature designTemperature)
    {
        double f = designTemperature.DegreesFahrenheit;

        if (!double.IsFinite(f))
        {
            throw new ArgumentOutOfRangeException(nameof(designTemperature), "Design temperature must be finite.");
        }

        if (f <= DeratingFahrenheit[0])
        {
            return 1.0;
        }

        for (int i = 1; i < DeratingFahrenheit.Length; i++)
        {
            if (f <= DeratingFahrenheit[i])
            {
                double w = (f - DeratingFahrenheit[i - 1]) / (DeratingFahrenheit[i] - DeratingFahrenheit[i - 1]);
                return DeratingFactor[i - 1] + w * (DeratingFactor[i] - DeratingFactor[i - 1]);
            }
        }

        throw new ArgumentOutOfRangeException(
            nameof(designTemperature),
            $"Design temperature {designTemperature.DegreesCelsius:F1} °C is above the last tabulated value (450 °F, 232 °C).");
    }
}
