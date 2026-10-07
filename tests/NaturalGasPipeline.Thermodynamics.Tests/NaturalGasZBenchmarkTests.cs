using System.Text;
using UnitsNet;
using Xunit;
using Xunit.Abstractions;

namespace NaturalGasPipeline.Thermodynamics.Tests;

/// <summary>
/// LEVEL 2 — reliability of every Z option for NATURAL GAS, measured against
/// AGA8-DC92 (ISO 12213-2, ≈0.1 % in its normal range; itself checked
/// against NIST in <see cref="Aga8DetailTests"/>).
///
/// Gases: published pipeline and LNG compositions (Snam 2014–15 entry
/// points, Marcogaz 2022–23 LNG) mapped onto the engine components as the
/// front end does, the project concept gas, and a high-inert gas. All lie in
/// the AGA8 normal range. Conditions: 273–313 K, 0.101325–10 MPa.
///
/// The same composition goes to every model, so the deviations measure the
/// models (correlation + pseudo-criticals + correction, or EOS mixing rule),
/// not the composition.
/// </summary>
public class NaturalGasZBenchmarkTests
{
    private readonly ITestOutputHelper _output;

    public NaturalGasZBenchmarkTests(ITestOutputHelper output)
    {
        _output = output;
    }

    internal static readonly (string Name, (string Component, double Percent)[] Composition)[] Gases =
    {
        ("Concept gas", new[] { ("Methane", 90.0), ("Ethane", 5.0), ("Propane", 2.0), ("IsoButane", 0.3), ("NormalButane", 0.5), ("Nitrogen", 1.2), ("CarbonDioxide", 1.0) }),
        ("Russia (Tarvisio)", new[] { ("Methane", 95.554), ("Ethane", 2.632), ("Propane", 0.582), ("IsoButane", 0.097), ("NormalButane", 0.149), ("Nitrogen", 0.647), ("CarbonDioxide", 0.337) }),
        ("North Europe (Passo Gries)", new[] { ("Methane", 90.807), ("Ethane", 4.733), ("Propane", 0.821), ("IsoButane", 0.141), ("NormalButane", 0.234), ("Nitrogen", 1.942), ("CarbonDioxide", 1.322) }),
        ("Algeria (Mazara)", new[] { ("Methane", 85.200), ("Ethane", 9.205), ("Propane", 1.422), ("IsoButane", 0.131), ("NormalButane", 0.281), ("Nitrogen", 2.085), ("CarbonDioxide", 1.676) }),
        ("Libya (Gela)", new[] { ("Methane", 84.890), ("Ethane", 6.418), ("Propane", 2.076), ("IsoButane", 0.285), ("NormalButane", 0.667), ("Nitrogen", 4.187), ("CarbonDioxide", 1.477) }),
        ("LNG Qatar", new[] { ("Methane", 93.018), ("Ethane", 6.342), ("Propane", 0.260), ("IsoButane", 0.040), ("NormalButane", 0.059), ("Nitrogen", 0.281) }),
        ("LNG Nigeria", new[] { ("Methane", 90.941), ("Ethane", 5.990), ("Propane", 2.345), ("IsoButane", 0.382), ("NormalButane", 0.327), ("Nitrogen", 0.015) }),
        ("High inerts (10 % N2, 8 % CO2)", new[] { ("Methane", 80.0), ("Ethane", 2.0), ("Nitrogen", 10.0), ("CarbonDioxide", 8.0) }),
    };

    private static readonly double[] Temperatures = { 273.15, 288.15, 313.15 };
    private static readonly double[] PressuresMpa = { 0.101325, 1.0, 2.0, 4.0, 7.0, 10.0 };

    internal static GasMixture Mixture((string Component, double Percent)[] composition)
    {
        double total = composition.Sum(c => c.Percent);
        var gas = new GasMixture();
        foreach ((string name, double percent) in composition)
        {
            GasComponent c = name switch
            {
                "Methane" => StandardGasComponents.Methane,
                "Ethane" => StandardGasComponents.Ethane,
                "Propane" => StandardGasComponents.Propane,
                "IsoButane" => StandardGasComponents.IsoButane,
                "NormalButane" => StandardGasComponents.NormalButane,
                "Nitrogen" => StandardGasComponents.Nitrogen,
                "CarbonDioxide" => StandardGasComponents.CarbonDioxide,
                "HydrogenSulfide" => StandardGasComponents.HydrogenSulfide,
                _ => throw new ArgumentOutOfRangeException(name)
            };
            c.MoleFraction = percent / total;
            gas.Components.Add(c);
        }

        return gas;
    }

    private static double Fraction(GasMixture gas, string name) => gas.Components.Where(c => c.Name == name).Sum(c => c.MoleFraction);

    /// <summary>The model options offered by the front end.</summary>
    internal static IEnumerable<(string Name, Func<GasMixture, ICompressibilityModel> Build)> Models()
    {
        PseudoCriticalProperties Kay(GasMixture g) => PseudoCriticalProperties.FromKay(g);
        double Sg(GasMixture g) => SpecificGravity.OfMixture(g, DryAirMolarMass.Iso6976_2016);
        PseudoCriticalProperties Sutton(GasMixture g) => new SuttonPseudoCriticalCorrelation().Calculate(Sg(g));
        PseudoCriticalProperties Standing(GasMixture g) =>
            new StandingPseudoCriticalCorrelation(StandingGasSystem.NaturalGas, StandingCoefficientSet.WhitsonBrule).Calculate(Sg(g));
        PseudoCriticalProperties Wa(GasMixture g, PseudoCriticalProperties pc) =>
            WichertAzizCorrection.Apply(pc, Fraction(g, "CarbonDioxide"), Fraction(g, "HydrogenSulfide"));
        PseudoCriticalProperties Ckb(GasMixture g, PseudoCriticalProperties pc) =>
            CarrKobayashiBurrowsCorrection.Apply(pc, Fraction(g, "CarbonDioxide"), Fraction(g, "HydrogenSulfide"), Fraction(g, "Nitrogen"));

        yield return ("DAK · Kay + Wichert-Aziz", g => CompressibilityModelFactory.Create(CorrespondingStatesModelType.DranchukAbouKassem, Wa(g, Kay(g))));
        yield return ("HY · Kay + Wichert-Aziz", g => CompressibilityModelFactory.Create(CorrespondingStatesModelType.HallYarborough, Wa(g, Kay(g))));
        yield return ("Papay · Kay + Wichert-Aziz", g => CompressibilityModelFactory.Create(CorrespondingStatesModelType.Papay, Wa(g, Kay(g))));
        yield return ("DAK · Kay, no correction", g => CompressibilityModelFactory.Create(CorrespondingStatesModelType.DranchukAbouKassem, Kay(g)));
        yield return ("DAK · Kay + Carr-Kobayashi-Burrows", g => CompressibilityModelFactory.Create(CorrespondingStatesModelType.DranchukAbouKassem, Ckb(g, Kay(g))));
        yield return ("DAK · Sutton + Wichert-Aziz", g => CompressibilityModelFactory.Create(CorrespondingStatesModelType.DranchukAbouKassem, Wa(g, Sutton(g))));
        yield return ("DAK · Standing + Wichert-Aziz", g => CompressibilityModelFactory.Create(CorrespondingStatesModelType.DranchukAbouKassem, Wa(g, Standing(g))));
        yield return ("Peng-Robinson (kij = 0)", g => new PengRobinsonModel(g));
        yield return ("SRK (kij = 0)", g => new SoaveRedlichKwongModel(g));
    }

    /// <summary>
    /// Measured max |ΔZ/Z| against AGA8 for P ≥ 1 MPa (2026-10-06), rounded up:
    /// DAK·Kay+WA 1.30, HY·Kay+WA 1.34, Papay 3.78, DAK·Kay 3.59, DAK·Kay+CKB 6.65,
    /// DAK·Sutton+WA 9.08, DAK·Standing+WA 13.92, PR (kij = 0) 4.16, SRK (kij = 0) 2.84.
    /// The specific-gravity methods (Sutton, Standing) and CKB fail on the high-inert gas.
    /// </summary>
    internal static double Band(string model) => model switch
    {
        "DAK · Kay + Wichert-Aziz" => 1.4,
        "HY · Kay + Wichert-Aziz" => 1.4,
        "Papay · Kay + Wichert-Aziz" => 3.9,
        "DAK · Kay, no correction" => 3.7,
        "DAK · Kay + Carr-Kobayashi-Burrows" => 6.8,
        "DAK · Sutton + Wichert-Aziz" => 9.2,
        "DAK · Standing + Wichert-Aziz" => 14.0,
        "Peng-Robinson (kij = 0)" => 4.3,
        "SRK (kij = 0)" => 2.9,
        _ => throw new ArgumentOutOfRangeException(nameof(model))
    };

    [Fact]
    public void AllModels_AgainstAga8_ForNaturalGases_Report()
    {
        var report = new StringBuilder();
        report.AppendLine("Z deviation from AGA8-DC92, 100·(Z_model − Z_AGA8)/Z_AGA8, % — 8 natural gases × 3 T × 6 P");
        report.AppendLine("Model                               | max|d| all | max|d| P≥1MPa | mean d  | max|d| std cond | in-range max|d|");

        var worstByModel = new Dictionary<string, double>();

        foreach ((string modelName, Func<GasMixture, ICompressibilityModel> build) in Models())
        {
            double maxAll = 0, maxPipe = 0, sum = 0, maxStd = 0, maxInRange = 0;
            int n = 0;

            foreach ((string _, (string, double)[] composition) in Gases)
            {
                GasMixture gas = Mixture(composition);
                var aga8 = new Aga8DetailModel(gas);
                ICompressibilityModel model = build(gas);

                foreach (double t in Temperatures)
                {
                    foreach (double pMpa in PressuresMpa)
                    {
                        var p = Pressure.FromMegapascals(pMpa);
                        var temperature = Temperature.FromKelvins(t);
                        Assert.True(aga8.IsWithinValidatedRange(p, temperature));

                        double zRef = aga8.Calculate(p, temperature);
                        double d = (model.Calculate(p, temperature) - zRef) / zRef * 100.0;
                        double ad = Math.Abs(d);
                        maxAll = Math.Max(maxAll, ad);
                        sum += d;
                        n++;
                        if (pMpa >= 1.0)
                        {
                            maxPipe = Math.Max(maxPipe, ad);
                        }

                        if (pMpa < 0.2 && Math.Abs(t - 288.15) < 1e-9)
                        {
                            maxStd = Math.Max(maxStd, ad);
                        }

                        bool inRange = model is not ICompressibilityCorrelation c || c.IsWithinValidatedRange(p, temperature);
                        if (inRange)
                        {
                            maxInRange = Math.Max(maxInRange, ad);
                        }
                    }
                }
            }

            worstByModel[modelName] = maxPipe;
            Assert.True(maxStd < 0.1, $"{modelName}: Z at standard conditions {maxStd:F3} % from AGA8");
            report.AppendLine($"{modelName,-36}| {maxAll,10:F3} | {maxPipe,13:F3} | {sum / n,7:+0.000;-0.000} | {maxStd,15:F4} | {maxInRange,10:F3}");
        }

        report.AppendLine();
        report.AppendLine("Per gas, P ≥ 1 MPa, max|d| (%):");
        report.Append($"{"Gas",-32}");
        List<(string Name, Func<GasMixture, ICompressibilityModel> Build)> models = Models().ToList();
        foreach ((string name, _) in models)
        {
            report.Append($"| {name.Split(' ')[0] + (name.Contains("Sutton") ? "-Sut" : name.Contains("Standing") ? "-Std" : name.Contains("no corr") ? "-noC" : name.Contains("Carr") ? "-CKB" : ""),-9}");
        }

        report.AppendLine();
        foreach ((string gasName, (string, double)[] composition) in Gases)
        {
            GasMixture gas = Mixture(composition);
            var aga8 = new Aga8DetailModel(gas);
            report.Append($"{gasName,-32}");
            foreach ((string _, Func<GasMixture, ICompressibilityModel> build) in models)
            {
                ICompressibilityModel model = build(gas);
                double worst = 0;
                foreach (double t in Temperatures)
                {
                    foreach (double pMpa in PressuresMpa.Where(x => x >= 1.0))
                    {
                        var p = Pressure.FromMegapascals(pMpa);
                        var temperature = Temperature.FromKelvins(t);
                        double zRef = aga8.Calculate(p, temperature);
                        worst = Math.Max(worst, Math.Abs((model.Calculate(p, temperature) - zRef) / zRef * 100.0));
                    }
                }

                report.Append($"| {worst,9:F3}");
            }

            report.AppendLine();
        }

        _output.WriteLine(report.ToString());

        // Bands = measured maximum (2026-10-06) rounded up. They record what each option
        // achieves for natural gas; changing one needs a documented reason.
        foreach (KeyValuePair<string, double> kv in worstByModel)
        {
            double band = Band(kv.Key);
            Assert.True(kv.Value <= band, $"{kv.Key}: {kv.Value:F2} % from AGA8 (documented band {band} %)");
        }
    }

    [Fact]
    public void ConceptGas_ZAtPipelineConditions_Report()
    {
        GasMixture gas = Mixture(Gases[0].Composition);
        var aga8 = new Aga8DetailModel(gas);
        var sb = new StringBuilder("Concept gas, Z by model (AGA8 reference)\n");
        sb.AppendLine($"{"Model",-36}| 288.15 K 0.101325 MPa | 288.15 K 7 MPa | 288.15 K 5 MPa");
        foreach ((string name, Func<GasMixture, ICompressibilityModel> build) in Models().Prepend(("AGA8-DC92", (Func<GasMixture, ICompressibilityModel>)(g => new Aga8DetailModel(g)))))
        {
            ICompressibilityModel m = build(gas);
            double Z(double pMpa) => m.Calculate(Pressure.FromMegapascals(pMpa), Temperature.FromKelvins(288.15));
            sb.AppendLine($"{name,-36}| {Z(0.101325),21:F5} | {Z(7),14:F5} | {Z(5),14:F5}");
        }

        _output.WriteLine(sb.ToString());
        Assert.Equal(0.834429172204, aga8.Calculate(Pressure.FromMegapascals(7), Temperature.FromKelvins(288.15)), 9);
    }
}
