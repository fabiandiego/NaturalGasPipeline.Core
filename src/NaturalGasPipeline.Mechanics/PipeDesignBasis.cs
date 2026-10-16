using UnitsNet;

namespace NaturalGasPipeline.Mechanics;

/// <summary>
/// Material and code factors for the steel-pipe design formula
/// P = (2·S·t/D)·F·E·T: the yield strength S and the three dimensionless
/// factors. Immutable.
/// </summary>
public sealed class PipeDesignBasis
{
    public PipeDesignBasis(Pressure smys, double designFactor, double jointFactor, double temperatureFactor)
    {
        if (!double.IsFinite(smys.Pascals) || smys.Pascals <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(smys), "SMYS must be finite and greater than zero.");
        }

        Smys = smys;
        DesignFactor = RequireFactor(designFactor, nameof(designFactor));
        JointFactor = RequireFactor(jointFactor, nameof(jointFactor));
        TemperatureFactor = RequireFactor(temperatureFactor, nameof(temperatureFactor));
    }

    /// <summary>
    /// Basis from the code tables: grade SMYS, F of the location class,
    /// E of the seam and T at the design temperature.
    /// </summary>
    public static PipeDesignBasis FromCode(
        LinePipeGrade grade,
        LocationClass locationClass,
        LongitudinalJoint joint,
        Temperature designTemperature)
    {
        if (grade is null)
        {
            throw new ArgumentNullException(nameof(grade));
        }

        return new PipeDesignBasis(
            grade.Smys,
            DesignFactors.BasicDesignFactor(locationClass),
            DesignFactors.JointFactor(joint),
            DesignFactors.TemperatureDerating(designTemperature));
    }

    /// <summary>Specified minimum yield strength S.</summary>
    public Pressure Smys { get; }

    /// <summary>Design factor F.</summary>
    public double DesignFactor { get; }

    /// <summary>Longitudinal joint factor E.</summary>
    public double JointFactor { get; }

    /// <summary>Temperature derating factor T.</summary>
    public double TemperatureFactor { get; }

    /// <summary>Allowable hoop stress S·F·E·T.</summary>
    public Pressure AllowableHoopStress =>
        Pressure.FromPascals(Smys.Pascals * DesignFactor * JointFactor * TemperatureFactor);

    private static double RequireFactor(double value, string name)
    {
        if (!double.IsFinite(value) || value <= 0.0 || value > 1.0)
        {
            throw new ArgumentOutOfRangeException(name, value, "Factor must be in (0, 1].");
        }

        return value;
    }
}
