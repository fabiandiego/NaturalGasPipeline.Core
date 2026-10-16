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
                $"friction {sec.FrictionPressureDrop.Kilopascals,9:F1} + elevation {sec.ElevationPressureDrop.Kilopascals,8:F1}; " +
                $"V_max {sec.MaximumVelocity.MetersPerSecond,6:F2} m/s at km {sec.MaximumVelocityChainage.Kilometers:F1}, V/V_e {sec.MaximumErosionalRatio:F2}");
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

    // ------------------------------------------------------------ MAOP check (Mechanics, pending review)

    [Fact]
    public void Maop_BelowStationDischarge_ShouldWarnOnEverySectionAboveIt()
    {
        double pd = 7.0e6, pmin = 5.0e6;
        double spacing = (pd * pd - pmin * pmin) / K;
        StationPlanningCriteria criteria = Criteria(7.0, 5.0) with { MaximumAllowableOperatingPressure = Pressure.FromMegapascals(6.9) };

        StationPlan plan = Greedy(Straight(3.4 * spacing, 0.0), criteria);
        Print(plan);

        // A, CS-1 and CS-2 discharge 7.0 MPa; CS-3 only needs
        // √(5² + 0.4·(7² − 5²)) = 5.88 MPa to reach B, below the MAOP.
        string[] maop = plan.Warnings.Where(w => w.Contains("MAOP")).ToArray();
        Assert.Equal(3, maop.Length);
        Assert.Contains(maop, w => w.StartsWith("A → CS-1"));
        Assert.DoesNotContain(maop, w => w.StartsWith("CS-3 → B"));
    }

    [Fact]
    public void Maop_PressureRegainedDownhill_ShouldWarnAtTheLowPoint()
    {
        // 20 km falling 2000 m: hydrostatic gain exceeds friction, so the
        // pressure RISES from 7.0 MPa at A. Analytic (constant Z, f):
        //   P1² − e^s·P2² = K·L·(e^s − 1)/s,  s = 2·g·Δz/a,  Δz = −2000 m.
        const double length = 20e3, drop = -2000.0;
        double s = 2.0 * G * drop / A;
        double p2 = System.Math.Sqrt((49e12 - K * length * (System.Math.Exp(s) - 1.0) / s) / System.Math.Exp(s));

        StationPlanningCriteria criteria = Criteria(7.0, 5.0) with { MaximumAllowableOperatingPressure = Pressure.FromMegapascals(7.5) };
        StationPlan plan = Greedy(Straight(length, drop), criteria);
        Print(plan);
        _output.WriteLine($"analytic outlet {p2 / 1e6:F6} MPa(a)");

        Assert.Empty(plan.Stations);
        Assert.True(p2 > 7.5e6, "Test premise: the outlet must be above the MAOP.");
        Assert.Equal(p2, plan.DeliveryPressure.Pascals, 10.0);
        Assert.Contains(plan.Warnings, w => w.Contains("MAOP") && w.Contains("km 20.000"));
    }

    // ------------------------------------------------------------ velocity check

    [Fact]
    public void Velocity_FlatRoute_ShouldMatchTheAnalyticValueAtEachSectionEnd()
    {
        // Constant Z: ρ = P/a, so V = G·a/P with G = ṁ/A. Highest velocity
        // at the lowest pressure of each section = its outlet.
        double pd = 7.0e6, pmin = 5.0e6;
        double spacing = (pd * pd - pmin * pmin) / K;
        double area = System.Math.PI * D * D / 4.0;
        double g = Flow.KilogramsPerSecond / area;

        StationPlan plan = Greedy(Straight(3.4 * spacing, 0.0), Criteria(7.0, 5.0));
        Print(plan);

        foreach (PipelineSection sec in plan.Sections)
        {
            double pOut = sec.OutletPressure.Pascals;
            double v = g * A / pOut;
            double ve = 0.3048 * 100.0 / System.Math.Sqrt(pOut / A / (0.45359237 / (0.3048 * 0.3048 * 0.3048)));

            Assert.Equal(v, sec.MaximumVelocity.MetersPerSecond, 1e-6);
            Assert.Equal(sec.EndChainage.Meters, sec.MaximumVelocityChainage.Meters, 1e-6);
            Assert.Equal(v / ve, sec.MaximumErosionalRatio, 1e-7);
        }

        // Defaults: no velocity limit given, and V/V_e ≈ 0.49 < 1 → no velocity warnings.
        Assert.DoesNotContain(plan.Warnings, w => w.Contains("velocity"));
    }

    [Fact]
    public void Velocity_AboveTheLimit_ShouldWarnOnEverySectionThatExceedsIt()
    {
        // All four sections end at 5.0 MPa → V = G·a/5 MPa ≈ 9.04 m/s.
        double pd = 7.0e6, pmin = 5.0e6;
        double spacing = (pd * pd - pmin * pmin) / K;
        double vAt5 = Flow.KilogramsPerSecond / (System.Math.PI * D * D / 4.0) * A / 5.0e6;
        _output.WriteLine($"analytic V at 5 MPa: {vAt5:F4} m/s");

        StationPlan below = Greedy(Straight(3.4 * spacing, 0.0), Criteria(7.0, 5.0) with { MaximumGasVelocity = Speed.FromMetersPerSecond(8.0) });
        StationPlan above = Greedy(Straight(3.4 * spacing, 0.0), Criteria(7.0, 5.0) with { MaximumGasVelocity = Speed.FromMetersPerSecond(10.0) });
        Print(below);

        Assert.InRange(vAt5, 8.0, 10.0);
        Assert.Equal(4, below.Warnings.Count(w => w.Contains("exceeds the limit")));
        Assert.DoesNotContain(above.Warnings, w => w.Contains("exceeds the limit"));
    }

    [Fact]
    public void Velocity_ErosionalLimitExceeded_ShouldWarn()
    {
        // C = 40: V_e = 1.2199·40/√(5e6/a) ≈ 7.3 m/s < 9.04 m/s at 5 MPa.
        double pd = 7.0e6, pmin = 5.0e6;
        double spacing = (pd * pd - pmin * pmin) / K;
        StationPlan longRoute = Greedy(Straight(1.5 * spacing, 0.0), Criteria(7.0, 5.0) with { ErosionalVelocityConstant = 40.0 });
        Print(longRoute);

        Assert.Contains(longRoute.Warnings, w => w.Contains("API RP 14E erosional velocity (C = 40)"));
        Assert.True(longRoute.Sections[0].MaximumErosionalRatio > 1.0);
    }

    [Fact]
    public void Criteria_NonPositiveErosionalConstant_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Greedy(Straight(50e3, 0.0), Criteria(7.0, 5.0) with { ErosionalVelocityConstant = 0.0 }));
    }

    [Fact]
    public void Maop_NotGiven_ShouldNotBeChecked()
    {
        StationPlan plan = Greedy(Straight(20e3, -2000.0), Criteria(7.0, 5.0));
        Assert.DoesNotContain(plan.Warnings, w => w.Contains("MAOP"));
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

        _output.WriteLine("");
        Print(table.Options[0].Plan!);
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

    [Fact]
    public void Criteria_Create_ShouldMatchObjectInitializer()
    {
        // The factory exists for C# 9 callers (Unity), which cannot use the initializer.
        StationPlanningCriteria c = StationPlanningCriteria.Create(Pressure.FromMegapascals(7), Pressure.FromMegapascals(5));

        Assert.Equal(Criteria(7.0, 5.0), c);
        Assert.Equal(1.0, c.MaximumStepLength.Kilometers, 12);
    }

    // ------------------------------------------------------------ concept case (front-end mock-up data)

    /// <summary>
    /// The case shown in the front-end concept: the Fluid tab's gas
    /// (Kay pseudo-criticals, Wichert-Aziz, DAK, Lee-Gonzalez-Eakin), the
    /// 600 km terrain route, 28 MMm³/d, 7 → 5 MPa. Prints every number the
    /// concept screens display, and checks them against each other.
    /// </summary>
    /// <summary>
    /// The concept case with the front end's DEFAULT Z model, AGA8-DC92 DETAIL
    /// (same gas, route, flow and limits as <see cref="TerrainRoute_ConceptCase_Report"/>;
    /// Z from AGA8 everywhere: standard-flow conversion, pipe segments and
    /// compression). Reference for a freshly installed program, which opens
    /// with AGA8 selected. Compared with the DAK case printed alongside.
    /// </summary>
    [Fact]
    public void TerrainRoute_ConceptCase_Aga8_Report()
    {
        static GasComponent With(GasComponent c, double y)
        {
            c.MoleFraction = y;
            return c;
        }

        var gas = new GasMixture
        {
            Components =
            {
                With(StandardGasComponents.Methane, 0.900),
                With(StandardGasComponents.Ethane, 0.050),
                With(StandardGasComponents.Propane, 0.020),
                With(StandardGasComponents.IsoButane, 0.003),
                With(StandardGasComponents.NormalButane, 0.005),
                With(StandardGasComponents.Nitrogen, 0.012),
                With(StandardGasComponents.CarbonDioxide, 0.010)
            }
        };
        gas.ValidateComposition();

        MolarMass molarMass = gas.CalculateMolarMass();
        var t = Temperature.FromDegreesCelsius(15.0);
        var pStd = Pressure.FromKilopascals(101.325);
        double[] elevations = { 420, 510, 600, 690, 650, 760, 800, 700, 620, 540, 580, 450, 380 };
        ProfilePoint[] route = elevations
            .Select((e, i) => new ProfilePoint(Length.FromKilometers(50.0 * i), Length.FromMeters(e)))
            .ToArray();

        PseudoCriticalProperties corrected = WichertAzizCorrection.Apply(PseudoCriticalProperties.FromKay(gas), 0.010, 0.0);

        (StationPlan Plan, StationPlan MaxSpacing, double ZStd) Run(ICompressibilityModel z)
        {
            double zStd = z.Calculate(pStd, t);
            var planner = new CompressorStationPlanner(new IsothermalSegmentModel(molarMass, z, new LeeGonzalezEakinViscosityModel()), t, molarMass, z, 1.28);
            var standard = new StandardFlowConditions { Temperature = t, Pressure = pStd, CompressibilityFactor = zStd };
            var q = new StandardVolumetricFlow(VolumeFlow.FromCubicMetersPerSecond(28.0e6 / 86400.0), standard);
            StationPlanningProblem problem = planner.ProblemFor(route, Length.FromMeters(D), Roughness, q, Criteria(7.0, 5.0));
            StationCountComparison table = planner.CompareStationCounts(problem, additionalStations: 3);
            return (table.Options[0].Plan!, table.MaximumSpacing, zStd);
        }

        var aga8 = Run(new Aga8DetailModel(gas));
        var dak = Run(CompressibilityModelFactory.Create(CorrespondingStatesModelType.DranchukAbouKassem, corrected));

        _output.WriteLine("CONCEPT CASE, Z MODEL COMPARISON (method 2, minimum N)");
        foreach ((string name, var r) in new[] { ("AGA8-DC92", aga8), ("DAK (Kay + W-A)", dak) })
        {
            _output.WriteLine(
                $"  {name,-16} Z_std {r.ZStd:F5}  m_dot {r.Plan.MassFlow.KilogramsPerSecond:F2} kg/s  N {r.Plan.StationCount}  " +
                $"ratio {r.Plan.Stations[0].PressureRatio:F4}  P_s {(double)r.Plan.TotalIsentropicPower.Megawatts:F3} MW  " +
                $"(method 1: {(double)r.MaxSpacing.TotalIsentropicPower.Megawatts:F3} MW)  delivery {r.Plan.DeliveryPressure.Megapascals:F4} MPa");
        }

        double dP = (double)(aga8.Plan.TotalIsentropicPower.Watts / dak.Plan.TotalIsentropicPower.Watts) - 1.0;
        _output.WriteLine($"  AGA8 vs DAK: power {dP * 100.0:+0.00;-0.00} %");
        Print(aga8.Plan);

        Assert.Equal(dak.Plan.StationCount, aga8.Plan.StationCount);
        Assert.InRange(System.Math.Abs(dP), 0.0, 0.03);
        Assert.Equal(5.0, aga8.Plan.DeliveryPressure.Megapascals, 1e-4);
    }

    [Fact]
    public void TerrainRoute_ConceptCase_Report()
    {
        static GasComponent With(GasComponent c, double y)
        {
            c.MoleFraction = y;
            return c;
        }

        var gas = new GasMixture
        {
            Components =
            {
                With(StandardGasComponents.Methane, 0.900),
                With(StandardGasComponents.Ethane, 0.050),
                With(StandardGasComponents.Propane, 0.020),
                With(StandardGasComponents.IsoButane, 0.003),
                With(StandardGasComponents.NormalButane, 0.005),
                With(StandardGasComponents.Nitrogen, 0.012),
                With(StandardGasComponents.CarbonDioxide, 0.010)
            }
        };
        gas.ValidateComposition();

        MolarMass molarMass = gas.CalculateMolarMass();
        PseudoCriticalProperties kay = PseudoCriticalProperties.FromKay(gas);
        PseudoCriticalProperties corrected = WichertAzizCorrection.Apply(kay, 0.010, 0.0);
        CorrespondingStatesCompressibilityModel z = CompressibilityModelFactory.Create(CorrespondingStatesModelType.DranchukAbouKassem, corrected);
        var viscosity = new LeeGonzalezEakinViscosityModel();
        var t = Temperature.FromDegreesCelsius(15.0);

        double Rho(double pa) => pa * molarMass.KilogramsPerMole / (z.Calculate(Pressure.FromPascals(pa), t) * R * t.Kelvins);

        var pStd = Pressure.FromKilopascals(101.325);
        double zStd = z.Calculate(pStd, t);

        _output.WriteLine("FLUID");
        _output.WriteLine($"  M {molarMass.GramsPerMole:F3} g/mol, SG (ISO 6976 air) {SpecificGravity.OfMixture(gas, DryAirMolarMass.Iso6976_2016):F4}");
        _output.WriteLine($"  Kay Tpc {kay.Temperature.Kelvins:F2} K, Ppc {kay.Pressure.Megapascals:F4} MPa; Wichert-Aziz Tpc {corrected.Temperature.Kelvins:F2} K, Ppc {corrected.Pressure.Megapascals:F4} MPa");
        _output.WriteLine($"  Z_std {zStd:F5} (DAK in range at standard conditions: {z.IsWithinValidatedRange(pStd, t)}), rho_std {Rho(pStd.Pascals):F4} kg/m3");
        _output.WriteLine($"  at 7 MPa, 15 C: Tpr {corrected.ReducedTemperature(t):F3}, Ppr {corrected.ReducedPressure(Pressure.FromMegapascals(7)):F3}, Z {z.Calculate(Pressure.FromMegapascals(7), t):F4}, rho {Rho(7e6):F2} kg/m3");
        _output.WriteLine("  Z curve (MPa: Z): " + string.Join("  ", new[] { 1.0, 2, 3, 4, 5, 6, 7, 8, 9, 10 }
            .Select(p => $"{p:F0}:{z.Calculate(Pressure.FromMegapascals(p), t):F4}")));

        var model = new IsothermalSegmentModel(molarMass, z, viscosity);
        var planner = new CompressorStationPlanner(model, t, molarMass, z, 1.28);

        double[] elevations = { 420, 510, 600, 690, 650, 760, 800, 700, 620, 540, 580, 450, 380 };
        ProfilePoint[] route = elevations
            .Select((e, i) => new ProfilePoint(Length.FromKilometers(50.0 * i), Length.FromMeters(e)))
            .ToArray();

        var standard = new StandardFlowConditions { Temperature = t, Pressure = pStd, CompressibilityFactor = zStd };
        var q = new StandardVolumetricFlow(VolumeFlow.FromCubicMetersPerSecond(28.0e6 / 86400.0), standard);
        StationPlanningProblem problem = planner.ProblemFor(route, Length.FromMeters(D), Roughness, q, Criteria(7.0, 5.0));

        StationCountComparison table = planner.CompareStationCounts(problem, additionalStations: 3);
        _output.WriteLine("");
        _output.WriteLine("CASE");
        Print(table.MaximumSpacing);

        foreach (StationCountOption o in table.Options.Where(o => o.Feasible))
        {
            StationPlan op = o.Plan!;
            _output.WriteLine($"  OPTION N {o.StationCount}: ratio {o.PressureRatio:F4}, spacing {op.Stations.Min(s => s.DistanceFromPrevious.Kilometers):F1}-{op.Stations.Max(s => s.DistanceFromPrevious.Kilometers):F1} km, total {(double)o.TotalIsentropicPower.Megawatts:F3} MW");
        }

        StationPlan chosen = table.Options[0].Plan!;
        Print(chosen);

        // Pressure profile: section ends and every 10 km.
        var profile = new List<string>();

        foreach (PipelineSection sec in chosen.Sections)
        {
            double x = sec.StartChainage.Meters;
            profile.Add($"{x / 1e3:F1}:{sec.InletPressure.Megapascals:F3}");

            for (int k = 0; k < sec.Segments.Count; k++)
            {
                SegmentResult r = sec.Segments[k];
                x += r.Segment.Length.Meters;

                if (System.Math.Abs(x / 10e3 - System.Math.Round(x / 10e3)) < 1e-6 || k == sec.Segments.Count - 1)
                {
                    profile.Add($"{x / 1e3:F1}:{r.OutletPressure.Megapascals:F3}");
                }
            }
        }

        _output.WriteLine("  PROFILE " + string.Join(" ", profile));

        // Fluid state in the middle of section CS-2 → CS-3.
        PipelineSection mid = chosen.Sections[2];
        int half = mid.Segments.Count / 2;
        double xm = mid.StartChainage.Meters + mid.Segments.Take(half).Sum(s => s.Segment.Length.Meters);
        double pm = mid.Segments[half].InletPressure.Pascals;
        double area = System.Math.PI * D * D / 4.0;
        _output.WriteLine($"  STATE {mid.Name} km {xm / 1e3:F1}: P {pm / 1e6:F3} MPa, rho {Rho(pm):F2} kg/m3, " +
                          $"v {chosen.MassFlow.KilogramsPerSecond / (Rho(pm) * area):F2} m/s, Z {z.Calculate(Pressure.FromPascals(pm), t):F4}");
        _output.WriteLine($"  ACTUAL FLOW at A: {chosen.MassFlow.KilogramsPerSecond / Rho(7e6):F3} m3/s, mass {chosen.MassFlow.KilogramsPerSecond:F2} kg/s");

        // Hardware preview: a real stage at station 3.
        PlannedStation cs3 = chosen.Stations[2];
        var real = new CompressorStage(molarMass, z, 1.28, CompressionEfficiencyBasis.Isentropic, 0.80, 0.98);
        CompressionResult c = real.Compress(cs3.SuctionPressure, t, cs3.DischargePressure, chosen.MassFlow);
        _output.WriteLine($"  HARDWARE {cs3.Name}: {cs3.SuctionPressure.Megapascals:F4} -> {cs3.DischargePressure.Megapascals:F4} MPa, T2 {c.DischargeTemperature.DegreesCelsius:F1} C, " +
                          $"head {c.Head.KilojoulesPerKilogram:F2} kJ/kg, gas {(double)c.GasPower.Megawatts:F3} MW, shaft {(double)c.ShaftPower.Megawatts:F3} MW, " +
                          $"Z1 {c.SuctionCompressibilityFactor:F4}, Z2 {c.DischargeCompressibilityFactor:F4}");

        Assert.Equal(5.0e6, chosen.DeliveryPressure.Pascals, 5.0);
        Assert.True((double)c.ShaftPower.Watts > (double)cs3.IsentropicPower.Watts);
        Assert.Equal(1.0, gas.TotalMoleFraction(), 12);
    }
}
