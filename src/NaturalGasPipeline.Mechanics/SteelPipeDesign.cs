using UnitsNet;

namespace NaturalGasPipeline.Mechanics;

/// <summary>
/// Internal-pressure design of steel line pipe (Barlow thin-wall hoop stress
/// with code factors):
///
///   P = (2·S·t / D) · F · E · T
///
/// P design pressure (GAUGE), S SMYS, t nominal wall thickness, D nominal
/// outside diameter, F design factor, E longitudinal joint factor, T
/// temperature derating factor. Source: 49 CFR §192.105 (verified); same
/// form as ASME B31.8 §841.1.1.
///
/// Scope and limits:
/// - Internal pressure only. External loads, corrosion allowance and mill
///   under-tolerance are NOT included; the code requires extra thickness for
///   external loads not to be counted in P (49 CFR §192.105(a)).
/// - Uses outside diameter (Barlow, conservative for thin walls); intended
///   for D/t ≳ 20. Below that a warning is returned by <see cref="Check"/>.
/// - P is a gauge pressure. The hydraulic engine works in absolute pressure:
///   use <see cref="ToAbsolute"/> / <see cref="ToGauge"/> at the boundary.
/// - Design pressure is not the MAOP: the MAOP is also limited by the
///   hydrostatic test pressure divided by the class test factor, by the
///   weakest component (flanges, valves) and by regulation. Only the
///   pipe-strength limit is computed here.
/// </summary>
[PendingReview("M-1", "Formula and factors verified against 49 CFR 192 only; check against the ASME B31.8 edition adopted (§841.1.1 and its tables) and a worked example from Menon or the course notes.")]
public static class SteelPipeDesign
{
    /// <summary>Standard atmosphere, 101 325 Pa (exact by definition). Default reference for gauge ↔ absolute.</summary>
    public static readonly Pressure StandardAtmosphere = Pressure.FromPascals(101325.0);

    /// <summary>Below this D/t the thin-wall Barlow formula is flagged (outside its intended range).</summary>
    public const double MinimumThinWallRatio = 20.0;

    /// <summary>Design pressure (gauge) for the given pipe and design basis.</summary>
    public static Pressure DesignPressureGauge(Length outsideDiameter, Length wallThickness, PipeDesignBasis basis)
    {
        if (basis is null)
        {
            throw new ArgumentNullException(nameof(basis));
        }

        double d = RequirePositive(outsideDiameter.Meters, nameof(outsideDiameter));
        double t = RequirePositive(wallThickness.Meters, nameof(wallThickness));

        if (2.0 * t >= d)
        {
            throw new ArgumentOutOfRangeException(nameof(wallThickness), "Wall thickness must be less than half the outside diameter.");
        }

        return Pressure.FromPascals(2.0 * basis.AllowableHoopStress.Pascals * t / d);
    }

    /// <summary>
    /// Minimum wall thickness for internal pressure alone, t = P·D / (2·S·F·E·T).
    /// Corrosion allowance, mill tolerance and external loads must be added
    /// by the caller before choosing a commercial wall.
    /// </summary>
    public static Length RequiredWallThickness(Length outsideDiameter, Pressure designPressureGauge, PipeDesignBasis basis)
    {
        if (basis is null)
        {
            throw new ArgumentNullException(nameof(basis));
        }

        double d = RequirePositive(outsideDiameter.Meters, nameof(outsideDiameter));
        double p = RequirePositive(designPressureGauge.Pascals, nameof(designPressureGauge));
        double t = p * d / (2.0 * basis.AllowableHoopStress.Pascals);

        if (2.0 * t >= d)
        {
            throw new ArgumentOutOfRangeException(
                nameof(designPressureGauge),
                "Required wall reaches half the diameter: the pressure is outside the range of the thin-wall formula.");
        }

        return Length.FromMeters(t);
    }

    /// <summary>Hoop stress σ_h = P·D / (2·t) for a gauge pressure (Barlow, outside diameter).</summary>
    public static Pressure HoopStress(Length outsideDiameter, Length wallThickness, Pressure pressureGauge)
    {
        double d = RequirePositive(outsideDiameter.Meters, nameof(outsideDiameter));
        double t = RequirePositive(wallThickness.Meters, nameof(wallThickness));
        return Pressure.FromPascals(pressureGauge.Pascals * d / (2.0 * t));
    }

    /// <summary>Gauge → absolute: P_abs = P_g + P_atm (default standard atmosphere).</summary>
    public static Pressure ToAbsolute(Pressure gauge, Pressure? atmospheric = null) =>
        Pressure.FromPascals(gauge.Pascals + (atmospheric ?? StandardAtmosphere).Pascals);

    /// <summary>Absolute → gauge: P_g = P_abs − P_atm (default standard atmosphere).</summary>
    public static Pressure ToGauge(Pressure absolute, Pressure? atmospheric = null) =>
        Pressure.FromPascals(absolute.Pascals - (atmospheric ?? StandardAtmosphere).Pascals);

    /// <summary>
    /// Full check of a pipe: design pressure (gauge and absolute), hoop
    /// stress and %SMYS at an operating pressure, and range warnings.
    /// </summary>
    /// <param name="operatingPressureAbsolute">Highest absolute pressure the pipe sees (from the hydraulics).</param>
    /// <param name="atmospheric">null: standard atmosphere. Use the site value for high-altitude routes.</param>
    public static PipeDesignCheck Check(
        Length outsideDiameter,
        Length wallThickness,
        PipeDesignBasis basis,
        Pressure operatingPressureAbsolute,
        Pressure? atmospheric = null)
    {
        Pressure design = DesignPressureGauge(outsideDiameter, wallThickness, basis);
        Pressure operatingGauge = ToGauge(operatingPressureAbsolute, atmospheric);
        Pressure hoop = HoopStress(outsideDiameter, wallThickness, operatingGauge);
        double ratio = outsideDiameter.Meters / wallThickness.Meters;

        var warnings = new List<string>();

        if (ratio < MinimumThinWallRatio)
        {
            warnings.Add($"D/t = {ratio:F1} < {MinimumThinWallRatio:F0}: thin-wall (Barlow) formula outside its intended range.");
        }

        if (operatingGauge.Pascals > design.Pascals)
        {
            warnings.Add(
                $"Operating {operatingGauge.Megapascals:F3} MPa(g) exceeds the design pressure {design.Megapascals:F3} MPa(g).");
        }

        return new PipeDesignCheck(
            design,
            ToAbsolute(design, atmospheric),
            operatingGauge,
            hoop,
            hoop.Pascals / basis.Smys.Pascals,
            ratio,
            warnings);
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

/// <summary>Result of <see cref="SteelPipeDesign.Check"/>.</summary>
public sealed class PipeDesignCheck
{
    internal PipeDesignCheck(
        Pressure designPressureGauge,
        Pressure designPressureAbsolute,
        Pressure operatingPressureGauge,
        Pressure operatingHoopStress,
        double fractionOfSmys,
        double diameterToThickness,
        IReadOnlyList<string> warnings)
    {
        DesignPressureGauge = designPressureGauge;
        DesignPressureAbsolute = designPressureAbsolute;
        OperatingPressureGauge = operatingPressureGauge;
        OperatingHoopStress = operatingHoopStress;
        FractionOfSmys = fractionOfSmys;
        DiameterToThickness = diameterToThickness;
        Warnings = warnings;
    }

    /// <summary>Design pressure P from the code formula (gauge). An upper bound for MAOP from pipe strength alone.</summary>
    public Pressure DesignPressureGauge { get; }

    /// <summary>The same pressure as absolute, for comparison with the hydraulic results.</summary>
    public Pressure DesignPressureAbsolute { get; }

    public Pressure OperatingPressureGauge { get; }

    /// <summary>Hoop stress at the operating pressure.</summary>
    public Pressure OperatingHoopStress { get; }

    /// <summary>Hoop stress / SMYS at the operating pressure (the usual "%SMYS", as a fraction).</summary>
    public double FractionOfSmys { get; }

    public double DiameterToThickness { get; }

    /// <summary>True when the operating pressure is within the design pressure.</summary>
    public bool WithinDesignPressure => OperatingPressureGauge.Pascals <= DesignPressureGauge.Pascals;

    public IReadOnlyList<string> Warnings { get; }
}
