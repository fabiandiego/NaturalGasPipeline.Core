using UnitsNet;
using Xunit;
using Xunit.Abstractions;

namespace NaturalGasPipeline.Thermodynamics.Tests;

/// <summary>
/// Gas characterization and Z against worked examples from academic sources
/// (retrieved 2026-10-04):
///
/// [ITU]    Istanbul Technical University, PET 509E Hydrocarbon Thermodynamics,
///          Ş. Yamanlar, Fall 2008, term project 1: DAK control points.
///          https://web.itu.edu.tr/~yamanlar/pet509e/term_project1.pdf
/// [KSU]    King Saud University, PGE 362 Properties of Reservoir Fluids,
///          M. A. Khamis, 2015, lecture 6 (sour gas, Wichert-Aziz).
///          https://faculty.ksu.edu.sa/sites/default/files/pge_362_lecture6_0.pdf
/// [Mosul]  University of Mosul, Fundamentals of Petroleum Reservoir
///          Engineering I, Examples 2.6–2.9 (after Ahmed).
///          https://uomosul.edu.iq/en/petroleumengineering/wp-content/uploads/sites/8/2023/09/Fundamentals-Of-Petroleum-Reservoir.-Eng.-I_compressed.pdf
/// [Kareem] Kareem, Iwalewa &amp; Al-Marhoun, J. Petrol. Explor. Prod. Technol. 6,
///          481 (2016), open access, "Example calculations".
///          https://link.springer.com/article/10.1007/s13202-015-0209-3
/// [Tikrit] Tikrit University, Gas Technology lecture 6, J. I. Humadi, Example 3.
///
/// The chart values are Standing–Katz readings, so they carry about ±0.005
/// of reading error. Measured |ΔZ/Z| of the correlations against them, at the
/// Tpr and Ppr printed in each source (2026-10-04):
///   DAK 1.35 %, Hall-Yarborough 1.84 %, Papay 3.34 % (inside its range).
/// Errors found in the sources are noted next to each case.
/// </summary>
public class AcademicReferenceTests
{
    private readonly ITestOutputHelper _output;

    public AcademicReferenceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static readonly PseudoCriticalProperties AnyPseudoCritical =
        new(Temperature.FromKelvins(200.0), Pressure.FromMegapascals(4.6));

    // ------------------------------------------------------------ DAK implementation

    /// <summary>[ITU] control points for a DAK routine, printed to 4 decimals.</summary>
    [Theory]
    [InlineData(1.1, 1.2, 0.5881)]
    [InlineData(2.0, 6.0, 0.9857)]
    [InlineData(2.0, 2.8, 0.9382)]
    public void Dak_ShouldReproduceItuControlPoints(double tr, double pr, double expected)
    {
        double z = new DranchukAbouKassemModel(AnyPseudoCritical).CalculateFromReducedProperties(tr, pr);

        _output.WriteLine($"[ITU] Tpr = {tr}, Ppr = {pr}: Z = {z:F6} (published {expected:F4})");

        Assert.InRange(z - expected, -5e-5, 5e-5);
    }

    // ------------------------------------------------------------ characterization

    /// <summary>
    /// [Kareem] γ = 0.7 with Sutton: Tpc = 377.59 °R, Ppc = 663.29 psia.
    /// The paper uses a Ppc slope of 131.07 (this library: 131.0), hence 0.05 psia.
    /// </summary>
    [Fact]
    public void Sutton_ShouldReproduceKareemExample()
    {
        PseudoCriticalProperties pc = new SuttonPseudoCriticalCorrelation().Calculate(0.7);

        _output.WriteLine($"Tpc = {pc.Temperature.DegreesRankine:F4} °R (published 377.59)");
        _output.WriteLine($"Ppc = {pc.Pressure.PoundsForcePerSquareInch:F4} psia (published 663.29, slope 131.07)");

        Assert.Equal(377.59, pc.Temperature.DegreesRankine, 1e-9);
        Assert.Equal(756.8 - 131.0 * 0.7 - 3.6 * 0.7 * 0.7, pc.Pressure.PoundsForcePerSquareInch, 1e-9);
        Assert.InRange(pc.Pressure.PoundsForcePerSquareInch - 663.29, 0.0, 0.05); // 0.07·γ = 0.049 psia
    }

    /// <summary>
    /// [Mosul] Example 2.7: Ma = 20.23, γ = 20.23 / 28.96 = 0.699, Standing
    /// (677 intercept) → Tpc = 389.1 °R, Ppc = 669.2 psia.
    /// </summary>
    [Fact]
    public void StandingAhmed_ShouldReproduceMosulExample2_7()
    {
        double gamma = SpecificGravity.FromMolarMass(MolarMass.FromGramsPerMole(20.23), DryAirMolarMass.Gthps);

        PseudoCriticalProperties pc =
            new StandingPseudoCriticalCorrelation(StandingGasSystem.NaturalGas, StandingCoefficientSet.Ahmed)
                .Calculate(0.699);

        _output.WriteLine($"γ = {gamma:F5} (published 0.699)");
        _output.WriteLine($"Tpc = {pc.Temperature.DegreesRankine:F3} °R (389.1), Ppc = {pc.Pressure.PoundsForcePerSquareInch:F3} psia (669.2)");

        Assert.InRange(gamma - 0.699, -0.0005, 0.0005);
        Assert.InRange(pc.Temperature.DegreesRankine - 389.1, -0.05, 0.05);
        Assert.InRange(pc.Pressure.PoundsForcePerSquareInch - 669.2, -0.05, 0.05);
    }

    /// <summary>
    /// [KSU] and [Mosul] Example 2.8: sour gas γ = 0.7, 5 % CO₂, 10 % H₂S.
    /// Standing (677): Tpc = 389.38 °R, Ppc = 669.1 psia; ε = 20.735 °R;
    /// T'pc = 368.64 °R; P'pc = 630.44 psia (computed by the source from the
    /// rounded 389.38 / 669.1, hence 0.05 psia).
    /// </summary>
    [Fact]
    public void WichertAziz_ShouldReproduceKsuSourGasExample()
    {
        PseudoCriticalProperties sweet =
            new StandingPseudoCriticalCorrelation(StandingGasSystem.NaturalGas, StandingCoefficientSet.Ahmed)
                .Calculate(0.7);

        double epsilon = WichertAzizCorrection.AdjustmentRankine(0.15, 0.10);
        PseudoCriticalProperties sour = WichertAzizCorrection.Apply(sweet, 0.05, 0.10);

        _output.WriteLine($"Tpc = {sweet.Temperature.DegreesRankine:F3} °R (389.38), Ppc = {sweet.Pressure.PoundsForcePerSquareInch:F3} psia (669.1)");
        _output.WriteLine($"ε = {epsilon:F4} °R (20.735)");
        _output.WriteLine($"T'pc = {sour.Temperature.DegreesRankine:F3} °R (368.64), P'pc = {sour.Pressure.PoundsForcePerSquareInch:F3} psia (630.44)");

        Assert.InRange(sweet.Temperature.DegreesRankine - 389.38, -0.005, 0.005);
        Assert.InRange(sweet.Pressure.PoundsForcePerSquareInch - 669.1, -0.05, 0.05);
        Assert.InRange(epsilon - 20.735, -0.0005, 0.0005);
        Assert.InRange(sour.Temperature.DegreesRankine - 368.64, -0.005, 0.005);
        Assert.InRange(sour.Pressure.PoundsForcePerSquareInch - 630.44, -0.05, 0.05);
    }

    /// <summary>
    /// [Mosul] Example 2.9, same gas with Carr-Kobayashi-Burrows:
    /// T'pc = 389.38 − 80·0.05 + 130·0.10 = 398.38 °R (printed 398.3),
    /// P'pc = 751.1 psia. The source prints Ppr = 4.56; 3500 / 751.1 = 4.66.
    /// </summary>
    [Fact]
    public void CarrKobayashiBurrows_ShouldReproduceMosulExample2_9()
    {
        PseudoCriticalProperties sweet =
            new StandingPseudoCriticalCorrelation(StandingGasSystem.NaturalGas, StandingCoefficientSet.Ahmed)
                .Calculate(0.7);

        PseudoCriticalProperties corrected = CarrKobayashiBurrowsCorrection.Apply(sweet, 0.05, 0.10, 0.0);

        _output.WriteLine($"T'pc = {corrected.Temperature.DegreesRankine:F3} °R (398.38), P'pc = {corrected.Pressure.PoundsForcePerSquareInch:F3} psia (751.1)");

        Assert.InRange(corrected.Temperature.DegreesRankine - 398.38, -0.01, 0.01);
        Assert.InRange(corrected.Pressure.PoundsForcePerSquareInch - 751.1, -0.05, 0.05);
    }

    /// <summary>
    /// [Mosul] Example 2.6, Kay's rule: Tpc = 383.38 °R, Ppc = 666.38 psia.
    /// The source uses an older critical-constant table (C₁ 343.33 °R vs
    /// 343.01 °R here, from the reference equation of state), so the values
    /// differ by a measured +0.18 % (Tpc) and +0.07 % (Ppc).
    /// </summary>
    [Fact]
    public void Kay_ShouldAgreeWithMosulExample2_6WithinCriticalConstantDifferences()
    {
        PseudoCriticalProperties pc = PseudoCriticalProperties.FromKay(MosulExample26Gas());

        double dT = pc.Temperature.DegreesRankine / 383.38 - 1.0;
        double dP = pc.Pressure.PoundsForcePerSquareInch / 666.38 - 1.0;

        _output.WriteLine($"Tpc = {pc.Temperature.DegreesRankine:F3} °R (383.38, {dT * 100:+0.000;-0.000} %)");
        _output.WriteLine($"Ppc = {pc.Pressure.PoundsForcePerSquareInch:F3} psia (666.38, {dP * 100:+0.000;-0.000} %)");

        Assert.InRange(dT, -0.0025, 0.0025);
        Assert.InRange(dP, -0.001, 0.001);
    }

    // ------------------------------------------------------------ Z vs chart

    public static IEnumerable<object[]> ChartReadings() => new List<object[]>
    {
        // source, Tpr, Ppr (as printed), Z (Standing–Katz chart)
        new object[] { "Kareem 2016", 1.6155, 3.0153, 0.83 },
        new object[] { "Mosul 2.6", 1.67, 4.5, 0.85 },
        new object[] { "Mosul 2.7", 1.64, 4.48, 0.845 },
        new object[] { "KSU / Mosul 2.8", 1.68, 5.55, 0.89 },
        new object[] { "Mosul 2.9 (Ppr 3500/751.1)", 1.56, 3500.0 / 751.1, 0.820 },
        new object[] { "Tikrit 3", 1.19, 2.25, 0.51 },
    };

    [Theory]
    [MemberData(nameof(ChartReadings))]
    public void Correlations_ShouldMatchPublishedChartReadings(string source, double tr, double pr, double chartZ)
    {
        var models = new (CorrespondingStatesCompressibilityModel Model, double Band)[]
        {
            (new DranchukAbouKassemModel(AnyPseudoCritical), 0.015),
            (new HallYarboroughModel(AnyPseudoCritical), 0.020),
            (new PapayModel(AnyPseudoCritical), 0.035),
        };

        _output.WriteLine($"[{source}] Tpr = {tr}, Ppr = {pr:F4}, chart Z = {chartZ}");

        foreach ((CorrespondingStatesCompressibilityModel model, double band) in models)
        {
            double z = model.CalculateFromReducedProperties(tr, pr);
            double deviation = z / chartZ - 1.0;
            bool inRange = model.IsWithinValidatedReducedRange(tr, pr);

            _output.WriteLine($"  {model.Name,-22} Z = {z:F4} ({deviation * 100:+0.00;-0.00} %), in validated range: {inRange}");

            if (inRange)
            {
                Assert.InRange(deviation, -band, band);
            }
        }
    }

    /// <summary>
    /// [Tikrit] Tpr 1.19, Ppr 2.25 is outside Papay's validated range, and
    /// Papay is indeed 19 % off the chart there: the range flag is what
    /// protects the user.
    /// </summary>
    [Fact]
    public void Papay_ShouldFlagTheTikritPointWhereItFails()
    {
        var papay = new PapayModel(AnyPseudoCritical);
        double deviation = papay.CalculateFromReducedProperties(1.19, 2.25) / 0.51 - 1.0;

        _output.WriteLine($"Papay at Tpr 1.19, Ppr 2.25: {deviation * 100:+0.0} % vs chart");

        Assert.False(papay.IsWithinValidatedReducedRange(1.19, 2.25));
        Assert.True(deviation > 0.15);
    }

    // ------------------------------------------------------------ full chains

    /// <summary>
    /// [KSU] full chain in SI: γ 0.7 → Standing (677) → Wichert-Aziz → DAK at
    /// 3500 psia, 160 °F. Published (chart): Z = 0.89. Measured: +0.73 %.
    /// </summary>
    [Fact]
    public void SourGasChain_ShouldMatchKsuResult()
    {
        PseudoCriticalProperties pc = WichertAzizCorrection.Apply(
            new StandingPseudoCriticalCorrelation(StandingGasSystem.NaturalGas, StandingCoefficientSet.Ahmed).Calculate(0.7),
            0.05,
            0.10);

        var model = new DranchukAbouKassemModel(pc);

        double z = model.Calculate(
            Pressure.FromPoundsForcePerSquareInch(3500.0),
            Temperature.FromDegreesFahrenheit(160.0));

        _output.WriteLine($"Z = {z:F4} (published 0.89, {(z / 0.89 - 1.0) * 100:+0.00;-0.00} %)");

        Assert.Equal(0.8965005615060525, z, 1e-12);
        Assert.InRange(z / 0.89 - 1.0, -0.015, 0.015);
    }

    /// <summary>
    /// [Mosul] Example 2.6 full chain: composition → Kay → DAK at 3000 psia,
    /// 180 °F. Published (chart): Z = 0.85. Measured: +1.16 %.
    /// </summary>
    [Fact]
    public void CompositionChain_ShouldMatchMosulExample2_6()
    {
        ICompressibilityModel model = CompressibilityModelFactory.Create(
            CompressibilityModelType.DranchukAbouKassem,
            MosulExample26Gas());

        double z = model.Calculate(
            Pressure.FromPoundsForcePerSquareInch(3000.0),
            Temperature.FromDegreesFahrenheit(180.0));

        _output.WriteLine($"Z = {z:F4} (published 0.85, {(z / 0.85 - 1.0) * 100:+0.00;-0.00} %)");

        Assert.Equal(0.8598408519648645, z, 1e-12);
        Assert.InRange(z / 0.85 - 1.0, -0.015, 0.015);
    }

    private static GasMixture MosulExample26Gas()
    {
        static GasComponent With(GasComponent component, double fraction)
        {
            component.MoleFraction = fraction;
            return component;
        }

        return new GasMixture
        {
            Components =
            {
                With(StandardGasComponents.CarbonDioxide, 0.02),
                With(StandardGasComponents.Nitrogen, 0.01),
                With(StandardGasComponents.Methane, 0.85),
                With(StandardGasComponents.Ethane, 0.04),
                With(StandardGasComponents.Propane, 0.03),
                With(StandardGasComponents.IsoButane, 0.03),
                With(StandardGasComponents.NormalButane, 0.02),
            }
        };
    }
}
