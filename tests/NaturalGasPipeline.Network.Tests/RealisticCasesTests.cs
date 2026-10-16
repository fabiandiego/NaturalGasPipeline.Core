using NaturalGasPipeline.Core.Flow;
using NaturalGasPipeline.Hydraulics;
using NaturalGasPipeline.Thermodynamics;
using UnitsNet;
using Xunit;
using Xunit.Abstractions;

namespace NaturalGasPipeline.Network.Tests;

/// <summary>
/// The example cases shipped with the Unity front end
/// (Assets/StreamingAssets/Cases/*.json), run exactly as its EngineBridge does
/// with the default models: AGA8-DC92 DETAIL (also at standard conditions),
/// Lee–Gonzalez–Eakin, isothermal Darcy + Colebrook–White, ε = 0.0457 mm,
/// k = 1.28, method 2 with N = minimum, marching step 1 km.
///
/// R1–R3 are real pipelines with published design data (TAP onshore, Medgaz,
/// Ruby); their terrain profiles are approximate (documented end points and
/// extreme points, intermediate elevations from places near the route) and
/// some inputs are assumptions (wall thickness, minimum suction pressure).
/// S1–S2 are synthetic. Data are generated from one script together with the
/// JSON files, so the inputs here and in the front end are identical.
/// </summary>
public class RealisticCasesTests
{
    private readonly ITestOutputHelper _output;

    public RealisticCasesTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public sealed record ExampleCase(
        string Id, (string Name, double Percent)[] Composition, double StdTemperatureC, double StdPressureKPa,
        double FlowMMm3PerDay, double InletMPa, double DeliveryMPa, double FlowingC, double InternalDiameterMm,
        (double Km, double M)[] Route);

    public static readonly ExampleCase[] Cases =
    {
        new("R1", new[] { ("Methane", 92.4835), ("Ethane", 3.9409999999999985), ("Propane", 1.7645999999999993), ("IsoButane", 0.3243999999999999), ("NormalButane", 0.6871999999999998), ("Nitrogen", 0.09339999999999997), ("CarbonDioxide", 0.7058999999999997) }, 15.0, 101.325, 27.397260273972606, 9.601325, 5.0, 15.0, 1183.0, new[] { (0.0, 20.0), (60.0, 50.0), (150.0, 300.0), (230.0, 100.0), (300.0, 50.0), (380.0, 150.0), (450.0, 600.0), (520.0, 800.0), (550.0, 900.0), (580.0, 850.0), (620.0, 1800.0), (660.0, 900.0), (700.0, 300.0), (740.0, 50.0), (765.0, 10.0) }),
        new("R2", new[] { ("Methane", 85.2), ("Ethane", 9.204999999999998), ("Propane", 1.4219999999999997), ("IsoButane", 0.13099999999999998), ("NormalButane", 0.2809999999999999), ("Nitrogen", 2.0849999999999995), ("CarbonDioxide", 1.6759999999999997) }, 15.0, 101.325, 21.91780821917808, 20.101325, 8.301325, 15.0, 558.8000000000001, new[] { (0.0, 0.0), (15.0, -150.0), (71.0, -900.0), (77.0, -1700.0), (110.0, -2155.0), (150.0, -1500.0), (175.0, -500.0), (190.0, -100.0), (198.3, 0.0) }),
        new("R3", new[] { ("Methane", 96.302), ("Ethane", 1.6379999999999997), ("Propane", 0.05899999999999999), ("IsoButane", 0.005999999999999999), ("NormalButane", 0.006999999999999999), ("Nitrogen", 1.1999999999999997), ("CarbonDioxide", 0.7879999999999999) }, 15.555555555555555, 101.55977492836996, 42.47526988800001, 10.029775502162439, 5.617130834534689, 15.0, 1036.32, new[] { (0.0, 2030.0), (100.0, 2100.0), (160.0, 2000.0), (250.0, 1400.0), (330.0, 1550.0), (450.0, 1800.0), (600.0, 1650.0), (750.0, 1300.0), (900.0, 1500.0), (1000.0, 1600.0), (1086.6, 1250.0) }),
        new("S1", new[] { ("Methane", 95.55591111822237), ("Ethane", 2.632052641052821), ("Propane", 0.5820116402328046), ("IsoButane", 0.09700194003880078), ("NormalButane", 0.14900298005960116), ("Nitrogen", 0.6470129402588052), ("CarbonDioxide", 0.3370067401348027) }, 15.0, 101.325, 90.0, 7.5, 5.1, 15.0, 1376.8, new[] { (0.0, 150.0), (100.0, 180.0), (200.0, 140.0), (300.0, 220.0), (400.0, 250.0), (500.0, 190.0), (600.0, 160.0), (700.0, 210.0), (800.0, 240.0), (900.0, 170.0), (1000.0, 120.0), (1100.0, 140.0), (1200.0, 100.0) }),
        new("S2", new[] { ("Methane", 84.89), ("Ethane", 6.418), ("Propane", 2.076), ("IsoButane", 0.285), ("NormalButane", 0.667), ("Nitrogen", 4.187), ("CarbonDioxide", 1.477) }, 15.0, 101.325, 7.0, 9.5, 6.0, 15.0, 584.2, new[] { (0.0, 200.0), (40.0, 500.0), (80.0, 1200.0), (120.0, 2200.0), (160.0, 3200.0), (180.0, 4000.0), (200.0, 3600.0), (240.0, 2800.0), (280.0, 2600.0), (320.0, 2700.0), (360.0, 2500.0), (400.0, 2400.0) }),
    };

    public static TheoryData<string> Ids()
    {
        var d = new TheoryData<string>();
        foreach (ExampleCase c in Cases)
        {
            d.Add(c.Id);
        }

        return d;
    }

    [Theory]
    [MemberData(nameof(Ids))]
    public void ExampleCase_AsRunByTheFrontEnd_Report(string id)
    {
        ExampleCase c = Cases.Single(x => x.Id == id);
        StationPlan plan = Run(c, c.FlowMMm3PerDay, c.DeliveryMPa);
        Print(c.Id, c, plan);

        if (c.Id == "R1")
        {
            Print("R1 at 20 bcm/y (expansion)", c, Run(c, 20e9 / 365 / 1e6, c.DeliveryMPa));
        }

        if (c.Id == "R3")
        {
            foreach (double suctionPsig in new[] { 700.0, 900.0, 1000.0 })
            {
                double mpa = suctionPsig * 0.006894757293168361 + 0.101325;
                Print($"R3 with minimum suction {suctionPsig} psig", c, Run(c, c.FlowMMm3PerDay, mpa));
            }
        }

        Assert.True(plan.DeliveryPressure.Megapascals >= c.DeliveryMPa - 1e-6);

        // Published station counts (R1–R3) and regression values (S1–S2), see the manual, chapter 9.
        switch (c.Id)
        {
            case "R1":
                Assert.Equal(0, plan.StationCount); // TAP 10 bcm/y: no station between Kipoi and Fier
                Assert.Equal(2, Run(c, 20e9 / 365 / 1e6, c.DeliveryMPa).StationCount); // 20 bcm/y: Serres and Bilisht added
                break;
            case "R2":
                Assert.Equal(0, plan.StationCount); // Medgaz: no intermediate compression
                Assert.True(plan.DeliveryPressure.Megapascals >= 8.2 * 1.0 + 0.101325); // arrival >= 82 barg
                break;
            case "R3":
                Assert.Equal(3, plan.StationCount); // Ruby: Wildcat Hills, Wieland Flat, Desert Valley
                break;
            case "S1":
                Assert.Equal(8, plan.StationCount);
                break;
            case "S2":
                Assert.Equal(1, plan.StationCount);
                break;
        }

        // Repeated segment warnings (e.g. AGA8 above 12 MPa on every 1 km step of Medgaz) are summarised per section.
        Assert.InRange(plan.Warnings.Count, 0, 10);
    }

    private void Print(string title, ExampleCase c, StationPlan p)
    {
        _output.WriteLine($"CASE {title}: {p.Method}, route {p.RouteLength.Kilometers:F1} km, m_dot {p.MassFlow.KilogramsPerSecond:F2} kg/s, " +
                          $"N {p.StationCount}, ratio {(p.Stations.Count > 0 ? p.Stations[0].PressureRatio.ToString("F3") : "-")}, " +
                          $"P_s {(double)p.TotalIsentropicPower.Megawatts:F2} MW, delivery {p.DeliveryPressure.Megapascals:F3} MPa (target {c.DeliveryMPa:F3})");
        foreach (PlannedStation s in p.Stations)
        {
            _output.WriteLine($"  {s.Name} km {s.Chainage.Kilometers:F1} z {s.Elevation.Meters:F0} m: {s.SuctionPressure.Megapascals:F3} -> {s.DischargePressure.Megapascals:F3} MPa, {(double)s.IsentropicPower.Megawatts:F2} MW");
        }

        foreach (PipelineSection sec in p.Sections)
        {
            _output.WriteLine($"  {sec.Name}: {sec.Length.Kilometers:F1} km, {sec.InletPressure.Megapascals:F3} -> {sec.OutletPressure.Megapascals:F3} MPa, " +
                              $"friction {sec.FrictionPressureDrop.Kilopascals:F0} kPa, elevation {sec.ElevationPressureDrop.Kilopascals:F0} kPa, V_max {sec.MaximumVelocity.MetersPerSecond:F2} m/s, V/V_e {sec.MaximumErosionalRatio:F2}");
        }

        foreach (string w in p.Warnings)
        {
            _output.WriteLine($"  ! {w}");
        }
    }

    /// <summary>Mirrors NGP.Simulation.EngineBridge.Run with the default model choices.</summary>
    private static StationPlan Run(ExampleCase c, double flowMMm3PerDay, double deliveryMPa)
    {
        var gas = new GasMixture();
        foreach ((string name, double percent) in c.Composition)
        {
            GasComponent comp = name switch
            {
                "Methane" => StandardGasComponents.Methane,
                "Ethane" => StandardGasComponents.Ethane,
                "Propane" => StandardGasComponents.Propane,
                "IsoButane" => StandardGasComponents.IsoButane,
                "NormalButane" => StandardGasComponents.NormalButane,
                "Nitrogen" => StandardGasComponents.Nitrogen,
                "CarbonDioxide" => StandardGasComponents.CarbonDioxide,
                _ => StandardGasComponents.HydrogenSulfide
            };
            comp.MoleFraction = percent / 100.0;
            gas.Components.Add(comp);
        }

        gas.ValidateComposition();
        MolarMass molarMass = gas.CalculateMolarMass();
        var z = new Aga8DetailModel(gas);
        var t = Temperature.FromDegreesCelsius(c.FlowingC);
        var tStd = Temperature.FromDegreesCelsius(c.StdTemperatureC);
        var pStd = Pressure.FromKilopascals(c.StdPressureKPa);
        var standard = new StandardFlowConditions { Temperature = tStd, Pressure = pStd, CompressibilityFactor = z.Calculate(pStd, tStd) };
        var q = new StandardVolumetricFlow(VolumeFlow.FromCubicMetersPerSecond(flowMMm3PerDay * 1.0e6 / 86400.0), standard);

        var model = new IsothermalSegmentModel(molarMass, z, new LeeGonzalezEakinViscosityModel(), new ColebrookWhiteModel());
        var planner = new CompressorStationPlanner(model, t, molarMass, z, 1.28);
        ProfilePoint[] route = c.Route.Select(p => new ProfilePoint(Length.FromKilometers(p.Km), Length.FromMeters(p.M))).ToArray();
        StationPlanningCriteria criteria = StationPlanningCriteria.Create(
            Pressure.FromMegapascals(c.InletMPa), Pressure.FromMegapascals(deliveryMPa), null, null, Length.FromKilometers(1.0));
        StationPlanningProblem problem = planner.ProblemFor(
            route, Length.FromMillimeters(c.InternalDiameterMm), Length.FromMillimeters(0.0457), q, criteria);
        StationCountComparison comparison = planner.CompareStationCounts(problem, 3);

        if (comparison.MinimumStationCount == 0)
        {
            return comparison.MaximumSpacing;
        }

        StationCountOption? option = comparison.Options.FirstOrDefault(o => o.StationCount == comparison.MinimumStationCount && o.Feasible);
        return option?.Plan ?? planner.Plan(problem, new EqualRatioPlacement(comparison.MinimumStationCount));
    }
}
