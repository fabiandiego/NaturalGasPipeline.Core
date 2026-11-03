# NaturalGasPipeline

Calculation engine (back end) for natural-gas transmission pipeline engineering.
C# (netstandard2.1 + .NET 8), explicit physical units with [UnitsNet](https://github.com/angularsen/UnitsNet).
Built to grow: every family of models has an interface, and implementations can be added without touching the calculations that use them.
The front end (Unity) lives in a separate repository and loads the engine as DLLs.

The engine answers, in this order:
1. What gas do I have? (composition → properties)
2. What pressure loss will I have from A to B, from friction, elevation and distance? (pipe segments)
3. How many compressor stations do I need, and how far apart? (station planning)
4. What will each station be like, and how much energy will it use? (stations; real consumption awaits the machinery library)

Project status, decisions and references: [`docs/STATUS.md`](docs/STATUS.md).

## Structure

```
NaturalGasPipeline.sln
Directory.Build.props        shared settings (C# 12, nullable, XML documentation)
Directory.Packages.props     NuGet versions, defined once
src/Directory.Build.props    engine libraries: netstandard2.1 (Unity) + net8.0
tests/Directory.Build.props  tests: net8.0
src/
  NaturalGasPipeline.Core            no domain dependencies
    Common/      PhysicalConstants (R, g), Guard (netstandard2.1 helpers)
    Flow/        mass, actual volumetric and standard volumetric flow; conversions
    Gas/         GasProperties (immutable snapshot of a state)
    Numerics/    CubicEquationSolver, BrentRootSolver, DenseLinearSolver
    Pipeline/    Pipe (OD, wall), PipeSegment (ID, L, ε, Δz)
  NaturalGasPipeline.Thermodynamics  → Core
    Components/      GasComponent, StandardGasComponents (C1–nC4, N₂, CO₂, H₂S)
    Mixtures/        GasMixture, NaturalGasMixtures, binary interaction parameters
    Characterization/ γ ↔ M (DryAirMolarMass: ISO 6976:2016, GPA 2145-09, GTHPS 28.96, 29),
                      pseudo-critical properties: Kay, Sutton, Standing (natural gas / condensate;
                      Whitson & Brulé or Ahmed coefficients), Wichert-Aziz and
                      Carr-Kobayashi-Burrows corrections (CO₂, H₂S, N₂)
    State/           GasState
    Compressibility/ ICompressibilityModel: ideal, constant Z, Peng-Robinson, SRK,
                     corresponding states (DAK, Hall-Yarborough, Papay), CNGA,
                     AGA8-DC92 DETAIL (ISO 12213-2; port of the NIST reference code)
    Viscosity/       IViscosityModel: Lee-Gonzalez-Eakin, constant
  NaturalGasPipeline.Hydraulics      → Core, Thermodynamics
    Friction/    IFrictionFactorModel: Colebrook-White, Haaland, Swamee-Jain, Churchill,
                 AGA fully turbulent, AGA with drag factor Df,
                 constant f, constant transmission factor F
    Reynolds, Darcy-Weisbach, IsothermalGasFlowSolver
  NaturalGasPipeline.Correlations    → Core, Thermodynamics, Hydraulics
    IGasFlowEquation: General Flow / AGA (any friction model),
                      Weymouth, Panhandle A, Panhandle B (explicit efficiency E)
    GasFlowEquationSolver: solves Q, P2, P1 or internal diameter
  NaturalGasPipeline.Compression     → Core, Thermodynamics
    CompressorStage: isentropic or polytropic power, discharge T, mean Z
    CompressorStation: N stages with equal ratios and optional intercooling
  NaturalGasPipeline.Network         → all of the above
    ISegmentModel: SI isothermal solver or any flow equation, in mass flow;
                   results split ΔP into friction and elevation parts
    PipelineProfile: segments from a route profile (chainage, elevation) and subdivision
    SeriesSystem: pipes, compressors and coolers in series (forward,
                  backward, or solving the mass flow)
    GasNetwork + NetworkSolver: networks with fixed-pressure or fixed-flow nodes,
                  loops and flow reversal (nodal method, Newton)
    CompressorStationPlanner: station placement on a terrain profile
                  (method 1 maximum spacing, method 2 equal ratios,
                  station count vs power, layout evaluation)
  NaturalGasPipeline.Mechanics       → Core   PENDING REVIEW, see docs/MECHANICS_REVIEW.md
    SteelPipeDesign: design pressure P = (2·S·t/D)·F·E·T (gauge), required wall,
                  hoop stress, %SMYS, gauge ↔ absolute
    DesignFactors / ApiLinePipeGrades: F by location class, E by seam,
                  T by temperature, API 5L SMYS
tests/
  NaturalGasPipeline.Core.Tests
  NaturalGasPipeline.Thermodynamics.Tests
  NaturalGasPipeline.Hydraulics.Tests
  NaturalGasPipeline.Correlations.Tests
  NaturalGasPipeline.Compression.Tests
  NaturalGasPipeline.Network.Tests
  NaturalGasPipeline.Mechanics.Tests
docs/        project status, decisions, references
scripts/     build-unity-plugins (Windows / Linux)
```

Dependencies go one way only: `Core ← Thermodynamics ← Hydraulics ← Correlations ← Network` (and `Thermodynamics ← Compression ← Network`, `Mechanics ← Network`).
New modules follow the same rule.

## Adding a model

1. Implement the interface of its family (`ICompressibilityModel`, `IViscosityModel`, `IFrictionFactorModel`, `IGasFlowEquation`, `ISegmentModel`, `IStationPlacementMethod`, …) in the matching folder.
2. Document in the XML comments the source, the correlation's own units and its validity range.
3. Convert to the correlation's units only inside the model; outside, always UnitsNet types.
4. Add tests at two levels:
   - **Implementation**: the code reproduces the published equation, compared with an independent reference (another implementation or a hand calculation).
   - **Accuracy**: the model compared with real or published data (NIST, textbook examples), with the measured error band documented.
5. Physical constants live in `PhysicalConstants`, never declared locally.
6. No silent assumptions: anything assumed is stated in the XML comments, and anything outside a validity range raises a warning.

## Using the engine from Unity

The libraries in `src/` build for `netstandard2.1` (what Unity 6 can load: Mono, C# 9) and for `net8.0` (tests and .NET applications).
To produce the DLLs Unity needs (engine + UnitsNet):

```
scripts/build-unity-plugins.ps1      (Windows)
scripts/build-unity-plugins.sh       (Linux)
```

The output goes to `artifacts/UnityPlugins`; copy it to `Assets/_Project/Plugins/Engine` in the Unity project.
CI also publishes the same DLLs to the orphan branch `unity-plugins` on every push to `master`; its `SOURCE.txt` names the branch and commit they were built from.
netstandard2.1 lacks some .NET 8 APIs: use `Guard.NotNull` and `Guard.IsDefined` (Core.Common) instead of `ArgumentNullException.ThrowIfNull` and `Enum.IsDefined<T>`; the compiler attributes behind `init` and `required` come from PolySharp.

## Build and test

Open `NaturalGasPipeline.sln` in Visual Studio 2022 and run all tests from Test Explorer
(or `dotnet test` in the repository root). Tests write their intermediate data with `ITestOutputHelper`.
GitHub Actions (`.github/workflows/ci.yml`) builds both target frameworks and runs every test on each push.

## License

MIT. See [LICENSE](LICENSE).
