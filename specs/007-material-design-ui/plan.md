# Implementation Plan: Material Design Visual Refresh

**Branch**: `007-material-design-ui` | **Date**: 2026-09-18 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/007-material-design-ui/spec.md`

## Summary

Rebuild the application's presentation on **MudBlazor 9.10.0**, a Material Design component library, without changing a single behaviour, number or word. A `MudTheme` holds the entire visual system — both palettes, typography and layout constants — in one C# file; components become `MudPaper`, `MudButton`, `MudChip`, `MudGrid`; and dark mode arrives free, because `MudThemeProvider` observes the system preference by default and follows live changes to it.

Adoption is full: `MudChart` replaces the hand-written SVG chart and `MudList` replaces the `<ul>`/`<li>` activity list. That required **Amendment 1** to the spec, because MudChart cannot dash its series, brings hover and click interactions, and owns its own geometry — costing the chart's colour-blind differentiation and about 7 tests. The amendment records what was conceded and what was not ([spec.md](spec.md#amendments), [research.md](research.md) R15, R16).

Three criteria that would otherwise be opinions become tests: contrast ratios (read straight off the theme object, both palettes), the absence of colour literals outside the theme, and the preservation of every user-visible string.

## Technical Context

**Language/Version**: C# 13 / .NET 10 (`net10.0`)

**Primary Dependencies**: **MudBlazor 9.10.0** (new — targets `net10.0` natively). Existing: ASP.NET Core Blazor Web App (Interactive Server), EF Core + SQLite, bUnit 2.11.3, xUnit v3

**Storage**: Unchanged — SQLite via `ImportDbContext`. This feature writes nothing and reads nothing new

**Testing**: xUnit v3 via Microsoft.Testing.Platform, bUnit for component rendering, `WebApplicationFactory` for host tests. New: a contrast test reading the theme object, a colour-literal scan, and text-content characterization tests

**Target Platform**: Modern evergreen browsers, desktop and mobile; server-rendered from ASP.NET Core with prerendering

**Project Type**: Web application — a Blazor Web App front end over a layered domain/infrastructure backend

**Performance Goals**: No regression in time to first meaningful content (NFR-001). Adds MudBlazor's CSS and JS bundle to the initial payload — the one place this feature could plausibly regress, and worth a look during review

**Constraints**: Zero behaviour change (FR-001); the pre-existing 99 tests pass with assertions untouched (SC-002); 4.5:1 / 3:1 contrast in both palettes (FR-019); no horizontal scroll from 320px (FR-017); 48px touch targets (FR-021); no in-app theme toggle (FR-027)

**Scale/Scope**: 8 athlete-facing surfaces × 2 palettes. 8 Razor components touched, 1 theme file added, 1 stylesheet reduced to near-nothing, 1 package added, 1 production class and ~7 tests deleted, 0 domain files touched

## Constitution Check

*GATE: evaluated before Phase 0 and re-evaluated after Phase 1.*

| Principle | Assessment | Verdict |
|-----------|------------|---------|
| **I. Strict TDD** | The parts with a right answer are driven test-first: contrast in both palettes, palette slot-set equality, colour-literal absence, string preservation. The parts that are matters of taste — spacing rhythm, elevation — are verified by human review, which is what the quickstart's manual checklist is for. Asserting `border-radius: 12px` would be testing the implementation, which Principle I forbids | **PASS** |
| **II. Domain Independence from Strava** | No domain file touched. No Strava concept enters the theme | **PASS** |
| **III. Simplicity Before Abstraction** | **DEVIATION** — a component library is by definition more than this application needs. Taken deliberately, with the reason documented in [research.md](research.md) R1 and the formal justification in Complexity Tracking below. Mitigated by adding only `MudThemeProvider` (not the popover/dialog/snackbar providers), and by not converting markup that does not benefit (R15) | **DEVIATION, JUSTIFIED** |
| **IV. Testability** | No mocks added. Two recorded costs: bUnit must run with loose JS interop for MudBlazor to render (R13), and ~7 tests are deleted because the behaviour they pinned now lives in the library (R15). Each deletion must be named in the completion review | **PASS, with recorded costs** |
| **V. Isolation of External Integrations** | Untouched. No integration code changes | **PASS** |
| **VI. Observability & Deliberate Error Handling** | The unavailable and error surfaces gain *visual* treatment (FR-014, FR-015); their trigger conditions and messages are unchanged | **PASS** |
| **VII. Specification Adherence** | The conflict between full MudBlazor adoption and FR-003/FR-013/FR-022/FR-029/SC-002 was resolved by **amending the spec**, which is what this principle requires, rather than by deviating in code. R16 bounds what the amendment licenses; FR-027 still forbids a theme toggle | **PASS** |

**Post-Phase 1 re-evaluation**: the design adds one package, one theme file and one small palette type. No new project, no repository, no service, no abstraction with a single implementation. The Principle III deviation is confined to the dependency itself and does not propagate into the codebase's structure.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|--------------------------------------|
| **MudBlazor dependency** (Principle III; Technology Constraints, "a different frontend technology MUST NOT be introduced without a documented reason") | The feature's goal is that the application reads as *authentically* Material — FR-004, and US1's "looks like a designed product". MudBlazor encodes Material's proportions, states and elevation; a hand-authored token layer reproduces them only as far as the author's eye. For a project whose purpose is evaluating spec-driven AI development rather than learning CSS, buying the visual language spends the budget better | A hand-authored CSS token layer was fully designed and costed first, and is genuinely viable — lighter, no interop, no prerender flash. It was rejected by the developer on 2026-09-18 after review, in favour of authentic Material. The costs accepted in exchange are enumerated in [research.md](research.md) R1 and R14 |
| **Loose JS interop in bUnit** (Principle IV) | MudBlazor components invoke JS on render; bUnit's strict default throws, so no test could render a Mud component at all | Stubbing each interop call individually — rejected as far more test scaffolding than the components under test warrant, and Principle IV specifically warns against test-double scaffolding |
| **Loss of the chart's non-colour differentiation** (spec FR-022, and feature 006's deliberate accommodation) | Full MudChart adoption, requested by the developer. MudChart emits no `stroke-dasharray` and offers no option for one | A CSS override on `.mud-chart-line path.mud-chart-serie:nth-of-type(n)` would restore the dashes and was offered on 2026-09-18. The developer chose to accept the loss. Recorded in Amendment 1 as a real accessibility regression, with series identification falling back to the legend and hover labels (FR-013a) |
| **Deleting ~7 passing tests** (Principle I; spec SC-002 before amendment) | `MetricsChart.Plot`'s geometry moves into MudChart, so its 4 tests pin code that no longer exists, and 3 `polyline` assertions describe an element type no longer rendered | Keeping the custom chart — rejected by the developer. SC-002 was amended rather than bypassed: removals are permitted only if recorded in the completion review, and no assertion may be left vacuous |

## Project Structure

### Documentation (this feature)

```text
specs/007-material-design-ui/
├── plan.md              # This file
├── spec.md              # Feature specification
├── research.md          # Phase 0 — 15 decisions with rationale
├── data-model.md        # Phase 1 — no persisted data; theme model + surface/scheme matrix
├── quickstart.md        # Phase 1 — validation guide
├── contracts/
│   ├── design-tokens.md # The theme contract: palettes, typography, contrast obligations
│   └── markup-contract.md # What each component guarantees to emit
├── checklists/
│   └── requirements.md  # Spec quality checklist (16/16)
└── tasks.md             # Phase 2 — created by /speckit-tasks, NOT by this command
```

### Source Code (repository root)

```text
src/TrainingLoadAnalyzer.Web/
├── TrainingLoadAnalyzer.Web.csproj           # + PackageReference MudBlazor 9.10.0
├── Program.cs                                # + AddMudServices()
├── _Imports.razor                            # + @using MudBlazor
├── wwwroot/
│   └── app.css                               # REDUCED: dead rules deleted; keeps the reduced-motion rule
├── Theme/
│   ├── TrainingLoadTheme.cs                  # NEW: the MudTheme — both palettes, typography, layout
│   └── ChartPalette.cs                       # NEW: 3 series × 2 schemes (MudTheme has no slot)
├── Components/
│   ├── App.razor                             # + MudBlazor.min.css and MudBlazor.min.js
│   ├── Layout/
│   │   ├── MainLayout.razor                  # + MudThemeProvider, MudLayout, MudAppBar; emits chart vars
│   │   └── MainLayout.razor.css              # REVISED: #blazor-error-ui onto theme colours
│   ├── Dashboard/
│   │   ├── MetricTile.razor                  # → MudPaper / MudText / MudChip
│   │   ├── SyncPanel.razor                   # → MudButton Class="sync"
│   │   ├── WeeklyLoadPanel.razor             # → MudPaper / MudText / MudChip
│   │   ├── RecentActivityList.razor          # → MudList (R15)
│   │   ├── MetricsChartView.razor            # → MudChart; keeps date range + insufficient-data text
│   │   └── MetricsChartView.razor.css        # REDUCED: MudChart owns the plot's styling
│   └── Features/Dashboard/
│       └── MetricsChart.cs                   # DELETED — geometry moves into MudChart (R15)
│   └── Pages/
│       ├── Dashboard.razor                   # → MudContainer + MudGrid
│       ├── Error.razor                       # → MudPaper; inert Bootstrap class removed
│       └── NotFound.razor                    # → MudPaper

tests/TrainingLoadAnalyzer.Web.Tests/
├── (existing tests — arrange step gains AddMudServices + loose interop)
├── MetricsChartTests.cs                      # DELETED — 4 tests pinning MetricsChart geometry
├── DashboardComponentTests.cs                # 3 polyline assertions removed; li test REWRITTEN, not left vacuous
├── Theme/
│   ├── ContrastRatio.cs                      # NEW: WCAG relative luminance (test-side only)
│   ├── PaletteContrastTests.cs               # NEW: both palettes against the obligation table
│   └── ColourDisciplineTests.cs              # NEW: no colour literals outside Theme/
└── InformationPreservationTests.cs           # NEW: text-content characterization per state

src/TrainingLoadAnalyzer.Domain/              # NOT TOUCHED
src/TrainingLoadAnalyzer.Infrastructure/      # NOT TOUCHED
```

**Structure Decision**: the existing layered layout is unchanged. This feature is confined to `src/TrainingLoadAnalyzer.Web` and `tests/TrainingLoadAnalyzer.Web.Tests`. The one structural addition is `Theme/`, which exists so that NFR-002's "one definition site" is a directory you can point at — and so the contrast test can read the palette as an object rather than parsing CSS ([research.md](research.md) R6).

## Phase 0 — Complete

[research.md](research.md): 16 decisions. The load-bearing ones are R1 (adopt MudBlazor, with costs stated), R3 (dark mode verified from library source — automatic, no code), R5 (chart colours, the one thing the theme cannot carry), R6 (contrast test reads the theme object), R10 (app bar, reversed, with the `FocusOnNavigate` problem solved rather than ignored), R13 (the bUnit risk), R14 (prerender flash, accepted), R15 (what full MudChart/MudList adoption actually costs, verified from library source) and R16 (what Amendment 1 does *not* license).

## Phase 1 — Complete

- [data-model.md](data-model.md) — states the absence of persisted data as a constraint, then models the theme and the 8×2 surface/scheme verification matrix
- [contracts/design-tokens.md](contracts/design-tokens.md) — both palettes, typography, layout, and the contrast obligations with one declared exclusion
- [contracts/markup-contract.md](contracts/markup-contract.md) — the invariants the existing tests bind to, per-component conversion, and the test-side arrange changes
- [quickstart.md](quickstart.md) — the regression gate, the automated checks, and the manual review no test can cover

## Risks

| Risk | Severity | Mitigation |
|------|----------|------------|
| **The 99 tests do not survive MudBlazor's interop requirements** | Highest — it is the one unverified assumption in the plan | First task is a spike: add the package, register services, convert one component, run the suite. Go/no-go before any further work (R13) |
| A qualifier chip replaces its text, losing meaning for screen readers | High — the likeliest silent regression, and now the *last* non-colour guarantee left outside the chart | Called out in the markup contract and R16, covered by the text-content test, re-checked by eye in the quickstart |
| Amendment 1 is read too broadly — "colour alone is fine now" spreads from the chart to chips and provenance | High | R16 states the four things conceded and the everything-else not conceded. FR-022's carve-out names the chart plot specifically |
| A test is deleted without being recorded | Medium | SC-002 as amended permits removal only with the removal named in the completion review |
| Someone re-adds dash patterns via CSS, reopening a settled decision | Low | R5 records that the override exists, was offered, and was declined |
| Prerender flash of light theme on first load in dark mode | Medium — a real regression versus the rejected CSS approach | Accepted with alternatives weighed (R14). Revisit if it proves irritating in use |
| MudBlazor's derived disabled/hover colours fail contrast | Medium — they are computed, not authored | `TextDisabled` is in the contrast obligation table; the disabled sync button is a named review item |
| Someone "fixes" the low line-to-line contrast between chart series | Low | R5 records why that is wrong: WCAG asks each line to contrast with its background, not its neighbours |
| Scope creep into a theme toggle, which MudBlazor makes trivial | Low | FR-027 forbids it; called out in the constitution check |
