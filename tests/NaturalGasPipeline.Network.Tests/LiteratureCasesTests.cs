using NaturalGasPipeline.Core.Flow;
using NaturalGasPipeline.Correlations;
using NaturalGasPipeline.Hydraulics;
using NaturalGasPipeline.Thermodynamics;
using UnitsNet;
using Xunit;
using Xunit.Abstractions;

namespace NaturalGasPipeline.Network.Tests;

/// <summary>
/// The five literature example cases shipped with the Unity front end
/// (StreamingAssets/Cases/*.json), run through the station planner exactly as
/// the front end's EngineBridge does: composition → M and SG (ISO 6976 air),
/// constant Z at flowing conditions with Z = 1 at standard conditions (as in
/// the textbooks), standard flow → mass flow, a flat two-point route, and a
/// target at B below the arrival pressure so that no station is placed and the
/// delivery pressure at B is the pipe's outlet pressure.
///
/// Sources (published values):
/// [A&amp;S] Arnold &amp; Stewart, Surface Production Operations, Vol. 1, 3rd ed.
///        (2016), ch. 6, Example 6.6a — project document.
/// [CED]  CED Engineering course P06-001 "Gas Pipeline Hydraulics" (after
///        Menon, Gas Pipeline Hydraulics, 2005), Examples 11 and 12; flowing
///        temperature 70 °F as established in GasFlowEquationValidationTests.
///
/// The books give only the gas gravity G; the cases use a methane–ethane
/// (G 0.6) or methane–propane (G 0.85) binary whose SG equals G with ISO 6976
/// air. With constant Z and a constant friction factor (or an empirical
/// equation that does not use the viscosity) the result depends on G, not on
/// the components.
/// </summary>
public class LiteratureCasesTests
{
    private const double Psi = 6894.757293168361;

    private readonly ITestOutputHelper _output;

    public LiteratureCasesTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public enum Flow { IsothermalDarcy, Weymouth, PanhandleA, PanhandleB, GeneralFlow }

    public sealed record LiteratureCase(
        string Id, string Source, string Heavy, double HeavyPercent, double Z,
        double Mmscfd, double InletPsia, double FlowingRankine, double DiameterInches, double LengthKm,
        Flow Model, double Efficiency, double DarcyF, double PublishedOutletPsia, double TolerancePsi);

    // Standard conditions of both books: 520 °R, 14.7 psia (Z = 1).
    private static readonly Temperature Tb = Temperature.FromDegreesRankine(520.0);
    private static readonly Pressure Pb = Pressure.FromPoundsForcePerSquareInch(14.7);

    public static readonly LiteratureCase[] Cases =
    {
        new("L1", "A&S Ex. 6.6a (A), general equation, 6 in, f = 0.015 (Moody chart)",
            "Propane", 30.575465174306693, 0.67, 23.0, 915.0, 540.0, 6.0, 2.1336,
            Flow.IsothermalDarcy, 1.0, 0.015, 883.0, 0.005 * 883.0),
        new("L2", "A&S Ex. 6.6a (C), Panhandle B, E = 0.95, 4 in",
            "Propane", 30.575465174306693, 0.67, 23.0, 915.0, 540.0, 4.0, 2.1336,
            Flow.PanhandleB, 0.95, 0.0, 771.0, 0.5),
        new("L3", "CED Ex. 11, Weymouth, E = 0.95, NPS 18 (D 17.5 in), 20 mi",
            "Ethane", 9.526456120339356, 0.85, 150.0, 1014.7, 530.0, 17.5, 32.18688,
            Flow.Weymouth, 0.95, 0.0, 946.24, 0.05),
        new("L4", "CED Ex. 11, Panhandle A, E = 0.95, NPS 18 (D 17.5 in), 20 mi",
            "Ethane", 9.526456120339356, 0.85, 150.0, 1014.7, 530.0, 17.5, 32.18688,
            Flow.PanhandleA, 0.95, 0.0, 970.81, 0.05),
        new("L5", "CED Ex. 12, General Flow, F = 20 (f = 0.01), NPS 16 (D 15.5 in), 100 mi; run forward from P1 = 1180.44 psig",
            "Ethane", 9.526456120339356, 0.844, 100.0, 1195.14, 530.0, 15.5, 160.9344,
            Flow.GeneralFlow, 1.0, 0.01, 1014.7, 0.05),
    };

    public static TheoryData<string> CaseIds()
    {
        var data = new TheoryData<string>();
        foreach (LiteratureCase c in Cases)
        {
            data.Add(c.Id);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(CaseIds))]
    public void LiteratureCase_AsRunByTheFrontEnd_ShouldReproduceThePublishedOutletPressure(string id)
    {
        LiteratureCase c = Cases.Single(x => x.Id == id);
        StationPlan plan = Run(c, out double sg, out MassFlow massFlow);
        double p2 = plan.DeliveryPressure.Pascals / Psi;

        _output.WriteLine($"{c.Id}: {c.Source}");
        _output.WriteLine($"  SG {sg:F6} (book {(c.Heavy == "Propane" ? 0.85 : 0.6)}), Z {c.Z}, m_dot {massFlow.KilogramsPerSecond:F4} kg/s, stations {plan.StationCount}");
        _output.WriteLine($"  P1 {c.InletPsia} psia ({c.InletPsia * Psi / 1e6:F5} MPa) -> P2 {p2:F3} psia ({plan.DeliveryPressure.Megapascals:F5} MPa); published {c.PublishedOutletPsia} psia; diff {p2 - c.PublishedOutletPsia:+0.000;-0.000} psi ({(p2 / c.PublishedOutletPsia - 1) * 100:+0.000;-0.000} %)");
        foreach (PipelineSection sec in plan.Sections)
        {
            _output.WriteLine($"  V_max {sec.MaximumVelocity.MetersPerSecond:F2} m/s, V/V_e {sec.MaximumErosionalRatio:F2}");
        }

        foreach (string w in plan.Warnings)
        {
            _output.WriteLine($"  ! {w}");
        }

        Assert.Equal(0, plan.StationCount);
        Assert.Equal(c.Heavy == "Propane" ? 0.85 : 0.6, sg, 1e-9);
        Assert.InRange(p2 - c.PublishedOutletPsia, -c.TolerancePsi, c.TolerancePsi);
    }

    /// <summary>Mirrors NGP.Simulation.EngineBridge.Run for these inputs (front end, Assets/_Project/Scripts/Simulation).</summary>
    private static StationPlan Run(LiteratureCase c, out double sg, out MassFlow massFlow)
    {
        var gas = new GasMixture();
        GasComponent ch4 = StandardGasComponents.Methane;
        ch4.MoleFraction = (100.0 - c.HeavyPercent) / 100.0;
        GasComponent heavy = c.Heavy == "Propane" ? StandardGasComponents.Propane : StandardGasComponents.Ethane;
        heavy.MoleFraction = c.HeavyPercent / 100.0;
        gas.Components.Add(ch4);
        gas.Components.Add(heavy);
        gas.ValidateComposition();

        MolarMass molarMass = gas.CalculateMolarMass();
        sg = SpecificGravity.OfMixture(gas, DryAirMolarMass.Iso6976_2016);

        var z = new ConstantCompressibilityModel(c.Z);
        var t = Temperature.FromDegreesRankine(c.FlowingRankine);
        var standard = new StandardFlowConditions { Temperature = Tb, Pressure = Pb, CompressibilityFactor = 1.0 };
        var q = new StandardVolumetricFlow(VolumeFlow.FromCubicMetersPerSecond(c.Mmscfd * 0.028316846592 * 1.0e6 / 86400.0), standard);
        var viscosity = new LeeGonzalezEakinViscosityModel();

        ISegmentModel model;
        IFrictionFactorModel friction = c.DarcyF > 0.0 ? new ConstantDarcyFrictionModel(c.DarcyF) : new ColebrookWhiteModel();
        if (c.Model == Flow.IsothermalDarcy)
        {
            model = new IsothermalSegmentModel(molarMass, z, viscosity, friction);
        }
        else
        {
            IGasFlowEquation equation = c.Model switch
            {
                Flow.Weymouth => GasFlowEquations.Weymouth(c.Efficiency),
                Flow.PanhandleA => GasFlowEquations.PanhandleA(c.Efficiency),
                Flow.PanhandleB => GasFlowEquations.PanhandleB(c.Efficiency),
                _ => new GeneralFlowEquation(friction)
            };
            model = new FlowEquationSegmentModel(equation, new FlowEquationGas(sg, DryAirMolarMass.Iso6976_2016, z, viscosity), standard);
        }

        var planner = new CompressorStationPlanner(model, t, molarMass, z, 1.28);
        ProfilePoint[] route =
        {
            new(Length.Zero, Length.Zero),
            new(Length.FromKilometers(c.LengthKm), Length.Zero)
        };

        StationPlanningCriteria criteria = StationPlanningCriteria.Create(
            Pressure.FromPoundsForcePerSquareInch(c.InletPsia),
            Pressure.FromMegapascals(4.0),
            null,
            null,
            Length.FromKilometers(1.0));

        StationPlanningProblem problem = planner.ProblemFor(
            route, Length.FromInches(c.DiameterInches), Length.FromMillimeters(0.0457), q, criteria);

        StationCountComparison comparison = planner.CompareStationCounts(problem, 3);
        massFlow = comparison.MaximumSpacing.MassFlow;

        // EngineBridge.Choose: with no station needed, the maximum-spacing plan is shown.
        return comparison.MaximumSpacing;
    }
}
