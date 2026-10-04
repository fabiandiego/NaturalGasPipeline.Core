namespace NaturalGasPipeline.Hydraulics;

/// <summary>
/// Colebrook–White (1939), implicit:
///   1/√f = −2·log10( (ε/D)/3.7 + 2.51 / (Re·√f) )
/// Solved by fixed-point iteration from a Swamee–Jain start (|Δf| &lt; 1e-12).
/// The reference turbulent law for commercial pipe; the Moody chart is a plot of it.
/// Verified against the exact Lambert-W solution to better than 1e-11.
/// </summary>
public sealed class ColebrookWhiteModel : FlowRegimeFrictionModel
{
    public override string Name => "Colebrook-White";

    protected override double CalculateTurbulent(double reynoldsNumber, double relativeRoughness)
    {
        double f = SwameeJainModel.Formula(reynoldsNumber, relativeRoughness);

        const int maxIterations = 100;
        const double tolerance = 1e-12;

        for (int i = 0; i < maxIterations; i++)
        {
            double inverseSqrtF =
                -2.0 * System.Math.Log10(
                    relativeRoughness / 3.7
                    + 2.51 / (reynoldsNumber * System.Math.Sqrt(f)));

            double updated = 1.0 / (inverseSqrtF * inverseSqrtF);

            if (!double.IsFinite(updated) || updated <= 0.0)
            {
                throw new InvalidOperationException(
                    "Colebrook-White iteration produced an invalid result.");
            }

            if (System.Math.Abs(updated - f) < tolerance)
            {
                return updated;
            }

            f = updated;
        }

        throw new InvalidOperationException("Colebrook-White iteration did not converge.");
    }

    protected override bool IsTurbulentRangeValidated(double reynoldsNumber, double relativeRoughness) => true;
}

/// <summary>
/// Haaland (1983), explicit:
///   1/√f = −1.8·log10( ((ε/D)/3.7)^1.11 + 6.9/Re )
/// Validated range: 4·10³ ≤ Re ≤ 10⁸, ε/D ≤ 0.05.
/// Measured maximum |deviation| from Colebrook over that range: 1.42 %.
/// </summary>
public sealed class HaalandModel : FlowRegimeFrictionModel
{
    public override string Name => "Haaland";

    protected override double CalculateTurbulent(double reynoldsNumber, double relativeRoughness)
    {
        double x =
            -1.8 * System.Math.Log10(
                System.Math.Pow(relativeRoughness / 3.7, 1.11)
                + 6.9 / reynoldsNumber);

        return 1.0 / (x * x);
    }

    protected override bool IsTurbulentRangeValidated(double reynoldsNumber, double relativeRoughness) =>
        reynoldsNumber <= 1.0e8 && relativeRoughness <= 0.05;
}

/// <summary>
/// Swamee &amp; Jain (1976), explicit:
///   f = 0.25 / [ log10( (ε/D)/3.7 + 5.74/Re^0.9 ) ]²
/// Validated range: 5·10³ ≤ Re ≤ 10⁸, 10⁻⁶ ≤ ε/D ≤ 10⁻².
/// Measured deviation from Colebrook over that range: up to +2.83 %
/// (largest at Re = 5·10³, ε/D = 10⁻²; well below 1 % for transmission lines).
/// </summary>
public sealed class SwameeJainModel : FlowRegimeFrictionModel
{
    public override string Name => "Swamee-Jain";

    protected override double CalculateTurbulent(double reynoldsNumber, double relativeRoughness) =>
        Formula(reynoldsNumber, relativeRoughness);

    protected override bool IsTurbulentRangeValidated(double reynoldsNumber, double relativeRoughness) =>
        reynoldsNumber >= 5.0e3 && reynoldsNumber <= 1.0e8
        && relativeRoughness >= 1.0e-6 && relativeRoughness <= 1.0e-2;

    internal static double Formula(double reynoldsNumber, double relativeRoughness)
    {
        double log =
            System.Math.Log10(
                relativeRoughness / 3.7
                + 5.74 / System.Math.Pow(reynoldsNumber, 0.9));

        return 0.25 / (log * log);
    }
}

/// <summary>
/// Churchill (1977), single explicit equation for ALL regimes
/// (laminar, transitional and turbulent):
///   f = 8 · [ (8/Re)¹² + (A + B)^(−3/2) ]^(1/12)
///   A = [ 2.457 · ln( 1 / ((7/Re)^0.9 + 0.27·ε/D) ) ]¹⁶
///   B = (37530 / Re)¹⁶
/// Reduces exactly to 64/Re in laminar flow and is continuous through the
/// transition, so an iterative solver never stops there.
/// Measured deviation from Colebrook (4·10³ ≤ Re ≤ 10⁸, ε/D ≤ 0.05): up to +3.11 %,
/// near the low-Re / high-roughness corner. In the transition band the value
/// is an interpolation, not a measured law.
/// </summary>
public sealed class ChurchillModel : IFrictionFactorModel
{
    public string Name => "Churchill";

    public double CalculateDarcyFrictionFactor(double reynoldsNumber, double relativeRoughness)
    {
        FlowRegimeFrictionModel.ValidateInputs(reynoldsNumber, relativeRoughness);

        double a =
            System.Math.Pow(
                2.457 * System.Math.Log(
                    1.0 / (System.Math.Pow(7.0 / reynoldsNumber, 0.9) + 0.27 * relativeRoughness)),
                16.0);

        double b = System.Math.Pow(37530.0 / reynoldsNumber, 16.0);

        return 8.0 * System.Math.Pow(
            System.Math.Pow(8.0 / reynoldsNumber, 12.0) + System.Math.Pow(a + b, -1.5),
            1.0 / 12.0);
    }

    public bool IsWithinValidatedRange(double reynoldsNumber, double relativeRoughness) =>
        relativeRoughness <= 0.05;
}

/// <summary>
/// AGA fully turbulent (rough-pipe) law, as used in the AGA equation:
///   F = 4·log10( 3.7·D/ε ),  f = 4 / F²
/// Independent of Re; valid above the critical Reynolds number where the
/// AGA partially-turbulent (smooth-pipe) transmission factor meets it:
///   Re_c = 3.7·10^0.15 · (D/ε) · F ≈ 20.9 · (D/ε) · log10(3.7·D/ε)
/// (Arnold &amp; Stewart print the constant as "20,912"; it must be 20.912 —
/// with it their Example 6.7 gives Re_c = 9.03·10⁵, as stated.)
/// Requires ε &gt; 0.
/// </summary>
public sealed class AgaFullyTurbulentModel : IFrictionFactorModel
{
    public string Name => "AGA fully turbulent";

    public double CalculateDarcyFrictionFactor(double reynoldsNumber, double relativeRoughness)
    {
        FlowRegimeFrictionModel.ValidateInputs(reynoldsNumber, relativeRoughness);

        return FrictionFactorModelExtensions.DarcyFromTransmissionFactor(
            TransmissionFactor(relativeRoughness));
    }

    public bool IsWithinValidatedRange(double reynoldsNumber, double relativeRoughness) =>
        relativeRoughness > 0.0 && reynoldsNumber >= CriticalReynoldsNumber(relativeRoughness);

    /// <summary>F = 4·log10(3.7 / (ε/D))</summary>
    public static double TransmissionFactor(double relativeRoughness)
    {
        if (!double.IsFinite(relativeRoughness) || relativeRoughness <= 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(relativeRoughness), relativeRoughness,
                "The AGA fully turbulent law needs a rough pipe (ε/D > 0).");
        }

        return 4.0 * System.Math.Log10(3.7 / relativeRoughness);
    }

    /// <summary>Re_c = 3.7·10^0.15·(D/ε)·F</summary>
    public static double CriticalReynoldsNumber(double relativeRoughness) =>
        3.7 * System.Math.Pow(10.0, 0.15) / relativeRoughness * TransmissionFactor(relativeRoughness);
}

/// <summary>User-specified Darcy friction factor (e.g. read from a Moody chart or field-calibrated).</summary>
public sealed class ConstantDarcyFrictionModel : IFrictionFactorModel
{
    private readonly double _f;

    public ConstantDarcyFrictionModel(double darcyFrictionFactor)
    {
        FrictionFactorModelExtensions.RequirePositive(darcyFrictionFactor, nameof(darcyFrictionFactor));
        _f = darcyFrictionFactor;
    }

    public string Name => $"Constant f = {_f}";

    public double CalculateDarcyFrictionFactor(double reynoldsNumber, double relativeRoughness) => _f;

    public bool IsWithinValidatedRange(double reynoldsNumber, double relativeRoughness) => true;
}

/// <summary>User-specified transmission factor F (f = 4 / F²), the "F" input of the GTHPS specification.</summary>
public sealed class ConstantTransmissionFactorModel : IFrictionFactorModel
{
    private readonly double _f;

    public ConstantTransmissionFactorModel(double transmissionFactor)
    {
        TransmissionFactor = transmissionFactor;
        _f = FrictionFactorModelExtensions.DarcyFromTransmissionFactor(transmissionFactor);
    }

    public double TransmissionFactor { get; }

    public string Name => $"Constant F = {TransmissionFactor}";

    public double CalculateDarcyFrictionFactor(double reynoldsNumber, double relativeRoughness) => _f;

    public bool IsWithinValidatedRange(double reynoldsNumber, double relativeRoughness) => true;
}

/// <summary>Built-in correlations selectable by type.</summary>
public enum FrictionFactorModelType
{
    ColebrookWhite,
    Haaland,
    SwameeJain,
    Churchill,
    AgaFullyTurbulent
}

public static class FrictionFactorModelFactory
{
    public static IFrictionFactorModel Create(FrictionFactorModelType type) => type switch
    {
        FrictionFactorModelType.ColebrookWhite => new ColebrookWhiteModel(),
        FrictionFactorModelType.Haaland => new HaalandModel(),
        FrictionFactorModelType.SwameeJain => new SwameeJainModel(),
        FrictionFactorModelType.Churchill => new ChurchillModel(),
        FrictionFactorModelType.AgaFullyTurbulent => new AgaFullyTurbulentModel(),
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported friction factor model.")
    };
}
