using UnitsNet;
using Xunit;
using Xunit.Abstractions;

namespace NaturalGasPipeline.Mechanics.Tests;

/// <summary>
/// Steel-pipe design pressure P = (2·S·t/D)·F·E·T (49 CFR §192.105,
/// same form as ASME B31.8 §841.1.1).
///
/// REVIEW PENDING (docs/MECHANICS_REVIEW.md): the references below are hand
/// calculations from the published formula and the 49 CFR 192 tables. A
/// worked example from Menon (Gas Pipeline Hydraulics) or the course notes
/// has not been reproduced yet (item M-1).
/// </summary>
public class SteelPipeDesignTests
{
    private readonly ITestOutputHelper _output;

    public SteelPipeDesignTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static readonly Temperature Ambient = Temperature.FromDegreesCelsius(15.0);

    [Fact]
    public void DesignPressure_Nps20_X52Psi_Class1_ShouldBe1872Psig()
    {
        // Hand calculation in US units: D = 20 in, t = 0.500 in, S = 52 000 psi,
        // F = 0.72, E = 1, T = 1 → P = 2·52 000·0.5/20·0.72 = 1872 psig (exact).
        var grade = new LinePipeGrade("X52 (psi basis)", Pressure.FromPoundsForcePerSquareInch(52000.0));
        PipeDesignBasis basis = PipeDesignBasis.FromCode(grade, LocationClass.Class1Division2, LongitudinalJoint.ElectricResistanceWelded, Ambient);

        Pressure p = SteelPipeDesign.DesignPressureGauge(Length.FromInches(20.0), Length.FromInches(0.5), basis);

        _output.WriteLine($"F {basis.DesignFactor}, E {basis.JointFactor}, T {basis.TemperatureFactor}");
        _output.WriteLine($"P = {p.PoundsForcePerSquareInch:F6} psig = {p.Megapascals:F6} MPa(g)   (hand calculation: 1872 psig)");

        Assert.Equal(1872.0, p.PoundsForcePerSquareInch, 1e-6);
    }

    [Fact]
    public void DesignPressure_CatalogueX52_ShouldUseTheSiDesignation()
    {
        // Same pipe with the catalogue grade L360 (360 MPa):
        // P = 2·360 MPa·(12.7/508)·0.72 = 12.96 MPa(g) exactly.
        // The psi-based value (52 000 psi = 358.53 MPa) gives 0.41 % less:
        // that choice is review item M-3.
        PipeDesignBasis basis = PipeDesignBasis.FromCode(ApiLinePipeGrades.X52, LocationClass.Class1Division2, LongitudinalJoint.Seamless, Ambient);

        Pressure p = SteelPipeDesign.DesignPressureGauge(Length.FromInches(20.0), Length.FromInches(0.5), basis);
        double psiBasis = 2.0 * Pressure.FromPoundsForcePerSquareInch(52000.0).Pascals * 0.025 * 0.72;

        _output.WriteLine($"L360: P = {p.Megapascals:F6} MPa(g) = {p.PoundsForcePerSquareInch:F2} psig");
        _output.WriteLine($"52 000 psi: P = {psiBasis / 1e6:F6} MPa(g); difference {(p.Pascals / psiBasis - 1.0) * 100.0:F3} %");

        Assert.Equal(12.96e6, p.Pascals, 1e-3);
    }

    [Theory]
    [InlineData(LocationClass.Class1Division1, 0.80)]
    [InlineData(LocationClass.Class1Division2, 0.72)]
    [InlineData(LocationClass.Class2, 0.60)]
    [InlineData(LocationClass.Class3, 0.50)]
    [InlineData(LocationClass.Class4, 0.40)]
    public void BasicDesignFactor_ShouldMatchTheTable(LocationClass locationClass, double expected)
    {
        Assert.Equal(expected, DesignFactors.BasicDesignFactor(locationClass));
    }

    [Theory]
    [InlineData(LongitudinalJoint.Seamless, 1.00)]
    [InlineData(LongitudinalJoint.ElectricResistanceWelded, 1.00)]
    [InlineData(LongitudinalJoint.ElectricFlashWelded, 1.00)]
    [InlineData(LongitudinalJoint.SubmergedArcWelded, 1.00)]
    [InlineData(LongitudinalJoint.FurnaceButtWelded, 0.60)]
    public void JointFactor_ShouldMatchThe49Cfr192113Table(LongitudinalJoint joint, double expected)
    {
        Assert.Equal(expected, DesignFactors.JointFactor(joint));
    }

    [Theory]
    [InlineData(59.0, 1.000)]     // 15 °C
    [InlineData(250.0, 1.000)]
    [InlineData(275.0, 0.9835)]   // midway 1.000 → 0.967
    [InlineData(300.0, 0.967)]
    [InlineData(375.0, 0.9165)]   // midway 0.933 → 0.900
    [InlineData(450.0, 0.867)]
    public void TemperatureDerating_ShouldInterpolateThe49Cfr192115Table(double fahrenheit, double expected)
    {
        double t = DesignFactors.TemperatureDerating(Temperature.FromDegreesFahrenheit(fahrenheit));
        _output.WriteLine($"{fahrenheit} °F ({Temperature.FromDegreesFahrenheit(fahrenheit).DegreesCelsius:F1} °C): T = {t:F5}");
        Assert.Equal(expected, t, 1e-9);
    }

    [Fact]
    public void TemperatureDerating_AboveTheTable_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DesignFactors.TemperatureDerating(Temperature.FromDegreesFahrenheit(451.0)));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(1.01)]
    [InlineData(double.NaN)]
    public void Basis_FactorOutsideZeroOne_ShouldThrow(double factor)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PipeDesignBasis(Pressure.FromMegapascals(360.0), factor, 1.0, 1.0));
    }

    [Fact]
    public void RequiredWallThickness_ShouldInvertTheDesignPressure()
    {
        PipeDesignBasis basis = PipeDesignBasis.FromCode(ApiLinePipeGrades.X70, LocationClass.Class2, LongitudinalJoint.SubmergedArcWelded, Ambient);
        var od = Length.FromMillimeters(914.4);
        var p = Pressure.FromMegapascals(9.8);

        Length t = SteelPipeDesign.RequiredWallThickness(od, p, basis);
        Pressure back = SteelPipeDesign.DesignPressureGauge(od, t, basis);

        // t = P·D/(2·S·F·E·T) = 9.8·914.4/(2·485·0.60) mm
        double expected = 9.8 * 914.4 / (2.0 * 485.0 * 0.60);
        _output.WriteLine($"t_min = {t.Millimeters:F4} mm (hand: {expected:F4} mm), back-calculated P = {back.Megapascals:F6} MPa(g)");

        Assert.Equal(expected, t.Millimeters, 1e-9);
        Assert.Equal(p.Pascals, back.Pascals, 1e-3);
    }

    [Fact]
    public void HoopStress_AtDesignPressure_ShouldBeSmysTimesFet()
    {
        PipeDesignBasis basis = PipeDesignBasis.FromCode(ApiLinePipeGrades.X65, LocationClass.Class3, LongitudinalJoint.ElectricResistanceWelded, Temperature.FromDegreesFahrenheit(300.0));
        var od = Length.FromMillimeters(610.0);
        var t = Length.FromMillimeters(12.7);

        Pressure p = SteelPipeDesign.DesignPressureGauge(od, t, basis);
        Pressure hoop = SteelPipeDesign.HoopStress(od, t, p);

        Assert.Equal(0.50 * 1.0 * 0.967, hoop.Pascals / basis.Smys.Pascals, 1e-9);
    }

    [Fact]
    public void GaugeAndAbsolute_ShouldDifferByTheAtmosphere()
    {
        var g = Pressure.FromMegapascals(6.9);
        Assert.Equal(6.9e6 + 101325.0, SteelPipeDesign.ToAbsolute(g).Pascals, 1e-6);
        Assert.Equal(6.9e6, SteelPipeDesign.ToGauge(SteelPipeDesign.ToAbsolute(g)).Pascals, 1e-6);

        // High-altitude site: 85 kPa local atmosphere.
        Assert.Equal(6.9e6 + 85000.0, SteelPipeDesign.ToAbsolute(g, Pressure.FromKilopascals(85.0)).Pascals, 1e-6);
    }

    /// <summary>
    /// Concept case pipe (D_int 882 mm → OD 914.4 mm, t 16.2 mm) at the
    /// planner's 7 MPa(a) discharge. Shows that grade and location class
    /// decide whether the hydraulic design is mechanically admissible.
    /// </summary>
    [Fact]
    public void ConceptCasePipe_Report()
    {
        var od = Length.FromMillimeters(914.4);
        var t = Length.FromMillimeters(16.2);
        var operating = Pressure.FromMegapascals(7.0);

        _output.WriteLine($"OD {od.Millimeters} mm, t {t.Millimeters} mm, D/t {od.Millimeters / t.Millimeters:F1}, operating {operating.Megapascals} MPa(a)");
        _output.WriteLine($"{"grade",-12} {"class",-16} {"P_design MPa(g)",16} {"%SMYS at op.",13}  ok");

        foreach (LinePipeGrade grade in new[] { ApiLinePipeGrades.X52, ApiLinePipeGrades.X65, ApiLinePipeGrades.X70 })
        {
            foreach (LocationClass lc in new[] { LocationClass.Class1Division2, LocationClass.Class2, LocationClass.Class3, LocationClass.Class4 })
            {
                PipeDesignCheck c = SteelPipeDesign.Check(od, t, PipeDesignBasis.FromCode(grade, lc, LongitudinalJoint.SubmergedArcWelded, Ambient), operating);
                _output.WriteLine($"{grade.Name,-12} {lc,-16} {c.DesignPressureGauge.Megapascals,16:F3} {c.FractionOfSmys * 100.0,12:F1}%  {(c.WithinDesignPressure ? "yes" : "NO")}");
            }
        }

        PipeDesignCheck x52c3 = SteelPipeDesign.Check(od, t, PipeDesignBasis.FromCode(ApiLinePipeGrades.X52, LocationClass.Class3, LongitudinalJoint.SubmergedArcWelded, Ambient), operating);
        PipeDesignCheck x70c1 = SteelPipeDesign.Check(od, t, PipeDesignBasis.FromCode(ApiLinePipeGrades.X70, LocationClass.Class1Division2, LongitudinalJoint.SubmergedArcWelded, Ambient), operating);

        // Hand: 2·360·16.2/914.4·0.50 = 6.37795 MPa(g) < 6.898675 MPa(g) operating.
        Assert.Equal(2.0 * 360e6 * 16.2 / 914.4 * 0.50, x52c3.DesignPressureGauge.Pascals, 1e-3);
        Assert.False(x52c3.WithinDesignPressure);
        Assert.Contains(x52c3.Warnings, w => w.Contains("exceeds the design pressure"));

        // Hand: 2·485·16.2/914.4·0.72 = 12.373 MPa(g); %SMYS = 6.898675·914.4/(2·16.2)/485.
        Assert.True(x70c1.WithinDesignPressure);
        Assert.Equal((7.0e6 - 101325.0) * 914.4 / (2.0 * 16.2) / 485e6, x70c1.FractionOfSmys, 1e-12);
    }

    [Fact]
    public void Check_ThickWall_ShouldWarn()
    {
        PipeDesignBasis basis = PipeDesignBasis.FromCode(ApiLinePipeGrades.X52, LocationClass.Class1Division2, LongitudinalJoint.Seamless, Ambient);
        PipeDesignCheck c = SteelPipeDesign.Check(Length.FromMillimeters(114.3), Length.FromMillimeters(8.6), basis, Pressure.FromMegapascals(5.0));
        Assert.Contains(c.Warnings, w => w.Contains("D/t"));
    }
}
