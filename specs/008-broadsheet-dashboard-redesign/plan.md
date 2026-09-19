# Implementation Plan: Broadsheet Dashboard Redesign

**Branch**: `008-broadsheet-dashboard-redesign` | **Date**: 2026-09-19 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/008-broadsheet-dashboard-redesign/spec.md`

## Summary

Replace the Material/MudBlazor dashboard built by feature 007 with the newsprint layout specified
in `docs/ui` — a sticky left rail holding every control, a content column holding four display
figures, a hand-drawn SVG chart and a recent-sessions table — while preserving every figure,
qualifier, message and state the current interface shows.

The technical approach, settled in [research.md](./research.md): **remove MudBlazor entirely** (R1)
and rebuild the nine athlete-facing `.razor` files on plain HTML carrying classes from a vendored
subset of the Broadsheet stylesheet (R2). Dark mode drops its JavaScript round-trip and becomes a
`prefers-color-scheme` media query over the same custom properties (R3). The trend chart returns to
hand-drawn SVG, recovered from commit `b1e8726^` for its culture-safe coordinate formatting, and
extended with load bars, a zero rule and a date axis (R5).

A pre-implementation contrast audit found nine measured shortfalls in the reference design against
the spec's own 4.5:1 / 3:1 bars; eight are fixed by stepping down the design system's existing
ramps, and the ninth — the chart's load bars at 1.33:1 — is exempted by recorded amendment rather
than silently shipped (R4, Amendment 1). The audit becomes executable: the palette tests are
repointed from MudBlazor's `Palette` type at the stylesheet's custom properties, so every row of
that table has a RED state (R7).

## Technical Context

**Language/Version**: C# 13 / .NET 10 (`net10.0`)

**Primary Dependencies**: ASP.NET Core Blazor Web App (Server interactivity), Entity Framework Core,
SQLite. **MudBlazor 9.10.0 is removed by this feature** — after it, the Web project has no UI
component library.

**Storage**: SQLite (unchanged; this feature performs no schema, query or read-model change beyond
adding two display members to `DashboardView`)

**Testing**: xUnit v3 (`xunit.v3.mtp-v2` 4.0.1), bUnit 2.11.3 for component rendering,
`Microsoft.AspNetCore.Mvc.Testing` for host-level tests. Plain `Assert.*` — no mocking library, no
FluentAssertions, no Playwright.

**Target Platform**: Modern evergreen browsers, desktop and phone, served by a locally run
ASP.NET Core host

**Project Type**: Server-rendered web application (single Web project over a Domain and an
Infrastructure project)

**Performance Goals**: First meaningful content no slower than today for the same stored history
(NFR-001). Removing `MudBlazor.min.css`/`.js` and the theme provider's first-render JS round-trip
should improve this; the requirement is only that it not regress.

**Constraints**: No horizontal scrolling from 320px up; 4.5:1 / 3:1 contrast in both schemes; 48×48
touch targets; exactly one `<h1>` per page (`FocusOnNavigate Selector="h1"`); no colour literal
outside a `Theme` path segment (`ColourDisciplineTests`); every SVG coordinate formatted with
`InvariantCulture`.

**Scale/Scope**: 9 athlete-facing `.razor` files, ~15 MudBlazor component types removed, 1 vendored
stylesheet, 1 revived chart module, ~5 test files rewritten or extended.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design — see below.*

| Principle | Assessment | Verdict |
| --- | --- | --- |
| **I. Strict TDD** | Every requirement class has a RED state before code: contrast and token coverage via the repointed palette tests over parsed CSS (R7), rendered text and markup hooks via bUnit (R8), chart geometry via the revived `MetricsChartTests`, layout and reflow via rendered-markup assertions. The visual judgements that cannot carry a test (does it *read* as newsprint) are explicitly listed for human review in [quickstart.md](./quickstart.md) rather than asserted vacuously. | PASS |
| **II. Domain independence from Strava** | No domain or Infrastructure change. `DashboardView` gains `MaximumHeartRate` and an ISO week designation — both Web-layer read-model members sourced from `AthleteSettings` and the existing `AsOf` date. No Strava type moves anywhere. | PASS |
| **III. Simplicity before abstraction** | The feature is net-subtractive: a dependency, a theme class, a chart palette, a JSInterop fake and two asset links are removed. The vendored stylesheet is a subset, not all 394 lines (R2). One new abstraction is introduced — a shared bUnit test base — justified by two existing duplications that must change together plus the new tests (R11). No production abstraction is added. | PASS |
| **IV. Testability** | No mocking library is introduced. The contrast tests parse a file and compute a ratio; the component tests render against a real reader, a real coordinator and a real SQLite fixture, as today. | PASS |
| **V. Isolation of external integrations** | Untouched. `/connect` remains a redirect-only minimal-API endpoint; sync coordination, token handling and rate-limit handling are not modified. | PASS |
| **VI. Observability & deliberate error handling** | All six existing states — loading, data-unavailable, empty (connected and not), reconnection-required, rate-limited — are restyled, not dropped, and each keeps its wording (FR-009, FR-002). `SyncMessage` is not modified. | PASS |
| **VII. Specification adherence** | Three decisions that would have changed scope or accessibility were put to the developer rather than taken silently (R1, R2, R4), and the two internal spec conflicts the audit exposed are resolved in the spec via Amendment 1, not in code. | PASS |

**Post-Phase-1 re-check**: no gate changed. The design adds no production abstraction beyond the
two read-model members Amendment 1 admits, and the one test-side abstraction remains justified by
current duplication. `ReconnectModal` — which has no test coverage today — is restyled under the
same visual system, and Phase 1 records the gap rather than leaving it implicit.

## Project Structure

### Documentation (this feature)

```text
specs/008-broadsheet-dashboard-redesign/
├── plan.md              # This file
├── research.md          # Phase 0 output — R1..R12
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/
│   └── dashboard-ui.md  # Phase 1 output — the rendering contract
├── checklists/
│   └── requirements.md  # From /speckit-specify
└── tasks.md             # Phase 2 output (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

```text
src/TrainingLoadAnalyzer.Web/
├── Components/
│   ├── App.razor                        # drop MudBlazor css/js links, add broadsheet.css
│   ├── Routes.razor                     # unchanged (FocusOnNavigate Selector="h1")
│   ├── _Imports.razor                   # drop @using MudBlazor
│   ├── Layout/
│   │   ├── MainLayout.razor(.css)       # MudLayout/MudAppBar/MudThemeProvider → plain shell
│   │   └── ReconnectModal.razor(.css)   # repoint 7 --mud-palette-* refs to Broadsheet tokens
│   ├── Pages/
│   │   ├── Dashboard.razor(.css)        # rail + content grid; holds window-selector state
│   │   ├── NotFound.razor
│   │   └── Error.razor
│   └── Dashboard/
│       ├── MetricRow.razor(.css)        # replaces MetricTile + WeeklyLoadPanel as one figure row
│       ├── SyncPanel.razor(.css)        # moves into the rail
│       ├── RecentActivityList.razor(.css)
│       └── MetricsChartView.razor(.css) # MudChart → hand-drawn SVG
├── Features/Dashboard/
│   ├── DashboardView.cs                 # + MaximumHeartRate, + IsoWeek
│   ├── DashboardViewBuilder.cs          # populate the two new members
│   └── MetricsChart.cs                  # REVIVED from b1e8726^, extended
├── Theme/
│   ├── TrainingLoadTheme.cs             # DELETED (MudTheme)
│   └── ChartPalette.cs                  # DELETED (MudChart positional palette)
├── wwwroot/
│   ├── Theme/broadsheet.css             # NEW — vendored token + component subset (R2)
│   └── app.css                          # drop the two dead .mud-button-root rules
└── Program.cs                           # drop AddMudServices()

tests/TrainingLoadAnalyzer.Web.Tests/
├── DashboardComponentTests.cs           # drop AddMudServices; keep the hooks in R8
├── InformationPreservationTests.cs      # the FR-002 / SC-001 safety net — assertions unchanged
├── MetricsChartTests.cs                 # REVIVED from b1e8726^, extended
├── DashboardRenderContext.cs            # NEW — the shared bUnit base (R11)
├── Fakes/MudChartBounds.cs              # DELETED
└── Theme/
    ├── ContrastRatio.cs                 # unchanged
    ├── ColourDisciplineTests.cs         # unchanged — broadsheet.css sits under a Theme segment
    ├── PaletteContrastTests.cs          # repointed: MudColor → parsed CSS custom properties
    └── PaletteSlotTests.cs              # repointed: palette slots → required token set
```

**Structure Decision**: The existing three-project layout is unchanged — this feature touches only
`TrainingLoadAnalyzer.Web` and its test project. The one structural addition is
`wwwroot/Theme/broadsheet.css`; the `Theme` path segment is load-bearing, since
`ColourDisciplineTests` exempts exactly that directory name from its colour-literal scan and would
otherwise fail on the vendored sheet's hex values (R2).

`MetricTile` and `WeeklyLoadPanel` merge into a single `MetricRow`, because the reference sets
Fitness, Fatigue, Form and week load as four peers in one row rather than as three cards plus a
separate panel. Their `.razor.css` files are pure layout and are rewritten rather than repointed.

## Phase Outputs

- **Phase 0** — [research.md](./research.md): R1..R12, including the measured contrast table and
  the two spec conflicts resolved by Amendment 1.
- **Phase 1** — [data-model.md](./data-model.md) (read model, design tokens, chart geometry),
  [contracts/dashboard-ui.md](./contracts/dashboard-ui.md) (region, state and markup-hook
  contract), [quickstart.md](./quickstart.md) (how to run and validate, including what only human
  review can settle).

## Complexity Tracking

> Fill ONLY if Constitution Check has violations that must be justified.

No violations. The feature removes more than it adds: one dependency, two theme classes, one
JSInterop fake and ~150 lines of inert vendored CSS. The single new abstraction — the shared bUnit
render context — is test-side and replaces existing duplication rather than anticipating future
need.

One risk is tracked rather than justified: `ReconnectModal.razor` is the heaviest consumer of
`--mud-palette-*` tokens (7 references) and **has no test coverage at all**. Repointing it to
Broadsheet tokens is therefore unverified by the suite, and it is flagged for explicit human review
in [quickstart.md](./quickstart.md). Writing a first test for it is in scope for `/speckit-tasks`
to schedule.
