using UnitsNet;
using Xunit;
using Xunit.Abstractions;

namespace NaturalGasPipeline.Thermodynamics.Tests;

/// <summary>
/// AGA8 DETAIL (AGA8-DC92, ISO 12213-2).
///
/// LEVEL 1 — implementation: the port reproduces the NIST reference code
/// (usnistgov/AGA8, C++ Detail.cpp): its published check point to 1e-8,
/// and Z / density computed with the compiled NIST code for natural gases
/// at pipeline conditions to 1e-10.
///
/// LEVEL 2 — accuracy: against the NIST WebBook reference equations for
/// pure methane (Setzmann–Wagner) and nitrogen (Span et al.).
/// </summary>
public class Aga8DetailTests
{
    private const double R = 8.31446261815324;
    private readonly ITestOutputHelper _output;

    public Aga8DetailTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>Gases used for the reference values (DETAIL order: 1 CH4, 2 N2, 3 CO2, 4 C2, 5 C3, 6 iC4, 7 nC4).</summary>
    internal static double[] DetailFractions(string gas)
    {
        var x = new double[22];
        switch (gas)
        {
            case "Concept":
                x[1] = .90; x[4] = .05; x[5] = .02; x[6] = .003; x[7] = .005; x[2] = .012; x[3] = .01;
                break;
            case "RussiaTarvisio":
                // Snam 2014–15 Tarvisio, with the engine's mapping (C5+ as n-butane, He as N2), scaled by 100/99.998.
                double s = 100.0 / 99.998;
                x[1] = 95.554 * s / 100; x[4] = 2.632 * s / 100; x[5] = .582 * s / 100; x[6] = .097 * s / 100;
                x[7] = (.095 + .021 + .014 + .019) * s / 100; x[2] = (.646 + .001) * s / 100; x[3] = .337 * s / 100;
                break;
            case "Methane":
                x[1] = 1;
                break;
            case "HighCO2N2":
                x[1] = .80; x[2] = .10; x[3] = .08; x[4] = .02;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(gas));
        }

        return x;
    }

    [Fact]
    public void NistCheckPoint_ShouldBeReproduced()
    {
        // usnistgov/AGA8 Detail_test_01.cpp: 21-component gas, 400 K, 50 MPa.
        double[] f = { 0.77824, 0.02, 0.06, 0.08, 0.03, 0.0015, 0.003, 0.0005, 0.00165, 0.00215, 0.00088, 0.00024, 0.00015, 0.00009,
                       0.004, 0.005, 0.002, 0.0001, 0.0025, 0.007, 0.001 };
        var x = new double[22];
        Array.Copy(f, 0, x, 1, 21);
        var model = new Aga8DetailModel(x);

        Aga8Properties p = model.Properties(400.0, 50000.0);

        _output.WriteLine($"M {p.MolarMassGramsPerMole:R}  D {p.DensityMolPerLitre:R}  Z {p.Z:R}");
        _output.WriteLine($"Cp {p.Cp:R}  W {p.SpeedOfSound:R}  kappa {p.IsentropicExponent:R}");

        Assert.Equal(20.54333051, p.MolarMassGramsPerMole, 8);
        Assert.Equal(12.80792403648801, p.DensityMolPerLitre, 8);
        Assert.Equal(50000.0, p.PressureKilopascals, 7);
        Assert.Equal(1.173801364147326, p.Z, 8);
        Assert.Equal(6971.387690924090, p.DPdD, 7);
        Assert.Equal(1118.803636639520, p.D2PdD2, 7);
        Assert.Equal(235.6641493068212, p.DPdT, 8);
        Assert.Equal(-2739.134175817231, p.InternalEnergy, 7);
        Assert.Equal(1164.699096269404, p.Enthalpy, 7);
        Assert.Equal(-38.54882684677111, p.Entropy, 8);
        Assert.Equal(39.12076154430332, p.Cv, 8);
        Assert.Equal(58.54617672380667, p.Cp, 8);
        Assert.Equal(712.6393684057903, p.SpeedOfSound, 8);
        Assert.Equal(16584.22983497785, p.GibbsEnergy, 7);
        Assert.Equal(7.432969304794577E-05, p.JouleThomson, 12);
        Assert.Equal(2.672509225184606, p.IsentropicExponent, 8);
    }

    /// <summary>Gas, T (K), P (kPa), D (mol/L), Z — from the compiled NIST C++ code (Detail.cpp, 2017).</summary>
    public static IEnumerable<object[]> NistCodeValues => new List<object[]>
    {
            new object[] { "Concept", 273.15, 101.325, 0.044747020207, 0.997044695979 },
            new object[] { "Concept", 273.15, 1000.000, 0.453620501075, 0.970665203317 },
            new object[] { "Concept", 273.15, 4000.000, 1.998736978856, 0.881183748663 },
            new object[] { "Concept", 273.15, 7000.000, 3.882629358491, 0.793842308072 },
            new object[] { "Concept", 273.15, 10000.000, 6.106657001333, 0.721038754606 },
            new object[] { "Concept", 288.15, 101.325, 0.042396782775, 0.997535652444 },
            new object[] { "Concept", 288.15, 1000.000, 0.427811554192, 0.975645860892 },
            new object[] { "Concept", 288.15, 4000.000, 1.848974012565, 0.902971202953 },
            new object[] { "Concept", 288.15, 7000.000, 3.501493118830, 0.834429172204 },
            new object[] { "Concept", 288.15, 10000.000, 5.367876166483, 0.777574890224 },
            new object[] { "Concept", 313.15, 101.325, 0.038987424788, 0.998166491388 },
            new object[] { "Concept", 313.15, 1000.000, 0.391114637683, 0.981989522448 },
            new object[] { "Concept", 313.15, 4000.000, 1.652351986940, 0.929754626899 },
            new object[] { "Concept", 313.15, 7000.000, 3.046381347855, 0.882520284553 },
            new object[] { "Concept", 313.15, 10000.000, 4.550151962408, 0.844082745926 },
            new object[] { "RussiaTarvisio", 273.15, 101.325, 0.044731033561, 0.997401034720 },
            new object[] { "RussiaTarvisio", 273.15, 1000.000, 0.451940052987, 0.974274426430 },
            new object[] { "RussiaTarvisio", 273.15, 4000.000, 1.963770460856, 0.896873936505 },
            new object[] { "RussiaTarvisio", 273.15, 7000.000, 3.745911506651, 0.822815874284 },
            new object[] { "RussiaTarvisio", 273.15, 10000.000, 5.787632354502, 0.760783700372 },
            new object[] { "RussiaTarvisio", 288.15, 101.325, 0.042383901089, 0.997838832207 },
            new object[] { "RussiaTarvisio", 288.15, 1000.000, 0.426478789177, 0.978694797212 },
            new object[] { "RussiaTarvisio", 288.15, 4000.000, 1.822990135900, 0.915841646906 },
            new object[] { "RussiaTarvisio", 288.15, 7000.000, 3.407371007805, 0.857478683105 },
            new object[] { "RussiaTarvisio", 288.15, 10000.000, 5.158514488570, 0.809133274732 },
            new object[] { "RussiaTarvisio", 313.15, 101.325, 0.038978227261, 0.998402024519 },
            new object[] { "RussiaTarvisio", 313.15, 1000.000, 0.390181706909, 0.984337475284 },
            new object[] { "RussiaTarvisio", 313.15, 4000.000, 1.635476055637, 0.939348454433 },
            new object[] { "RussiaTarvisio", 313.15, 7000.000, 2.989901875736, 0.899191159344 },
            new object[] { "RussiaTarvisio", 313.15, 10000.000, 4.431256591785, 0.866730391991 },
            new object[] { "Methane", 273.15, 101.325, 0.044721272552, 0.997618730697 },
            new object[] { "Methane", 273.15, 1000.000, 0.450923405593, 0.976471015794 },
            new object[] { "Methane", 273.15, 4000.000, 1.943400270755, 0.906274723804 },
            new object[] { "Methane", 273.15, 7000.000, 3.669629285819, 0.839920114886 },
            new object[] { "Methane", 273.15, 10000.000, 5.613372949744, 0.784401178840 },
            new object[] { "Methane", 288.15, 101.325, 0.042376024142, 0.998024312644 },
            new object[] { "Methane", 288.15, 1000.000, 0.425669929211, 0.980554517587 },
            new object[] { "Methane", 288.15, 4000.000, 1.807666274023, 0.923605375809 },
            new object[] { "Methane", 288.15, 7000.000, 3.353493100048, 0.871255111448 },
            new object[] { "Methane", 288.15, 10000.000, 5.040722825387, 0.828041109475 },
            new object[] { "Methane", 313.15, 101.325, 0.038972589454, 0.998546454160 },
            new object[] { "Methane", 313.15, 1000.000, 0.389613005134, 0.985774271444 },
            new object[] { "Methane", 313.15, 4000.000, 1.625385032429, 0.945180295421 },
            new object[] { "Methane", 313.15, 7000.000, 2.956724707823, 0.909280910344 },
            new object[] { "Methane", 313.15, 10000.000, 4.362168578258, 0.880457665472 },
            new object[] { "HighCO2N2", 273.15, 101.325, 0.044723870767, 0.997560774437 },
            new object[] { "HighCO2N2", 273.15, 1000.000, 0.451187193357, 0.975900119480 },
            new object[] { "HighCO2N2", 273.15, 4000.000, 1.948123357000, 0.904077525322 },
            new object[] { "HighCO2N2", 273.15, 7000.000, 3.684676682004, 0.836490068827 },
            new object[] { "HighCO2N2", 273.15, 10000.000, 5.641499006122, 0.780490496279 },
            new object[] { "HighCO2N2", 288.15, 101.325, 0.042377901948, 0.997980089207 },
            new object[] { "HighCO2N2", 288.15, 1000.000, 0.425857801789, 0.980121933508 },
            new object[] { "HighCO2N2", 288.15, 4000.000, 1.810849340248, 0.921981885101 },
            new object[] { "HighCO2N2", 288.15, 7000.000, 3.363100253678, 0.868766252635 },
            new object[] { "HighCO2N2", 288.15, 10000.000, 5.058237109071, 0.825173994592 },
            new object[] { "HighCO2N2", 313.15, 101.325, 0.038973680709, 0.998518495078 },
            new object[] { "HighCO2N2", 313.15, 1000.000, 0.389720110809, 0.985503353892 },
            new object[] { "HighCO2N2", 313.15, 4000.000, 1.627080445277, 0.944195420444 },
            new object[] { "HighCO2N2", 313.15, 7000.000, 2.961526830519, 0.907806509218 },
            new object[] { "HighCO2N2", 313.15, 10000.000, 4.370580759685, 0.878763023495 },
    };

    [Theory]
    [MemberData(nameof(NistCodeValues))]
    public void ZAndDensity_ShouldMatchNistCode(string gas, double t, double pKpa, double d, double z)
    {
        var model = new Aga8DetailModel(DetailFractions(gas));

        double density = model.Density(t, pKpa);
        double zPort = model.Calculate(Pressure.FromKilopascals(pKpa), Temperature.FromKelvins(t));

        _output.WriteLine($"{gas} T {t} K P {pKpa} kPa: Z NIST code {z:F12}, port {zPort:F12}");
        Assert.Equal(d, density, 10);
        Assert.Equal(z, zPort, 10);
    }

    [Fact]
    public void EngineMixture_ShouldMapOntoDetailComponents()
    {
        var gas = new GasMixture
        {
            Components =
            {
                With(StandardGasComponents.Methane, .90), With(StandardGasComponents.Ethane, .05), With(StandardGasComponents.Propane, .02),
                With(StandardGasComponents.IsoButane, .003), With(StandardGasComponents.NormalButane, .005),
                With(StandardGasComponents.Nitrogen, .012), With(StandardGasComponents.CarbonDioxide, .01)
            }
        };

        var fromMixture = new Aga8DetailModel(gas);
        var fromFractions = new Aga8DetailModel(DetailFractions("Concept"));
        var p = Pressure.FromMegapascals(7);
        var t = Temperature.FromKelvins(288.15);

        Assert.Equal(18.065386, fromMixture.MolarMassGramsPerMole, 9);
        Assert.Equal(fromFractions.Calculate(p, t), fromMixture.Calculate(p, t), 14);
        Assert.True(fromMixture.IsWithinValidatedRange(p, t));
        Assert.False(fromMixture.IsWithinValidatedRange(Pressure.FromMegapascals(15), t));
    }

    [Fact]
    public void Composition_ShouldBeCheckedWithoutSilentChanges()
    {
        var unknown = new GasMixture { Components = { new GasComponent { Name = "Xenon", MoleFraction = 1.0 } } };
        Assert.Throws<ArgumentException>(() => new Aga8DetailModel(unknown));

        var notNormalised = new GasMixture { Components = { With(StandardGasComponents.Methane, 0.9) } };
        Assert.Throws<ArgumentException>(() => new Aga8DetailModel(notNormalised));

        var sour = new GasMixture { Components = { With(StandardGasComponents.Methane, 0.97), With(StandardGasComponents.HydrogenSulfide, 0.03) } };
        var model = new Aga8DetailModel(sour);
        Assert.False(model.CompositionWithinNormalRange(out IReadOnlyList<string> violations));
        _output.WriteLine(string.Join("; ", violations));
        Assert.Contains(violations, v => v.StartsWith("H2S"));
    }

    /// <summary>
    /// The two parts of the normal range are independent. Gas: front-end example
    /// case R1 (TAP onshore; GGTC analysis 2026-10-16, mol %), butanes
    /// 0.3244 + 0.6872 = 1.0116 % > 1 %. At the line inlet (9.601325 MPa(a), 15 °C)
    /// and at standard conditions (101.325 kPa, 15 °C) P and T are inside
    /// 17–143 °F and ≤ 1750 psia, so only the composition is outside.
    /// </summary>
    [Fact]
    public void NormalRange_CompositionOutside_ShouldNotFailPressureTemperatureCheck()
    {
        var r1 = new GasMixture
        {
            Components =
            {
                With(StandardGasComponents.Methane, .924835), With(StandardGasComponents.Ethane, .03941),
                With(StandardGasComponents.Propane, .017646), With(StandardGasComponents.IsoButane, .003244),
                With(StandardGasComponents.NormalButane, .006872), With(StandardGasComponents.Nitrogen, .000934),
                With(StandardGasComponents.CarbonDioxide, .007059)
            }
        };
        var model = new Aga8DetailModel(r1);
        var t = Temperature.FromDegreesCelsius(15.0);

        bool composition = model.CompositionWithinNormalRange(out IReadOnlyList<string> violations);
        _output.WriteLine($"R1 composition violations: {string.Join("; ", violations)}");

        Assert.False(composition);
        // Message numbers follow the current culture (1.012 or 1,012), so only the component is asserted.
        Assert.StartsWith("butanes ", Assert.Single(violations));
        Assert.True(model.IsWithinValidatedRange(Pressure.FromKilopascals(9601.325), t));
        Assert.True(model.IsWithinValidatedRange(Pressure.FromKilopascals(101.325), t));
    }

    /// <summary>
    /// The converse: a gas inside the composition limits (the concept gas) at
    /// 20 MPa is outside the pressure part of the normal range.
    /// </summary>
    [Fact]
    public void NormalRange_PressureOutside_ShouldNotDependOnComposition()
    {
        var model = new Aga8DetailModel(DetailFractions("Concept"));
        var t = Temperature.FromDegreesCelsius(15.0);

        Assert.True(model.CompositionWithinNormalRange(out IReadOnlyList<string> violations));
        Assert.Empty(violations);
        Assert.False(model.IsWithinValidatedRange(Pressure.FromMegapascals(20.0), t));
        Assert.True(model.IsWithinValidatedRange(Pressure.FromMegapascals(7.0), t));
    }

    /// <summary>
    /// Limits of the P–T part, from the published field units: 17 °F, 143 °F
    /// and 1750 psia. Expected values computed by hand from the exact
    /// definitions (°F → K: (F + 459.67)·5/9; 1 psi = 6894.757293168361 Pa):
    /// 264.816667 K, 334.816667 K, 12.065825 MPa. Limits are inclusive.
    /// </summary>
    [Fact]
    public void NormalRange_Limits_ShouldMatchPublishedFieldUnits()
    {
        var model = new Aga8DetailModel(DetailFractions("Concept"));
        double tMin = Aga8DetailModel.NormalRangeMinimumTemperatureKelvins;
        double tMax = Aga8DetailModel.NormalRangeMaximumTemperatureKelvins;
        double pMax = Aga8DetailModel.NormalRangeMaximumPressurePascals;

        _output.WriteLine($"T_min {tMin:F6} K, T_max {tMax:F6} K, P_max {pMax / 1e6:F6} MPa");
        Assert.Equal(264.816667, tMin, 6);
        Assert.Equal(334.816667, tMax, 6);
        Assert.Equal(12.065825, pMax / 1e6, 6);

        var tIn = Temperature.FromKelvins(288.15);
        var pIn = Pressure.FromMegapascals(7.0);
        Assert.True(model.IsWithinValidatedRange(Pressure.FromPascals(pMax), tIn));
        Assert.False(model.IsWithinValidatedRange(Pressure.FromPascals(pMax + 1.0), tIn));
        Assert.True(model.IsWithinValidatedRange(pIn, Temperature.FromKelvins(tMin)));
        Assert.False(model.IsWithinValidatedRange(pIn, Temperature.FromKelvins(tMin - 0.01)));
        Assert.True(model.IsWithinValidatedRange(pIn, Temperature.FromKelvins(tMax)));
        Assert.False(model.IsWithinValidatedRange(pIn, Temperature.FromKelvins(tMax + 0.01)));
    }

    [Theory]
    [MemberData(nameof(NistReferenceData.PureFluidDensities), MemberType = typeof(NistReferenceData))]
    public void Z_ShouldMatchNistReferenceEquationsForPureFluids(string fluid, double t, double pMpa, double density)
    {
        double zReference = pMpa * 1e6 * NistReferenceData.MolarMassOf(fluid) / (density * R * t);
        var x = new double[22];
        x[fluid == "Methane" ? 1 : 2] = 1.0;
        double z = new Aga8DetailModel(x).Calculate(Pressure.FromMegapascals(pMpa), Temperature.FromKelvins(t));
        double error = (z - zReference) / zReference;

        _output.WriteLine($"{fluid} T {t} K P {pMpa} MPa: Z NIST {zReference:F6}, AGA8 {z:F6}, error {error * 100:+0.0000;-0.0000} %");

        // AGA states about 0.1 % in the normal range.
        Assert.InRange(error, -0.001, 0.001);
    }

    private static GasComponent With(GasComponent c, double fraction)
    {
        c.MoleFraction = fraction;
        return c;
    }
}
