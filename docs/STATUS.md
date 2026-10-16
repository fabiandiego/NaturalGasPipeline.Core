# NaturalGasPipeline — Project status

**Version 2.2 — 2026-10-16** (Mechanics module, pending review; see [MECHANICS_REVIEW.md](MECHANICS_REVIEW.md)). Version 2.1 — 2026-10-15. Version 2.0 (2026-10-05) superseded the Spanish "Diagnóstico del motor" (v1, 2026-10-04).
Repository: `fabiandiego/NaturalGasPipeline.Core`, branch `master`. All earlier work branches were merged (PR #1, 2026-10-07) and deleted.
Build and tests: GitHub Actions on every push (netstandard2.1 + net8.0, all tests), and Visual Studio 2022 on the owner's machine (344 tests passed on 2026-10-05, before the station-planning work).
Unity DLLs: the CI job `unity-plugins` publishes the netstandard2.1 build of `master` (only `master`, since 2026-10-16) to the orphan branch `unity-plugins`; the Unity project copies them to `Assets/_Project/Plugins/Engine/`, where `SOURCE.txt` records the engine commit.

---

## 1. Purpose and scope

A modular calculation engine (C#) for natural-gas transmission pipelines, used by a Unity desktop application (separate repository) that runs 100 % offline on Windows and Linux.

The software answers, in this order:

| # | Question | Where | Status |
|---|---|---|---|
| 1 | What gas do I have? | Fluid Definition | Done: composition, Z correlations and EOS, viscosity, standard conditions |
| 2 | From A to B, what pressure loss from friction, elevation and distance? | Pipe segment | Done: isothermal solver, flow equations, terrain profiles, ΔP split into friction and elevation |
| 3 | How many compressor stations, and how far apart? | Station planning | Methods 1 and 2 and the N-vs-power table done; methods 3–5 prepared (§6) |
| 4 | What will each station be like, and how much energy will it use? | Station (node) | Isentropic (hardware-independent) power done; real power and fuel/energy await the machinery library |

Later: pressure reduction (decompression) stations and storage.

### Project criteria
- Explicit physical units (UnitsNet 5.75.1); no silent conversions or assumptions.
- Mass flow, actual volumetric flow and standard volumetric flow are distinct types; standard flow carries its reference conditions.
- Every important calculation has unit tests, at two levels: implementation (independent reference) and accuracy (published or measured data). Tests report intermediate values with `ITestOutputHelper`.
- Independent components first, then integration.
- Extensible catalogues: every model family is an interface.
- All code, comments and documentation in English (decision 2026-10-05).

## 2. Parameter ownership (decision 2026-10-05)

| Owner | Inputs | Results |
|---|---|---|
| **Fluid** (global) | Composition, Z model, pseudo-critical method, sour-gas correction, k = cp/cv, standard conditions | M, γ, Tpc, Ppc, Z(P,T), ρ |
| **Pipe segment** (removes pressure) | Internal diameter, wall, roughness; length and elevations *derived from the route on the DEM*; flowing temperature; friction model | ΔP = friction + elevation, pressure profile, velocity, density, Z, warnings |
| **Node** (changes the gas state at a point) | Supply A: inlet pressure. Delivery B: target pressure. Station: suction / discharge limits, later the machine from the library | Ratio, discharge T, head, power, later energy use |

- A node's elevation is taken from the DEM at its position, never typed, so there is one source of truth for elevation.
- Case inputs: pressure at A, target pressure at B, design flow as **standard volumetric flow** (customisable later). Conversion to mass flow is explicit: ṁ = Q_std·P_std·M / (Z_std·R·T_std).

## 3. Engine structure

```
Core ← Thermodynamics ← Hydraulics ← Correlations ← Network
              ↖ Compression ←────────────────────────┘
```

| Project | Content |
|---|---|
| Core | PhysicalConstants, Guard, flow types and conversions, Brent, cubic and dense linear solvers, Pipe / PipeSegment |
| Thermodynamics | Components, mixtures, characterisation (Kay, Sutton, Standing; Wichert-Aziz, Carr-Kobayashi-Burrows), Z (ideal, constant, PR, SRK, DAK, Hall-Yarborough, Papay, CNGA, AGA8-DC92 DETAIL), viscosity (Lee-Gonzalez-Eakin, constant) |
| Hydraulics | Friction (Colebrook-White, Haaland, Swamee-Jain, Churchill, AGA fully turbulent, AGA with drag factor, constant f / F), isothermal compressible segment solver |
| Correlations | Weymouth, Panhandle A/B, General Flow / AGA; solves Q, P1, P2 or D |
| Compression | Isentropic / polytropic stage, multi-stage station with intercooling |
| Network | Segment models, route profiles, series systems, nodal network solver, **compressor station planner** |
| Mechanics | **PENDING REVIEW.** Steel-pipe design pressure P = (2·S·t/D)·F·E·T (49 CFR §192.105 / ASME B31.8 §841.1.1), required wall, hoop stress and %SMYS, F/E/T tables, API 5L grades; MAOP check in the station planner. Open items M-1…M-3 in [MECHANICS_REVIEW.md](MECHANICS_REVIEW.md) |

Targets: netstandard2.1 (Unity 6, Mono, C# 9) and net8.0. PolySharp supplies compiler attributes on netstandard2.1. `scripts/build-unity-plugins.*` produces the DLLs for Unity.

## 4. Validation summary

### Compressibility and fluid properties
- **AGA8-DC92 DETAIL (ISO 12213-2)** added 2026-10-06 (`Aga8DetailModel`, port of the NIST reference code usnistgov/AGA8):
  reproduces the NIST check point to 1e-8 and the compiled NIST code to 1e-10 (60 states, 4 gases); pure methane and
  nitrogen within 0.02 % of the NIST WebBook reference equations. It is the reference for natural gas and the
  front-end default.
- **Reliability of every Z option for natural gas** (`NaturalGasZBenchmarkTests`, 2026-10-06): max |ΔZ/Z| against
  AGA8 for 8 gases (Snam entry points Russia, North Europe, Algeria, Libya; LNG Qatar, Nigeria; concept gas;
  10 % N2 + 8 % CO2), 273–313 K, 1–10 MPa:

  | Option | max \|ΔZ/Z\| | mean ΔZ/Z | at standard conditions |
  |---|---|---|---|
  | DAK · Kay + Wichert-Aziz | 1.30 % | −0.38 % | 0.012 % |
  | HY · Kay + Wichert-Aziz | 1.34 % | −0.47 % | 0.021 % |
  | SRK (kij = 0) | 2.84 % | +0.49 % | 0.005 % |
  | DAK · Kay, no correction | 3.59 % | −0.60 % | 0.023 % |
  | Papay · Kay + Wichert-Aziz | 3.78 % | −0.33 % | 0.062 % |
  | Peng-Robinson (kij = 0) | 4.16 % | −1.57 % | 0.061 % |
  | DAK · Kay + Carr-Kobayashi-Burrows | 6.65 % | +0.24 % | 0.040 % |
  | DAK · Sutton + Wichert-Aziz | 9.08 % | −0.13 % | 0.060 % |
  | DAK · Standing + Wichert-Aziz | 13.92 % | −1.19 % | 0.088 % |

  The gravity-only pseudo-critical methods (Sutton, Standing) and CKB fail on the high-inert gas; with the
  composition known, use AGA8, or DAK/HY with Kay + Wichert-Aziz. Z at standard conditions is within 0.1 % for every
  option, so the standard-to-mass flow conversion is not the weak point. The bands are asserted by the test.
- **EOS against NIST WebBook** (methane, nitrogen; 273–313 K; 1–10 MPa): Peng-Robinson underestimates methane Z by up to 2.8 %; SRK within 2.2 %. Neither reaches custody-transfer accuracy (AGA 8 / GERG-2008 ≈ 0.1 %).
- **Corresponding states against NIST** (pure fluids): methane DAK 1.49 %, HY 1.52 %, Papay 1.40 %; nitrogen DAK 2.42 %, HY 2.03 %, Papay 2.44 % (all underestimate Z).
- **Academic sources**: ITU PET 509E DAK checkpoints reproduced to 4 decimals; King Saud PGE 362 and Mosul examples (Standing, Wichert-Aziz, Carr-Kobayashi-Burrows, Kay) reproduced; against Standing–Katz chart readings DAK ≤ 1.35 %, HY ≤ 1.84 %, Papay ≤ 3.34 % inside its range.
- **Papay** is valid where it deviates ≤ 5 % from DAK (user decision); boundary tabulated every 0.05 in Tpr.
- **Viscosity**: Lee-Gonzalez-Eakin vs NIST methane +1.2 % to +4.1 %; not valid for pure nitrogen (≈ −45 %). μ only enters through Re, so its effect on ΔP is well below 1 %.
- Source discrepancies resolved explicitly: Standing dry-gas intercept 667 (Whitson & Brulé) vs 677 (Ahmed) is a user choice; dry-air molar mass (ISO 6976 28.96546, GPA 2145 28.9625, GTHPS 28.96, 29) is always explicit.

### Pipe flow
- Isothermal segment: P1² − e^s·P2² = f·G²·a·L/D·(e^s − 1)/s (a = Z·R·T/M, s = 2·g·Δz/a), Z, μ, f at P_avg. Against numerical integration with local Z(p): 0.02 % of ΔP (flat 50 km), 0.35 % (+500 m), 0.17 % (−300 m); subdividing converges as 1/N².
- Arnold & Stewart example 6.6a reproduced within 0.2 %.
- **Friction / elevation split** (new): ΔP_elevation = ρ_lm·g·Δz with ρ_lm = ρ̄·P_lm/P_avg and P_lm = (P1 − P2)/ln(P1/P2); ΔP_friction = ΔP_total − ΔP_elevation. Exact for a static gas column at constant Z (tested to 1e-6). It is a reporting definition; the momentum balance is integrated as a whole.
- Flow equations: CED P06-001 examples 11 and 12 reproduced to 0.01 psi; Arnold & Stewart 6.2, 6.5, 6.6a(C) and the AGA Ft table reproduced. Book errata documented (example 6.6a Panhandle B constant; example 6.7 diameter).

### Compression
- CED example 14: 2690.5 hp vs 2692 hp published (−0.06 %, rounding of the book's constant).

### Networks
- Nodal method with Newton; validated against an independent Python (fsolve) implementation (pressures to 1e-7, flows to 1e-5 kg/s), analytic parallel split and tree-vs-direct solves. No published looped-network example with complete data has been found.

### Mechanics (pending review)
- Verified only against the public 49 CFR 192 text (formula and F, E, T tables) and hand calculations: NPS 20 × 0.500 in, 52 000 psi, F 0.72 → 1872 psig exactly; L360 → 12.96 MPa(g) (+0.41 % vs the psi-based SMYS). No B31.8 or Menon worked example reproduced yet (M-1).
- Planner MAOP check validated analytically: a 20 km, −2000 m descent raises the pressure from 7.0 to the closed-form outlet value, and the warning is raised at the low point, not at the inlet.

### Velocity check (2026-10-16)
- `GasVelocityCalculator` (Hydraulics): V = ṁ/(ρ·A) and API RP 14E erosional velocity V_e = C/√ρ (Arnold & Stewart 2016, Eq. 6.15), converted exactly to SI (1.2199·C/√ρ); 30.48 m/s at 1 lbm/ft³, C = 100.
- Planner: every section reports its maximum velocity, where it occurs and the maximum V/V_e; optional `MaximumGasVelocity` limit and `ErosionalVelocityConstant` (default 100) raise warnings. Validated against V = G·a/P with constant Z.

### Realistic example cases of the front end (2026-10-16)
- `RealisticCasesTests` runs the five cases shipped with the Unity front end (`Assets/StreamingAssets/Cases`) with the default models, exactly as `EngineBridge` does. Published station counts reproduced: TAP onshore 10 bcm/y → 0 intermediate stations, 20 bcm/y → 2 (Serres, Bilisht); Medgaz → 0, arrival 13.7 MPa ≥ 82 barg; Ruby (minimum suction 800 psig, assumed) → 3 (Wildcat Hills, Wieland Flat, Desert Valley), placed in the same counties. Terrain profiles approximate; wall thickness and minimum suction are assumptions.
- Repeated segment warnings are now summarised per section (one line plus a count) instead of one line per 1 km step.

### Textbook cases through the planner (2026-10-16)
- `LiteratureCasesTests` runs five textbook examples through the station planner as `EngineBridge` would with the Constant Z option (composition → SG with ISO 6976 air, constant Z with Z_std = 1, flat two-point route, no station needed). Outlet pressure vs published:
  A&S Ex. 6.6a (A) isothermal Darcy f 0.015, 6 in: 884.29 vs 883 psia (+0.15 %, book rounding); A&S Ex. 6.6a (C) Panhandle B, 4 in: 771.08 vs 771 psia;
  CED Ex. 11 Weymouth 946.241 vs 946.24 psia; Panhandle A 970.810 vs 970.81 psia; CED Ex. 12 General Flow F = 20, forward: 1014.703 vs 1014.7 psia.

## 5. Compressor station planning (question 3)

All in `NaturalGasPipeline.Network`:

| Type | Role |
|---|---|
| `StationPlanningProblem` | Route profile, pipe, design mass flow (and the standard flow it came from), criteria |
| `StationPlanningCriteria` | Inlet pressure at A, delivery target at B, station discharge limit (default: inlet pressure), minimum suction (default: delivery target), marching step |
| `CompressorStationPlanner` | Physics (segment model, flowing temperature, gas); `Plan`, `CompareStationCounts`, `EvaluateLayout`, `ProblemFor` (standard → mass flow) |
| `IStationPlacementMethod` | Pluggable placement methods |
| `MaximumSpacingPlacement` | **Method 1** |
| `EqualRatioPlacement` | **Method 2** |
| `StationPlan`, `PlannedStation`, `PipelineSection` | Results: stations (position, elevation, spacing, suction, discharge, ratio, isentropic head, power, discharge T), sections (ΔP split), warnings |
| `StationCountComparison` | N vs power table |

**Method 1 — greedy maximum spacing.** March from A; place a station where the pressure reaches the minimum suction; restore the discharge pressure; the last station discharges only what B needs (backward march); if the gas would arrive below the target, add a station where the backward profile from B reaches the discharge pressure. Gives the minimum number of stations N_min. Can leave a small last station.

**Method 2 — equal pressure ratio for N stations.** The last station sits where the backward profile from B reaches the discharge pressure (independent of the suction pressure). The common suction pressure P_s is the root (Brent) of: position of the N-th point where the forward march falls to P_s, minus that position. All stations then share the ratio P_d/P_s. Infeasible when N < N_min.

**N vs power.** `CompareStationCounts` runs method 1, then method 2 for N = N_min … N_min + k, and reports ratio, spacing range and total isentropic power.

**Assumptions:** isothermal pipe flow at the flowing temperature; gas cooled to that temperature after each station (aftercooler duty not computed); power is isentropic (η = 1), the minimum any machine needs.

**Validation:** closed-form solutions with constant Z and f (flat and uniform-slope spacing to ±0.05 m; method-2 spacing and suction pressure exact); delivery to ±1 Pa; every plan rebuilt as a `SeriesSystem` gives the same delivery pressure; method-1 layouts round-trip through `EvaluateLayout`.

**Example (test output):** 600 km hilly route, ID 882 mm, 28.0 MMm³/d (15 °C, 101.325 kPa), 7 → 5 MPa, Peng-Robinson gas, k = 1.28:

| Plan | N | Ratio(s) | Spacing (km) | Total isentropic power |
|---|---|---|---|---|
| Method 1 | 5 | 1.40 ×4, 1.22 | 104–115 | 45.3 MW |
| Method 2 | 5 | 1.333 | 93–102 | 41.8 MW |
| Method 2 | 6 | 1.253 | 77–87 | 38.7 MW |
| Method 2 | 7 | 1.204 | 66–74 | 36.9 MW |
| Method 2 | 8 | 1.171 | 58–65 | 35.5 MW |

With the same five stations, equal ratios need 7.7 % less power than method 1. More stations lower the power further, at the cost of more stations to build: that trade-off is method 4.

## 6. Roadmap for placement methods 3–5

The extension points already exist: `IStationPlacementMethod`, `CompressorStationPlanner.EvaluateLayout` (hydraulics and criteria warnings for any layout), `StationPlacementInfeasibleException`, and `PlanningRun` (internal marching primitives).

| Method | Decides | Formulation | Needs |
|---|---|---|---|
| 3. Minimum total power for given N | Positions x₁…x_N and discharge pressures (≤ limit) | Small NLP: minimise Σ P_i subject to delivery = target, suction ≥ minimum, discharge ≤ limit; each evaluation is `EvaluateLayout`. Start from method 2. Derivative-free (Nelder–Mead / pattern search) or SQP with finite differences | Real compressor efficiencies from the machinery library (with η = 1 it reduces to near-equal ratios) |
| 4. Minimum total cost | N, positions, pressures, possibly diameter | Enumerate N (or branch and bound) with method 3 inside; objective = annualised station capital + pipe capital + energy cost (Edgar, Himmelblau & Bickel 1978) | Cost model (station capital vs power, pipe cost per diameter and length, energy price, discount rate) |
| 5. Dynamic programming over candidate sites | Positions restricted to buildable sites | Stages = candidate sites along the route (from the 3D map: access, land use, exclusion zones); state = discharge pressure (discretised); cost = power or cost of the section | Candidate sites from the front end; method 3/4 objective |

Planned additions when these start: `CandidateSites` on the problem (method 5), an `IStationCostModel` (method 4), and the machinery library (efficiency, driver type, fuel) for methods 3–4 and question 4.

## 7. Front end (Unity, separate repository)

- Repository `fabiandiego/NaturalGasPipeline.Unity`, branch `main`: Unity 6000.3.10f1, UI Toolkit, C# 9; user manual in `docs/manual/` (PDF built by CI to branch `manual-pdf`).
- Implemented tabs, in workflow order: Fluid Definition → Pipeline Architecture Drafter → Hardware Drafter → Network Map 3D. The Fluid tab offers AGA8-DC92 DETAIL (default for pipeline gas) and a library of 17 published gas compositions.
- Concept screens (artifact): tabs in workflow order — Fluid Definition → Pipeline Architecture Drafter → Hardware Drafter → Network Map 3D.
- Default language English; Russian, Spanish, Arabic and Persian postponed (Arabic and Persian need right-to-left layout and glyph shaping).
- Map: schematic horizontal scale over real terrain (DEM); lengths and elevations used by the engine come from the DEM.
- v1 scope: the map and an intermediate section between two standard compressor stations.
- Unity 6 loads netstandard2.1 DLLs only; CoreCLR is announced for Unity 7.
- Concept updated 2026-10-05: the Architecture tab shows separate **Nodes** and **Segments** tables, the station-planning inputs and results, and the N-vs-power table.
- All concept tabs show one engine case, printed by the test `StationPlannerTests.TerrainRoute_ConceptCase_Report`: the Fluid-tab gas (Kay + Wichert-Aziz + DAK), 600 km route, 28.0 MMm³/d (248.23 kg/s), 7 → 5 MPa; method 2 with 5 stations, ratio 1.355, 45.0 MW isentropic (method 1: 47.5 MW). Z at standard conditions is DAK extrapolated below Ppr 0.2 and is flagged as a warning until an ISO 6976 method exists.

## 8. Open items and known issues

- Mechanics review items M-1…M-3 (B31.8 tables and worked example, Class 1 Division 1, SMYS basis). MAOP from test pressure and component ratings is not modelled.
- Machinery library (compressors, drivers, efficiencies, fuel) — question 4.
- Heating value (ISO 6976) is needed if drivers are gas turbines (previously parked as "PCS").
- Non-isothermal pipe flow; compressors and regulators inside networks; GasLib validation.
- Removed 2026-10-05 (owner's decision): `Unity Gas Pipeline Simulator_tester/`, a legacy stub superseded by the Correlations project.
- CI uses actions targeting Node.js 20 (deprecated; works, update when convenient).
- CI runs tests on pushes to every branch; since 2026-10-16 only `master` publishes `unity-plugins`. Still check `SOURCE.txt` before copying DLLs into Unity.
- `BinaryInteractionDatabase` returns 0 for a misspelt component name; no default kij.

## 9. Sources that could not be accessed (to be supplied manually)

| # | Source | Why it matters | What happened |
|---|---|---|---|
| 1 | "A dynamic programming based Gas Pipeline Optimizer", Springer LNCIS chapter, doi 10.1007/BFb0120035 | Method 5 (DP) | HTTP 429 (rate limited) |
| 2 | "The Optimal Design of Natural Gas Transmission Pipelines", *Energy Sources* Part B, doi 10.1080/15567240802534193 | Methods 3–4 | HTTP 403 |
| 3 | Ríos-Mercado & Borraz-Sánchez (2015), *Applied Energy* 147:536–555, full text (OSTI 1235737 / ScienceDirect) | Review of design and operation problems | OSTI robots error; ScienceDirect paywall; only the abstract was read |
| 4 | "Optimal structure of gas transmission trunklines" (ResearchGate 29614456) | Station number and spacing on trunklines | HTTP 429 |
| 5 | Wong & Larson (1968), DP for natural-gas pipeline systems, *IEEE Trans. Automatic Control* (citation unverified) | Classic DP reference for method 5 | Not found online |
| 6 | Carter (1998), "Pipeline optimization: dynamic programming after 30 years", PSIG (citation unverified) | DP practice for method 5 | Not found online |
| 7 | Menon, *Gas Pipeline Hydraulics* (2005), section on compressor station locations | Textbook form of method 1 | Not verified; the earlier chapter citation was removed |
| 8 | Edgar, Himmelblau & Lasdon, *Optimization of Chemical Processes* (2001), gas transmission example | Method 4 formulation and data | Only the GAMS model summary was read |
| 9 | CED course P06-001 (Menon), example 12 two-segment method | Published 1183.75 psig not reproduced | Method not in the retrieved text; flowing temperature illegible (deduced as 70 °F) |
| 10 | ASME B31.8 (current edition) | MAOP / design factor | Paywalled standard |
| 11 | ISO 6976:2016 and AGA Report No. 8 (2017) | Heating value; reference Z | Paywalled standards |

## 10. Key literature by critical point

Verified bibliographic details unless marked *(unverified)*.

**Gas properties**
- Kunz, O., Wagner, W. (2012). The GERG-2008 wide-range equation of state for natural gases and other mixtures. *J. Chem. Eng. Data* 57 (page range unverified). — Reference EOS for natural gas; benchmark for every correlation in the engine.
- AGA Report No. 8 (2017), Parts 1–2. — Industry reference for Z (paywalled).
- Dranchuk & Abou-Kassem (1975); Hall & Yarborough (1973); Sutton (1985); Wichert & Aziz (1972); Lee, Gonzalez & Eakin (1966). — Correlations implemented; references in the code comments.

**Pipe flow**
- Coelho, P. M., Pinho, C. (2007). Considerations about equations for steady state flow in natural gas pipelines. *J. Braz. Soc. Mech. Sci. & Eng.* 29(3). — Comparison of flow equations and their assumptions.
- Osiadacz, A. J., Chaczykowski, M. (2001). Comparison of isothermal and non-isothermal pipeline gas flow models. *Chemical Engineering Journal* 81. — When the isothermal assumption breaks down.
- Colebrook (1939); Churchill (1977); AGA NB-13 method as presented by Menon. — Friction models implemented.

**Compression**
- Schultz, J. M. (1962). The polytropic analysis of centrifugal compressors. *J. Eng. Power* 84(1), 69–82 (erratum 84(2), 222). — Real-gas polytropic head; basis for the machinery library.
- Kurz, R. (2012). Gas compressor station economic optimization. *International Journal of Rotating Machinery*, doi 10.1155/2012/715017. — Station design and cost trade-offs (methods 3–4, question 4).

**Station placement and pipeline design optimisation**
- Edgar, T. F., Himmelblau, D. M., Bickel, T. C. (1978). Optimal design of gas transmission networks. *SPE Journal* 18(2). — Number and location of stations, diameters and pressures (GRG + branch and bound); optimal ratios are not equal once capital cost counts.
- Ríos-Mercado, R. Z., Borraz-Sánchez, C. (2015). Optimization problems in natural gas transportation systems: a state-of-the-art review. *Applied Energy* 147, 536–555.
- André, J., Bonnans, F., Cornibert, L. (2009). Optimization of capacity expansion planning for gas transportation networks. *European Journal of Operational Research* 197(3), 1019–1027.
- Babonneau, F., Nesterov, Y., Vial, J.-P. (2012). Design and operations of gas transmission networks. *Operations Research* 60(1), 34–47.
- Martin, A., Möller, M., Moritz, S. (2006). Mixed integer models for the stationary case of gas network optimization. *Mathematical Programming* 105 (authors and volume unverified; title and journal verified).
- Wong, P. J., Larson, R. E. (1968). Optimization of natural-gas pipeline systems via dynamic programming. *IEEE Trans. Automatic Control* *(unverified)*.
- Carter, R. G. (1998). Pipeline optimization: dynamic programming after 30 years. PSIG *(unverified)*.

**Network validation**
- Schmidt, M. et al. (2017). GasLib — A library of gas network instances. *Data* 2(4), 40. — Public benchmark networks for validating the network solver.

**Route and terrain (front end)**
- Automatic pipeline route design with multi-criteria evaluation based on least-cost path analysis (Mus project, Turkey). *ISPRS Int. J. Geo-Information* 8(4), 173 (2019). — GIS least-cost routing on a DEM; candidate sites for method 5.

**Industry context (project library)**
- *Compression Machinery for Oil and Gas*, ch. 9 "Midstream" (Elsevier, in the project): stations every 48–112 km, typical ratios 1.2–1.8, spacing optimised between station capital cost and power cost.
