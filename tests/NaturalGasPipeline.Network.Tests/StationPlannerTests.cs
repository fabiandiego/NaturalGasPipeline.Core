using NaturalGasPipeline.Compression;
using NaturalGasPipeline.Core.Flow;
using NaturalGasPipeline.Core.Numerics;
using NaturalGasPipeline.Core.Pipeline;
using NaturalGasPipeline.Hydraulics;
using NaturalGasPipeline.Thermodynamics;
using UnitsNet;
using Xunit;
using Xunit.Abstractions;

namespace NaturalGasPipeline.Network.Tests;

/// <summary>
/// Compressor station placement and the friction / elevation split.
///
/// Analytic references use a constant Z and a constant Darcy f, for which the
/// isothermal momentum balance integrates exactly and composes exactly over
/// any subdivision of a uniform slope:
///   flat:   P1² − P2² = K·L,                 K = f·G²·a/D, a = Z·R·T/M
///   slope:  P1² − e^s·P2² = K·L·(e^s − 1)/s,  s = 2·g·Δz/a
/// so station spacings can be checked to the root-finder tolerance.
/// </summary>
public class StationPlannerTests
{
    private const double R = 8.31446261815324;
    private const double G = 9.80665;

    private readonly ITestOutputHelper _output;

    public StationPlannerTests(ITestOutputHelper output)
    {
        _output = output;
    }

    // ---- analytic gas and pipe
    private static readonly MolarMass M = MolarMass.FromKilogramsPerMole(0.018065);
    private const double Z = 0.85;
    private const double F = 0.0085;
    private const double D = 0.882;
    private static readonly Temperature T = Temperature.FromKelvins(288.15);
    private static readonly MassFlow Flow = MassFlow.FromKilogramsPerSecond(245.0);
    private static readonly Length Roughness = Length.FromMillimeters(0.0457);

    private static double A => Z * R * T.Kelvins / M.KilogramsPerMole;

    private static double K
    {
        get
        {
            double area = System.Math.PI * D * D / 4.0;
            double g = Flow.KilogramsPerSecond / area;
            return F * g * g * A / D;
        }
    }

    private static CompressorStationPlanner AnalyticPlanner() =>
        new(
            new IsothermalSegmentModel(
                M,
                new ConstantCompressibilityModel(Z),
                new ConstantViscosityModel(DynamicViscosity.FromCentipoise(0.011)),
                new ConstantDarcyFrictionModel(F)),
            T,
            M,
            new ConstantCompressibilityModel(Z),
            1.28);

    private static ProfilePoint[] Straight(double lengthMeters, double riseMeters) =>
        new[]
        {
            new ProfilePoint(Length.Zero, Length.FromMeters(500.0)),
            new ProfilePoint(Length.FromMeters(lengthMeters), Length.FromMeters(500.0 + riseMeters))
        };

    private static StationPlanningCriteria Criteria(double pA, double pB, double? pMin = null) => new()
    {
        InletPressure = Pressure.FromMegapascals(pA),
        DeliveryPressure = Pressure.FromMegapascals(pB),
        MinimumSuctionPressure = pMin is double v ? Pressure.FromMegapascals(v) : null
    };

    private static StationPlanningProblem Problem(ProfilePoint[] route, StationPlanningCriteria criteria) => new()
    {
        Route = route,
        InternalDiameter = Length.FromMeters(D),
        AbsoluteRoughness = Roughness,
        MassFlow = Flow,
        Criteria = criteria
    };

    private static StationPlan Greedy(ProfilePoint[] route, StationPlanningCriteria criteria) =>
        AnalyticPlanner().Plan(Problem(route, criteria), new MaximumSpacingPlacement());

    private static StationPlan EqualRatio(ProfilePoint[] route, StationPlanningCriteria criteria, int n) =>
        AnalyticPlanner().Plan(Problem(route, criteria), new EqualRatioPlacement(n));

    private void Print(StationPlan plan)
    {
        _output.WriteLine($"[{plan.Method}]");
        _output.WriteLine($"ṁ = {plan.MassFlow.KilogramsPerSecond:F2} kg/s, route {plan.RouteLength.Kilometers:F3} km, " +
                          $"delivery {plan.DeliveryPressure.Megapascals:F6} MPa (target {plan.DeliveryPressureTarget.Megapascals:F3})");

        foreach (PlannedStation s in plan.Stations)
        {
            _output.WriteLine(
                $"  {s.Name}: km {s.Chainage.Kilometers,9:F4} (+{s.DistanceFromPrevious.Kilometers,8:F3}), z {s.Elevation.Meters,6:F1} m, " +
                $"{s.SuctionPressure.Megapascals:F4} → {s.DischargePressure.Megapascals:F4} MPa, r {s.PressureRatio:F4}, " +
                $"P_s {(double)s.IsentropicPower.Megawatts:F3} MW, T2s {s.IsentropicDischargeTemperature.DegreesCelsius:F1} °C");
        }

        foreach (PipelineSection sec in plan.Sections)
        {
            _output.WriteLine(
                $"  {sec.Name,-12} {sec.Length.Kilometers,8:F3} km: ΔP {sec.PressureDrop.Kilopascals,9:F1} kPa = " +
                $"friction {sec.FrictionPressureDrop.Kilopascals,9:F1} + elevation {sec.ElevationPressureDrop.Kilopascals,8:F1}");
        }

        foreach (string w in plan.Warnings)
        {
            _output.WriteLine($"  ! {w}");
        }
    }

    // ------------------------------------------------------------ friction / elevation split

    [Fact]
    public void Split_FlatSegment_ShouldBeAllFriction()
    {
        var model = new IsothermalSegmentModel(M, new ConstantCompressibilityModel(Z),
            new ConstantViscosityModel(DynamicViscosity.FromCentipoise(0.011)), new ConstantDarcyFrictionModel(F));

        SegmentResult r = model.SolveOutletPressure(
            new PipeSegment(Length.FromMeters(D), Length.FromKilometers(50), Roughness, Length.Zero), T, Pressure.FromMegapascals(7), Flow);

        _output.WriteLine($"ΔP {r.PressureDrop.Kilopascals:F3} kPa, friction {r.FrictionPressureDrop.Kilopascals:F3}, elevation {r.ElevationPressureDrop.Kilopascals:F3}");
        Assert.Equal(0.0, r.ElevationPressureDrop.Pascals, 12);
        Assert.Equal(r.PressureDrop.Pascals, r.FrictionPressureDrop.Pascals, 6);
    }

    [Fact]
    public void Split_NearlyStaticUphillGas_ShouldBeHydrostatic()
    {
        // Almost no flow: the drop is the gas column, P2 = P1·e^(−s/2) exactly for constant Z.
        var model = new IsothermalSegmentModel(M, new ConstantCompressibilityModel(Z),
            new ConstantViscosityModel(DynamicViscosity.FromCentipoise(0.011)), new ConstantDarcyFrictionModel(F));

        const double rise = 1000.0;
        SegmentResult r = model.SolveOutletPressure(
            new PipeSegment(Length.FromMeters(D), Length.FromKilometers(10), Roughness, Length.FromMeters(rise)),
            T, Pressure.FromMegapascals(7), MassFlow.FromKilogramsPerSecond(0.01));

        double s = 2.0 * G * rise / A;
        double hydrostatic = 7.0e6 * (1.0 - System.Math.Exp(-s / 2.0));

        _output.WriteLine($"ΔP {r.PressureDrop.Pascals:F2} Pa, hydrostatic exact {hydrostatic:F2} Pa, " +
                          $"elevation part {r.ElevationPressureDrop.Pascals:F2} Pa, friction part {r.FrictionPressureDrop.Pascals:F4} Pa");

        Assert.Equal(hydrostatic, r.PressureDrop.Pascals, hydrostatic * 1e-6);
        Assert.Equal(hydrostatic, r.ElevationPressureDrop.Pascals, hydrostatic * 1e-6);
        Assert.InRange(System.Math.Abs(r.FrictionPressureDrop.Pascals), 0.0, hydrostatic * 1e-6);
    }

    // ------------------------------------------------------------ placement, analytic

    [Fact]
    public void FlatRoute_ShouldPlaceStationsAtTheAnalyticSpacing()
    {
        double pd = 7.0e6, pmin = 5.0e6;
        double spacing = (pd * pd - pmin * pmin) / K;
        double length = 3.4 * spacing;

        StationPlan plan = Greedy(Straight(length, 0.0), Criteria(7.0, 5.0));
        Print(plan);
        _output.WriteLine($"analytic spacing {spacing / 1e3:F4} km");

        Assert.Equal(3, plan.Stations.Count);

        for (int i = 0; i < 3; i++)
        {
            Assert.Equal((i + 1) * spacing, plan.Stations[i].Chainage.Meters, 0.05);
            Assert.Equal(pmin, plan.Stations[i].SuctionPressure.Pascals, 1.0);
        }

        // Last station discharges only what B needs over the remaining 0.4·spacing.
        double required = System.Math.Sqrt(pmin * pmin + K * 0.4 * spacing);
        Assert.Equal(pd, plan.Stations[0].DischargePressure.Pascals, 1e-6);
        Assert.Equal(required, plan.Stations[2].DischargePressure.Pascals, 2.0);
        Assert.Equal(5.0e6, plan.DeliveryPressure.Pascals, 1.0);
        Assert.Equal(4, plan.Sections.Count);
    }

    [Fact]
    public void UniformUphill_ShouldMatchTheAnalyticSlopeEquation()
    {
        double pd = 7.0e6, pmin = 5.0e6;
        double length = 400e3, rise = 1500.0;
        double sinTheta = rise / length;

        // Reference: Pd² − e^s·Pmin² = K·ℓ·(e^s − 1)/s, s = 2·g·ℓ·sinθ/a, solved for ℓ.
        double Residual(double l)
        {
            double s = 2.0 * G * l * sinTheta / A;
            return pd * pd - System.Math.Exp(s) * pmin * pmin - K * l * (System.Math.Exp(s) - 1.0) / s;
        }

        double reference = BrentRootSolver.FindRoot(Residual, 1.0, length, 1e-6);
        double flat = (pd * pd - pmin * pmin) / K;

        StationPlan plan = Greedy(Straight(length, rise), Criteria(7.0, 5.0));
        Print(plan);
        _output.WriteLine($"analytic first spacing uphill {reference / 1e3:F4} km (flat {flat / 1e3:F4} km)");

        Assert.Equal(reference, plan.Stations[0].Chainage.Meters, 0.05);
        Assert.True(reference < flat);

        PipelineSection first = plan.Sections[0];
        Assert.True(first.ElevationPressureDrop.Pascals > 0.0);
        Assert.Equal(first.PressureDrop.Pascals, first.FrictionPressureDrop.Pascals + first.ElevationPressureDrop.Pascals, 1e-3);
    }

    [Fact]
    public void ArrivalBelowTarget_ShouldAddAStationThatDeliversExactlyTheTarget()
    {
        // Stations at 4 MPa suction, delivery 5 MPa: the remainder after the
        // first station arrives between 4 and 5 MPa, so one more station is
        // placed where the backward profile from B reaches 7 MPa.
        double pd = 7.0e6, pmin = 4.0e6, pb = 5.0e6;
        double l4 = (pd * pd - pmin * pmin) / K;
        double l5 = (pd * pd - pb * pb) / K;
        double length = l4 + 0.5 * (l4 + l5);

        StationPlan plan = Greedy(Straight(length, 0.0), Criteria(7.0, 5.0, pMin: 4.0));
        Print(plan);

        Assert.Equal(2, plan.Stations.Count);
        Assert.Equal(l4, plan.Stations[0].Chainage.Meters, 0.05);
        Assert.Equal(length - l5, plan.Stations[1].Chainage.Meters, 0.05);

        double suction2 = System.Math.Sqrt(pd * pd - K * (length - l5 - l4));
        Assert.Equal(suction2, plan.Stations[1].SuctionPressure.Pascals, 2.0);
        Assert.Equal(pd, plan.Stations[1].DischargePressure.Pascals, 2.0);
        Assert.Equal(pb, plan.DeliveryPressure.Pascals, 1.0);
    }

    [Fact]
    public void ShortRoute_ShouldNeedNoCompression()
    {
        StationPlan plan = Greedy(Straight(50e3, 0.0), Criteria(7.0, 5.0));
        Print(plan);

        double arrival = System.Math.Sqrt(49e12 - K * 50e3);
        Assert.Empty(plan.Stations);
        Assert.Equal(arrival, plan.DeliveryPressure.Pascals, 1.0);
        Assert.Contains(plan.Warnings, w => w.StartsWith("No compression needed"));
    }

    [Fact]
    public void Criteria_DischargeNotAboveMinimumSuction_ShouldThrow()
    {
        var criteria = new StationPlanningCriteria
        {
            InletPressure = Pressure.FromMegapascals(7),
            DeliveryPressure = Pressure.FromMegapascals(5),
            StationDischargePressure = Pressure.FromMegapascals(4.5)
        };

        Assert.Throws<ArgumentException>(() =>
            Greedy(Straight(400e3, 0.0), criteria));
    }

    // ------------------------------------------------------------ realistic route

    private static readonly GasMixture Gas = NaturalGasMixtures.TypicalNaturalGas;

    [Fact]
    public void TerrainRoute_StandardFlow_ShouldBeConsistentWithTheSeriesSolver()
    {
        ICompressibilityModel z = CompressibilityModelFactory.Create(CompressibilityModelType.PengRobinson, Gas);
        MolarMass molarMass = Gas.CalculateMolarMass();
        var model = new IsothermalSegmentModel(molarMass, z, new LeeGonzalezEakinViscosityModel());
        var planner = new CompressorStationPlanner(model, T, molarMass, z, 1.28);

        // 600 km route over hills (elevation every 50 km).
        double[] elevations = { 420, 510, 600, 690, 650, 760, 800, 700, 620, 540, 580, 450, 380 };
        ProfilePoint[] route = elevations
            .Select((e, i) => new ProfilePoint(Length.FromKilometers(50.0 * i), Length.FromMeters(e)))
            .ToArray();

        var standard = new StandardFlowConditions
        {
            Temperature = Temperature.FromDegreesCelsius(15.0),
            Pressure = Pressure.FromKilopascals(101.325),
            CompressibilityFactor = z.Calculate(Pressure.FromKilopascals(101.325), Temperature.FromDegreesCelsius(15.0))
        };
        var q = new StandardVolumetricFlow(VolumeFlow.FromCubicMetersPerSecond(28.0e6 / 86400.0), standard);

        StationPlanningProblem problem = planner.ProblemFor(route, Length.FromMeters(D), Roughness, q, Criteria(7.0, 5.0));
        StationPlan plan = planner.Plan(problem, new MaximumSpacingPlacement());
        _output.WriteLine($"Q_std = 28.0 MMm³/d at 15 °C, 101.325 kPa, Z_std = {standard.CompressibilityFactor:F5}");
        Print(plan);

        Assert.Same(q, plan.StandardFlow);
        Assert.Equal(FlowRateConverter.ConvertStandardVolumeToMass(q, molarMass).KilogramsPerSecond, plan.MassFlow.KilogramsPerSecond, 1e-9);
        Assert.Equal(5.0e6, plan.DeliveryPressure.Pascals, 1.0);
        Assert.NotEmpty(plan.Stations);
        Assert.All(plan.Stations, s => Assert.True(s.SuctionPressure.Pascals >= 5.0e6 - 1.0));
        Assert.All(plan.Stations, s => Assert.True(s.DistanceFromPrevious.Meters > 0.0));

        // Rebuild the planned layout as a series system: same delivery pressure.
        double series = SeriesDelivery(plan, model, molarMass, z);
        _output.WriteLine($"Series solver delivery: {series / 1e6:F6} MPa");
        Assert.Equal(plan.DeliveryPressure.Pascals, series, 5.0);

        // Station count against power (method 2), each plan checked the same way.
        StationCountComparison table = planner.CompareStationCounts(problem, additionalStations: 3);
        _output.WriteLine("");
        _output.WriteLine("  N | P_s (MPa) |   ratio | spacing min–max (km) | total P_s (MW) | delivery (MPa)");

        foreach (StationCountOption o in table.Options)
        {
            if (!o.Feasible)
            {
                _output.WriteLine($"  {o.StationCount} | infeasible: {o.Reason}");
                continue;
            }

            StationPlan p = o.Plan!;
            _output.WriteLine(
                $"  {o.StationCount} | {p.Stations[0].SuctionPressure.Megapascals,9:F4} | {o.PressureRatio,7:F4} | " +
                $"{p.Stations.Min(s => s.DistanceFromPrevious.Kilometers),8:F2} – {p.Stations.Max(s => s.DistanceFromPrevious.Kilometers),8:F2} | " +
                $"{(double)o.TotalIsentropicPower.Megawatts,14:F3} | {p.DeliveryPressure.Megapascals,14:F6}");

            Assert.Equal(5.0e6, p.DeliveryPressure.Pascals, 5.0);
            Assert.Equal(p.DeliveryPressure.Pascals, SeriesDelivery(p, model, molarMass, z), 5.0);
            Assert.All(p.Stations, s => Assert.Equal(o.PressureRatio, s.PressureRatio, 1e-6));
        }

        Assert.Equal(plan.StationCount, table.MinimumStationCount);
        Assert.True(table.Options[0].Feasible);
    }

    private static double SeriesDelivery(StationPlan plan, ISegmentModel model, MolarMass molarMass, ICompressibilityModel z)
    {
        var stage = new CompressorStage(molarMass, z, 1.28, CompressionEfficiencyBasis.Isentropic, 1.0, 1.0);
        var elements = new List<SystemElement>();

        for (int i = 0; i < plan.Sections.Count; i++)
        {
            elements.AddRange(plan.Sections[i].Segments.Select((s, j) => (SystemElement)new PipeElement($"{plan.Sections[i].Name} #{j + 1}", s.Segment, T, model)));

            if (i < plan.Stations.Count)
            {
                PlannedStation st = plan.Stations[i];
                elements.Add(new CompressorElement(st.Name, new CompressorStation(st.Name, stage, 1), st.PressureRatio));
                elements.Add(new CoolerElement($"{st.Name} cooler", T, Pressure.Zero));
            }
        }

        return new SeriesSystem(elements).SolveForward(plan.InletPressure, T, plan.MassFlow).OutletPressure.Pascals;
    }

    // ------------------------------------------------------------ method 2, analytic

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public void EqualRatio_FlatRoute_ShouldSpaceStationsEvenly(int n)
    {
        // Flat, P_A = P_d: last station at L − (P_d² − P_B²)/K; the N sections
        // before it are equal, each of length s = x_last/N, P_s = √(P_d² − K·s).
        double pd = 7.0e6, pb = 5.0e6;
        double greedySpacing = (pd * pd - pb * pb) / K;
        double length = 3.4 * greedySpacing;
        double xLast = length - (pd * pd - pb * pb) / K;
        double s = xLast / n;
        double ps = System.Math.Sqrt(pd * pd - K * s);

        StationPlan plan = EqualRatio(Straight(length, 0.0), Criteria(7.0, 5.0), n);
        Print(plan);
        _output.WriteLine($"analytic: spacing {s / 1e3:F4} km, P_s {ps / 1e6:F6} MPa, ratio {pd / ps:F6}");

        Assert.Equal(n, plan.StationCount);

        for (int i = 0; i < n; i++)
        {
            Assert.Equal((i + 1) * s, plan.Stations[i].Chainage.Meters, 0.05);
            Assert.Equal(ps, plan.Stations[i].SuctionPressure.Pascals, 1.0);
            Assert.Equal(pd / ps, plan.Stations[i].PressureRatio, 1e-6);
        }

        Assert.Equal(pb, plan.DeliveryPressure.Pascals, 1.0);
    }

    [Fact]
    public void EqualRatio_TooFewStations_ShouldBeInfeasible()
    {
        double spacing = (49e12 - 25e12) / K;

        Assert.Throws<StationPlacementInfeasibleException>(() =>
            EqualRatio(Straight(3.4 * spacing, 0.0), Criteria(7.0, 5.0), 2));
    }

    [Fact]
    public void CompareStationCounts_MoreStations_ShouldNeedLessPower()
    {
        double spacing = (49e12 - 25e12) / K;
        CompressorStationPlanner planner = AnalyticPlanner();

        StationCountComparison table = planner.CompareStationCounts(Problem(Straight(3.4 * spacing, 0.0), Criteria(7.0, 5.0)), 4);

        _output.WriteLine($"minimum stations (method 1): {table.MinimumStationCount}, total {(double)table.MaximumSpacing.TotalIsentropicPower.Megawatts:F3} MW");

        foreach (StationCountOption o in table.Options)
        {
            _output.WriteLine($"  N = {o.StationCount}: ratio {o.PressureRatio:F4}, total isentropic power {(double)o.TotalIsentropicPower.Megawatts:F3} MW");
        }

        Assert.Equal(3, table.MinimumStationCount);
        Assert.Equal(new[] { 3, 4, 5, 6, 7 }, table.Options.Select(o => o.StationCount));
        Assert.All(table.Options, o => Assert.True(o.Feasible));

        for (int i = 1; i < table.Options.Count; i++)
        {
            Assert.True(table.Options[i].TotalIsentropicPower < table.Options[i - 1].TotalIsentropicPower);
        }
    }

    // ------------------------------------------------------------ layout evaluation (extension point)

    [Fact]
    public void EvaluateLayout_OfAPlan_ShouldReproduceIt()
    {
        double spacing = (49e12 - 25e12) / K;
        ProfilePoint[] route = Straight(3.4 * spacing, 0.0);
        StationPlan plan = Greedy(route, Criteria(7.0, 5.0));

        StationPlan evaluated = AnalyticPlanner().EvaluateLayout(
            Problem(route, Criteria(7.0, 5.0)),
            plan.Stations.Select(s => new StationSetting(s.Chainage, s.DischargePressure)).ToList());

        Print(evaluated);
        Assert.Equal(plan.DeliveryPressure.Pascals, evaluated.DeliveryPressure.Pascals, 1.0);
        Assert.Equal((double)plan.TotalIsentropicPower.Watts, (double)evaluated.TotalIsentropicPower.Watts, 1.0);
        Assert.Empty(evaluated.Warnings);
    }

    [Fact]
    public void EvaluateLayout_StationTooFarDownstream_ShouldWarn()
    {
        double spacing = (49e12 - 25e12) / K;
        ProfilePoint[] route = Straight(1.5 * spacing, 0.0);

        StationPlan evaluated = AnalyticPlanner().EvaluateLayout(
            Problem(route, Criteria(7.0, 5.0)),
            new[] { new StationSetting(Length.FromMeters(1.2 * spacing), Pressure.FromMegapascals(7.0)) });

        Print(evaluated);

        double suction = System.Math.Sqrt(49e12 - K * 1.2 * spacing);
        Assert.Equal(suction, evaluated.Stations[0].SuctionPressure.Pascals, 1.0);
        Assert.Contains(evaluated.Warnings, w => w.Contains("below the minimum suction"));
    }
}
