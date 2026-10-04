using UnitsNet;
using Xunit;
using Xunit.Abstractions;

namespace NaturalGasPipeline.Hydraulics.Tests;

/// <summary>
/// Interchangeable friction factor models.
///
/// Level 1 (implementation): each explicit correlation against an
/// independent Python implementation of the published formula.
///
/// Level 2 (accuracy): each explicit correlation against Colebrook-White
/// (itself verified against the exact Lambert-W solution) over its whole
/// range. Measured maximum |deviation| (2026-10-04, 80 × 51 grid):
///   Haaland     1.42 %  (Re 4e3–1e8, ε/D 0–0.05)
///   Swamee-Jain 2.83 %  (Re 5e3–1e8, ε/D 0–1e-2)
///   Churchill   3.11 %  (Re 4e3–1e8, ε/D 0–0.05)
/// </summary>
public class FrictionFactorModelTests
{
    private readonly ITestOutputHelper _output;

    public FrictionFactorModelTests(ITestOutputHelper output)
    {
        _output = output;
    }

    // ------------------------------------------------------------ level 1

    [Theory]
    [InlineData(FrictionFactorModelType.Haaland, 1e4, 1e-4, 0.030990343480878074)]
    [InlineData(FrictionFactorModelType.Haaland, 1e5, 1e-4, 0.018265053014793857)]
    [InlineData(FrictionFactorModelType.Haaland, 1e6, 1e-5, 0.01176686208870277)]
    [InlineData(FrictionFactorModelType.Haaland, 1e7, 9e-5, 0.011943149765621957)]
    [InlineData(FrictionFactorModelType.Haaland, 1e8, 1e-6, 0.006445137792277497)]
    [InlineData(FrictionFactorModelType.Haaland, 5e3, 1e-2, 0.0473033432457339)]
    [InlineData(FrictionFactorModelType.SwameeJain, 1e4, 1e-4, 0.03114870032801224)]
    [InlineData(FrictionFactorModelType.SwameeJain, 1e5, 1e-4, 0.01845244530756638)]
    [InlineData(FrictionFactorModelType.SwameeJain, 1e6, 1e-5, 0.011853158126668624)]
    [InlineData(FrictionFactorModelType.SwameeJain, 1e7, 9e-5, 0.01199443743976968)]
    [InlineData(FrictionFactorModelType.SwameeJain, 1e8, 1e-6, 0.006505780760985008)]
    [InlineData(FrictionFactorModelType.SwameeJain, 5e3, 1e-2, 0.04859553215682172)]
    [InlineData(FrictionFactorModelType.Churchill, 1e4, 1e-4, 0.031178157149301035)]
    [InlineData(FrictionFactorModelType.Churchill, 1e5, 1e-4, 0.018462624566280075)]
    [InlineData(FrictionFactorModelType.Churchill, 1e6, 1e-5, 0.011858160518513692)]
    [InlineData(FrictionFactorModelType.Churchill, 1e7, 9e-5, 0.011990797554557224)]
    [InlineData(FrictionFactorModelType.Churchill, 1e8, 1e-6, 0.006506034844939031)]
    [InlineData(FrictionFactorModelType.Churchill, 5e3, 1e-2, 0.04861068976498433)]
    [InlineData(FrictionFactorModelType.AgaFullyTurbulent, 1e8, 1e-4, 0.011979797083255311)]
    public void ExplicitCorrelation_ShouldMatchIndependentImplementation(
        FrictionFactorModelType type,
        double reynolds,
        double relativeRoughness,
        double expected)
    {
        IFrictionFactorModel model = FrictionFactorModelFactory.Create(type);

        double f = model.CalculateDarcyFrictionFactor(reynolds, relativeRoughness);
        double error = System.Math.Abs(f - expected) / expected;

        _output.WriteLine($"{model.Name}: Re = {reynolds:E2}, ε/D = {relativeRoughness:E2}");
        _output.WriteLine($"f = {f:R} (reference {expected:R}, rel. error {error:E1})");

        Assert.True(error < 1e-12, $"Relative error {error:E2}");
    }

    // ------------------------------------------------------------ level 2

    [Theory]
    [InlineData(FrictionFactorModelType.Haaland, 4e3, 1e8, 1e-6, 5e-2, 0.015)]
    [InlineData(FrictionFactorModelType.SwameeJain, 5e3, 1e8, 1e-6, 1e-2, 0.030)]
    [InlineData(FrictionFactorModelType.Churchill, 4e3, 1e8, 1e-6, 5e-2, 0.032)]
    public void ExplicitCorrelation_ShouldStayWithinDocumentedDeviationFromColebrook(
        FrictionFactorModelType type,
        double reynoldsMin,
        double reynoldsMax,
        double roughnessMin,
        double roughnessMax,
        double maxAllowedRelativeDeviation)
    {
        IFrictionFactorModel model = FrictionFactorModelFactory.Create(type);
        var colebrook = new ColebrookWhiteModel();

        double worst = 0.0;
        double worstRe = 0.0;
        double worstRr = 0.0;

        foreach ((double re, double rr) in Grid(reynoldsMin, reynoldsMax, roughnessMin, roughnessMax))
        {
            double reference = colebrook.CalculateDarcyFrictionFactor(re, rr);
            double deviation = (model.CalculateDarcyFrictionFactor(re, rr) - reference) / reference;

            if (System.Math.Abs(deviation) > System.Math.Abs(worst))
            {
                worst = deviation;
                worstRe = re;
                worstRr = rr;
            }
        }

        _output.WriteLine($"{model.Name}: max deviation from Colebrook {worst * 100:+0.000;-0.000} %");
        _output.WriteLine($"at Re = {worstRe:E3}, ε/D = {worstRr:E3} (allowed ±{maxAllowedRelativeDeviation * 100:F1} %)");

        Assert.InRange(worst, -maxAllowedRelativeDeviation, maxAllowedRelativeDeviation);
    }

    /// <summary>80 log-spaced Re values × (smooth pipe + 50 log-spaced ε/D values).</summary>
    private static IEnumerable<(double Re, double Rr)> Grid(
        double reMin, double reMax, double rrMin, double rrMax)
    {
        for (int i = 0; i < 80; i++)
        {
            double re = System.Math.Pow(10.0,
                System.Math.Log10(reMin) + i * (System.Math.Log10(reMax) - System.Math.Log10(reMin)) / 79.0);

            yield return (re, 0.0);

            for (int j = 0; j < 50; j++)
            {
                double rr = System.Math.Pow(10.0,
                    System.Math.Log10(rrMin) + j * (System.Math.Log10(rrMax) - System.Math.Log10(rrMin)) / 49.0);

                yield return (re, rr);
            }
        }
    }

    // ------------------------------------------------------------ regimes

    [Fact]
    public void Churchill_ShouldReduceToLaminarAndCrossTheTransition()
    {
        var churchill = new ChurchillModel();

        double laminar = churchill.CalculateDarcyFrictionFactor(1000.0, 1e-4);

        _output.WriteLine($"Re = 1000: f = {laminar:R} (64/Re = 0.064)");
        Assert.Equal(0.064, laminar, 12);

        // Continuous and finite through 2300–4000, where Colebrook throws.
        double previous = churchill.CalculateDarcyFrictionFactor(2000.0, 1e-4);

        for (double re = 2100.0; re <= 4000.0; re += 100.0)
        {
            double f = churchill.CalculateDarcyFrictionFactor(re, 1e-4);
            _output.WriteLine($"Re = {re:F0}: f = {f:F5}");

            Assert.True(double.IsFinite(f) && f > 0.0);
            Assert.True(System.Math.Abs(f - previous) / previous < 0.15, "Jump larger than 15 % between steps.");

            previous = f;
        }

        Assert.Throws<InvalidOperationException>(
            () => new ColebrookWhiteModel().CalculateDarcyFrictionFactor(3000.0, 1e-4));
    }

    [Theory]
    [InlineData(FrictionFactorModelType.ColebrookWhite)]
    [InlineData(FrictionFactorModelType.Haaland)]
    [InlineData(FrictionFactorModelType.SwameeJain)]
    public void TurbulentCorrelations_ShouldUseHagenPoiseuilleInLaminarFlow(FrictionFactorModelType type)
    {
        double f = FrictionFactorModelFactory.Create(type).CalculateDarcyFrictionFactor(1500.0, 1e-4);

        Assert.Equal(64.0 / 1500.0, f, 15);
    }

    // ------------------------------------------------------------ AGA

    /// <summary>
    /// Arnold &amp; Stewart, Example 6.7: d = 11.0 in, ε = 0.001158 in
    /// → F = 18.18 and Re_c = 9.03·10⁵ (book values, rounded).
    /// </summary>
    [Fact]
    public void AgaFullyTurbulent_ShouldReproduceBookExample6_7()
    {
        double relativeRoughness =
            FrictionFactorModelExtensions.RelativeRoughness(
                Length.FromInches(11.0),
                Length.FromInches(0.001158));

        double transmission = AgaFullyTurbulentModel.TransmissionFactor(relativeRoughness);
        double critical = AgaFullyTurbulentModel.CriticalReynoldsNumber(relativeRoughness);

        _output.WriteLine($"F = {transmission:F4} (book 18.18)");
        _output.WriteLine($"Re_c = {critical:E4} (book 9.03E+005)");

        Assert.InRange((transmission - 18.18) / 18.18, -0.001, 0.001);
        Assert.InRange((critical - 9.03e5) / 9.03e5, -0.005, 0.005);
    }

    [Fact]
    public void AgaFullyTurbulent_ShouldMatchColebrookAtVeryHighReynolds()
    {
        // The rough-pipe law is the Re → ∞ limit of Colebrook.
        double rr = 1.05e-4;
        double aga = new AgaFullyTurbulentModel().CalculateDarcyFrictionFactor(1e10, rr);
        double colebrook = new ColebrookWhiteModel().CalculateDarcyFrictionFactor(1e10, rr);

        _output.WriteLine($"AGA {aga:R}, Colebrook {colebrook:R}");

        Assert.InRange((aga - colebrook) / colebrook, -1e-4, 1e-4);
    }

    // ------------------------------------------------------------ user values

    [Fact]
    public void TransmissionFactor_ShouldConvertBothWays()
    {
        double f = 0.0121;
        double transmission = FrictionFactorModelExtensions.TransmissionFactorFromDarcy(f);

        Assert.Equal(2.0 / System.Math.Sqrt(f), transmission, 12);
        Assert.Equal(f, FrictionFactorModelExtensions.DarcyFromTransmissionFactor(transmission), 14);
        Assert.Equal(f, new ConstantTransmissionFactorModel(transmission).CalculateDarcyFrictionFactor(1e7, 1e-4), 14);
        Assert.Equal(f, new ConstantDarcyFrictionModel(f).CalculateDarcyFrictionFactor(1e7, 1e-4));
    }

    [Fact]
    public void ValidatedRange_ShouldFlagUseOutsideTheCorrelationRange()
    {
        Assert.False(new SwameeJainModel().IsWithinValidatedRange(1e7, 0.0));    // smooth pipe: below 1e-6
        Assert.True(new SwameeJainModel().IsWithinValidatedRange(1e7, 1e-4));
        Assert.False(new HaalandModel().IsWithinValidatedRange(1e9, 1e-4));      // Re above 1e8
        Assert.False(new AgaFullyTurbulentModel().IsWithinValidatedRange(1e5, 1e-4)); // below Re_c
        Assert.True(new ChurchillModel().IsWithinValidatedRange(3000.0, 1e-4));
    }

    [Fact]
    public void InvalidInputs_ShouldThrow()
    {
        var model = new HaalandModel();

        Assert.Throws<ArgumentOutOfRangeException>(() => model.CalculateDarcyFrictionFactor(0.0, 1e-4));
        Assert.Throws<ArgumentOutOfRangeException>(() => model.CalculateDarcyFrictionFactor(1e5, -1e-4));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ConstantDarcyFrictionModel(0.0));
        Assert.Throws<ArgumentException>(() =>
            FrictionFactorModelExtensions.RelativeRoughness(Length.FromMeters(0.1), Length.FromMeters(0.2)));
    }
}
