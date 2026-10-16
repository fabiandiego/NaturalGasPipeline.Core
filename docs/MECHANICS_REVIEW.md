# Mechanics module — review checklist

**Status: PENDING REVIEW.** `NaturalGasPipeline.Mechanics` was implemented on
2026-10-16 from public sources only. Its results are usable for the
simulator's checks, but they are **not** a code-compliant design until every
item below is signed off by the owner.

Every item is also marked in the code with `[PendingReview("M-n", ...)]`.
The test `MechanicsReviewTests.PendingReviewItems_AreListed` prints all marks
in each CI run (annotation "Mechanics pending review") and fails if the set of
open items in the code differs from its `ExpectedOpenItems` list, so this file,
the attributes and that list must be updated together.

## What is implemented

- Design pressure (gauge): `P = (2·S·t/D)·F·E·T` — `SteelPipeDesign.DesignPressureGauge`.
- Minimum wall for internal pressure: `t = P·D/(2·S·F·E·T)` — `SteelPipeDesign.RequiredWallThickness`
  (no corrosion allowance, mill tolerance or external loads).
- Hoop stress, %SMYS, D/t range warning and gauge ↔ absolute conversion — `SteelPipeDesign.Check`.
- Factors: F by location class, E by seam, T by temperature (interpolated) — `DesignFactors`.
- API 5L grade catalogue B…X80 — `ApiLinePipeGrades`.
- Station planner: optional absolute MAOP in `StationPlanningCriteria`; every section is
  checked at its inlet and every segment end, so pressure regained downhill is caught.

## Sources used

| Source | Used for | Verified |
|---|---|---|
| US 49 CFR Part 192, §192.105, .107, .111, .113, .115 (ecfr.gov, read 2026-10-16) | Formula; F = 0.72/0.60/0.50/0.40; E table (API 5L rows); T table | Yes (public regulation) |
| ASME B31.8 §841.1.1 and Tables 841.1.6-1/-2, 841.1.7-1, 841.1.8-1 | Same formula, code tables | **No** — paywalled |
| Secondary web sources on B31.8 Class 1 Division 1 | F = 0.80 | **No** |
| API 5L / ISO 3183 grade designations (L-number = SMYS in MPa) | SMYS catalogue | **No** — standard not consulted |

## Open items

| Id | Item | What to check | Where |
|---|---|---|---|
| M-1 | Formula and factors against ASME B31.8 | Compare §841.1.1 and its tables (edition adopted by the course) with `DesignFactors`; reproduce one worked example (Menon, *Gas Pipeline Hydraulics*, pipe strength chapter, or course notes) as a test. | `SteelPipeDesign`, `SteelPipeDesignTests` |
| M-2 | Class 1 Division 1, F = 0.80 | Confirm value and its condition (hydrostatic test level) in the adopted B31.8 edition, or remove the option. | `DesignFactors.BasicDesignFactor` |
| M-3 | SMYS values | Decide SI (L245…L555) vs US-customary (35 000…80 000 psi, 0.2–1.7 % lower) according to the API 5L edition adopted; PSL1 vs PSL2. | `ApiLinePipeGrades` |

## Known limits (not review items, by design)

- Design pressure ≠ MAOP: test pressure / class test factor, component ratings and
  regulation also limit MAOP. Only the pipe-strength limit is computed.
- Crossings, bridges, fabricated assemblies and station piping need a lower F
  (≤ 0.50 in stations, `DesignFactors.StationPipingDesignFactor`); the caller passes it.
- Thin-wall Barlow with outside diameter; flagged when D/t < 20.

## Sign-off

| Id | Reviewed by | Date | Result |
|---|---|---|---|
| M-1 | | | |
| M-2 | | | |
| M-3 | | | |
