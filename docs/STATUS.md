# NaturalGasPipeline — Project status

**Version 2.0 — 2026-10-05.** Supersedes the Spanish "Diagnóstico del motor" (v1, 2026-10-04).
Repository: `fabiandiego/NaturalGasPipeline.Core`, branch `claude/ubicacion-estaciones` (contains all earlier branches).
Build and tests: GitHub Actions on every push (netstandard2.1 + net8.0, all tests), and Visual Studio 2022 on the owner's machine (344 tests passed on 2026-10-05, before the station-planning work).

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
| Thermodynamics | Components, mixtures, characterisation (Kay, Sutton, Standing; Wichert-Aziz, Carr-Kobayashi-Burrows), Z (ideal, constant, PR, SRK, DAK, Hall-Yarborough, Papay, CNGA), viscosity (Lee-Gonzalez-Eakin, constant) |
| Hydraulics | Friction (Colebrook-White, Haaland, Swamee-Jain, Churchill, AGA fully turbulent, AGA with drag factor, constant f / F), isothermal compressible segment solver |
| Correlations | Weymouth, Panhandle A/B, General Flow / AGA; solves Q, P1, P2 or D |
| Compression | Isentropic / polytropic stage, multi-stage station with intercooling |
| Network | Segment models, route profiles, series systems, nodal network solver, **compressor station planner** |
| Mechanics | Reserved, empty |

Targets: netstandard2.1 (Unity 6, Mono, C# 9) and net8.0. PolySharp supplies compiler attributes on netstandard2.1. `scripts/build-unity-plugins.*` produces the DLLs for Unity.

## 4. Validation summary

### Compressibility and fluid properties
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

- Concept screens (artifact): tabs in workflow order — Fluid Definition → Pipeline Architecture Drafter → Hardware Drafter → Network Map 3D.
- Default language English; Russian, Spanish, Arabic and Persian postponed (Arabic and Persian need right-to-left layout and glyph shaping).
- Map: schematic horizontal scale over real terrain (DEM); lengths and elevations used by the engine come from the DEM.
- v1 scope: the map and an intermediate section between two standard compressor stations.
- Unity 6 loads netstandard2.1 DLLs only; CoreCLR is announced for Unity 7.
- Next UI change: the Architecture tab shows separate **Nodes** and **Segments** tables, plus the station-planning results and the N-vs-power table.

## 8. Open items and known issues

- MAOP from ASME B31.8 (wall, grade, location class) to bound the discharge pressure.
- Machinery library (compressors, drivers, efficiencies, fuel) — question 4.
- Heating value (ISO 6976) is needed if drivers are gas turbines (previously parked as "PCS").
- Non-isothermal pipe flow; compressors and regulators inside networks; GasLib validation.
- `Unity Gas Pipeline Simulator_tester/` is a legacy stub (unimplemented `GeneralFlowEquation`) superseded by the Correlations project; candidate for removal (owner's decision).
- `NaturalGasPipeline.Mechanics` is empty.
- CI uses actions targeting Node.js 20 (deprecated; works, update when convenient).
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
