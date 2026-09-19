# Completion Review: Material Design Visual Refresh

**Feature**: 007-material-design-ui | **Date**: 2026-09-18 | **Reviewer**: AI agent, pending human sign-off

The constitution requires each feature's completion review to check explicitly against Principles I, II and III, and the amended SC-002 requires every removed test to be named here.

## Test accounting

| | Count |
|---|---|
| Web tests before the feature | 99 |
| Removed under Amendment 1 | 7 |
| Surviving pre-existing tests | 92 |
| Added by this feature | 34 |
| **Web tests now** | **126** |
| Whole solution | **436, all passing** |

### Every test removed, named

Amendment 1 permits these removals; it does not permit doing it quietly.

| Test | File | Why it could not survive |
|---|---|---|
| `Three_series_carry_one_point_per_day` | `MetricsChartTests.cs` | Pinned `MetricsChart.Plot`'s output. MudChart computes its own geometry; the class is deleted |
| `Coordinates_are_machine_readable_whatever_the_current_culture_is` | `MetricsChartTests.cs` | Guarded the decimal-comma hazard (a Finnish locale silently malforming an SVG coordinate list). That risk now sits inside the library, where this project cannot test it — the most consequential of the seven losses |
| `A_negative_form_is_plotted_rather_than_clipped` | `MetricsChartTests.cs` | Pinned how a negative form value was scaled. MudChart owns the axis now |
| `An_empty_history_yields_no_series_at_all` | `MetricsChartTests.cs` | Same |
| `The_chart_renders_three_polylines_inside_an_svg` | `DashboardComponentTests.cs` | MudChart renders `<path>`, not `<polyline>` |
| `Every_chart_line_carries_the_isolation_scope_that_paints_it` | `DashboardComponentTests.cs` | Guarded bug `chart-not-rendered`: that our scoped CSS reached the polylines. MudChart paints via a `stroke` attribute, so the mechanism no longer exists |
| `The_chart_stylesheet_gives_every_line_a_stroke` | `DashboardComponentTests.cs` | Read `MetricsChartView.razor.css` for `.line-*` stroke rules. That stylesheet no longer paints anything |

The last two were feature 006's guard against the invisible-chart bug. **That guard is gone**, and nothing replaces it: if MudChart ever renders unpainted, no test here will notice. It is a real reduction in coverage, accepted as part of handing the plot to the library.

### Tests rewritten rather than removed

| Test | Was | Now |
|---|---|---|
| `Without_enough_history_the_chart_explains_itself_instead_of_drawing` | `Assert.Empty(FindAll("polyline"))` | `Assert.Empty(FindAll("svg"))` — same claim, new markup |
| `An_empty_recent_list_says_so` | `Assert.Empty(FindAll("li"))` | Asserts the `Nothing recorded yet.` message and no `.recent-row`. Against MudList's `<div>`s the old form would have passed **vacuously** — green while testing nothing |

### One test kept and now load-bearing

`The_chart_names_each_line_in_a_legend` was written for feature 006 as a convenience. Since Amendment 1 removed the dash patterns, the legend is the **only** thing distinguishing one series from another, so this test now guards FR-013a.

## Principle I — Strict TDD

**Held, with a stated boundary.** The parts with a right answer were driven RED → GREEN: the contrast helper against known ratios, both palettes against the obligation table, palette slot-set equality, the colour-literal scan (which failed against the chart stylesheet before it passed), and the 12 characterization tests, written against unconverted markup precisely so they could not be shaped by the result.

The component conversions had no meaningful RED. A test asserting that `MudPaper` renders a `div` tests the library, and Principle I forbids tests that describe the implementation. This is recorded as a judgement, not concealed.

**Where the discipline paid**: the colour-literal scan found `ReconnectModal.razor.css` — the connection-lost notice — which appeared in no plan or contract. Nobody would have remembered it; it would have shipped with a hard-coded white background in dark mode.

## Principle II — Domain Independence

**Held.** No file under `TrainingLoadAnalyzer.Domain` or `TrainingLoadAnalyzer.Infrastructure` was touched. Both suites pass unchanged. `MetricsChart.cs` was deleted from the Web project only.

## Principle III — Simplicity Before Abstraction

**Deviated, deliberately, as recorded in the plan's Complexity Tracking.** MudBlazor is by definition more than this application needs. Mitigations held: only `MudThemeProvider` was registered (no popover, dialog or snackbar providers), and the theme declares 10 colour slots rather than Material's ~30.

**One abstraction added**: `ChartPalette`, a static class holding six colours. It exists because `MudTheme` has no slot for a chart series. It has one consumer and one test; keeping it is judged better than inlining hex into a component, which the colour-literal scan would reject.

## Principle VI — Deliberate Error Handling

**Held.** Trigger conditions and messages are unchanged; only presentation moved. `#blazor-error-ui` no longer pins itself to `color-scheme: light only` with a `lightyellow` background and now works in both schemes.

## Principle VII — Specification Adherence

**Held, via amendment rather than improvisation.** Full MudChart/MudList adoption conflicted with FR-003, FR-013, FR-022, FR-029 and SC-002. The spec was amended (Amendment 1) before the code was written, which is what this principle requires.

Two temptations named in research and avoided: no theme toggle (FR-027), and no `stroke-dasharray` CSS override to quietly restore what Amendment 1 gave up.

## Deviations from the plan, and why

| Deviation | Reason |
|---|---|
| `<Routes @rendermode="InteractiveServer" />` added to `App.razor` | Not in the plan. Without it the layout renders as static SSR, `MudThemeProvider` never runs its JS, and dark mode never activates. See corrected R3 |
| Explicit `GetSystemDarkModeAsync()` in `MainLayout` | The plan claimed this came free. It does not — the library's watcher only handles *changes*. Research R3 has been corrected rather than quietly patched |
| 7 tests removed, not the 3 the plan predicted | The plan counted `polyline` assertions and missed that two further tests guarded the chart's stylesheet and CSS isolation |
| `MetricTile.razor.css` etc. added | Small layout-only stylesheets; the plan assumed MudBlazor would cover everything |

## Verified by running the application

Not merely by tests. Both schemes were emulated over CDP and the computed palette read back:

- Light: background `#FDFCFF`, app bar `#00639B`, chart `#0072B2`/`#D55E00`/`#009E73`
- Dark: background `#111318`, app bar `#1E2025`, chart `#56B4E9`/`#E69F00`/`#009E73`
- 320px → 2560px: no horizontal scroll, no overflowing element, at any width
- Sync button renders 156×48 — meets FR-021's 48px floor, which MudBlazor's own `padding: 6px 16px` does not
- Exactly one `<h1>`; the app bar title is a `<span>`

**Caution recorded**: Chrome's `--force-dark-mode` flag made the page *look* correctly dark while the theme was entirely light. Any future check of this must emulate `prefers-color-scheme`, not force dark rendering.

## Outstanding

- [ ] **Human review of the AI-generated code**, which the constitution requires before a feature is complete
- [ ] The prerender flash (R14) is still present and is now slightly worse: the scheme resolves after the first *interactive* render, not merely after interop. A dark-mode user sees a light flash on every load
- [ ] The chart does not fill its container's width — MudChart centres its plot, leaving a wide left margin on desktop. Cosmetic, not a requirement breach
- [ ] The sync panel is a full-width surface holding one button, which reads as empty. Worth a second look
