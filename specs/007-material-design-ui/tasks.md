---

description: "Task list for 007-material-design-ui"
---

# Tasks: Material Design Visual Refresh

**Input**: Design documents from `/specs/007-material-design-ui/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md) (including **Amendment 1**), [research.md](research.md), [data-model.md](data-model.md), [contracts/](contracts/)

**Tests**: **Required, not optional.** Constitution Principle I (Strict TDD) is NON-NEGOTIABLE, and the Development Workflow requires tasks be expressed as RED/GREEN/REFACTOR/VERIFY steps tied to concrete given/when/then scenarios. Where a step has no meaningful RED — CSS spacing, elevation, a component swap whose only assertion is "it renders" — the task says so explicitly rather than inventing a test that asserts the implementation.

**Organization**: Grouped by user story. US2 is deliberately sequenced **before** US1 even though both are P1: it builds the characterization net that makes every later conversion safe. See the note on Phase 3.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: US1–US5, mapping to spec.md
- Paths are repository-relative

## Path Conventions

- Web app: `src/TrainingLoadAnalyzer.Web/`, tests in `tests/TrainingLoadAnalyzer.Web.Tests/`
- Domain and Infrastructure are **not touched** by this feature

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Get MudBlazor into the solution and prove the existing suite survives it.

- [X] T001 **GO/NO-GO SPIKE** — add `<PackageReference Include="MudBlazor" Version="9.10.0" />` to `src/TrainingLoadAnalyzer.Web/TrainingLoadAnalyzer.Web.csproj`, call `builder.Services.AddMudServices()` in `src/TrainingLoadAnalyzer.Web/Program.cs`, add `@using MudBlazor` to `src/TrainingLoadAnalyzer.Web/Components/_Imports.razor`, link `_content/MudBlazor/MudBlazor.min.css` and `_content/MudBlazor/MudBlazor.min.js` in `src/TrainingLoadAnalyzer.Web/Components/App.razor`, convert exactly one element (the sync button) to `<MudButton Class="sync">`, add `Services.AddMudServices()` and `JSInterop.Mode = JSRuntimeMode.Loose` to `SeedAndRegister` in `tests/TrainingLoadAnalyzer.Web.Tests/DashboardComponentTests.cs`, then run `dotnet test --project tests/TrainingLoadAnalyzer.Web.Tests/TrainingLoadAnalyzer.Web.Tests.csproj`
- [X] T002 **STOP AND REPORT** — record the spike result. Baseline is `total: 99, failed: 0`. If any pre-existing test fails for a reason other than the deliberate removals in Amendment 1, halt and raise it before any further task. This is the single unverified assumption in the plan ([research.md](research.md) R13)
- [X] T003 [P] Delete the dead rules from `src/TrainingLoadAnalyzer.Web/wwwroot/app.css` — `.valid`, `.invalid`, `.validation-message`, `.blazor-error-boundary`, `.darker-border-checkbox`, `.form-floating`. Verified to target markup this app never renders ([research.md](research.md) R11). Keep `#blazor-error-ui` for now; it is restyled in T016

**Checkpoint**: MudBlazor renders, the suite is green, and the decision to proceed is explicit.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The theme. Everything else consumes it.

**⚠️ CRITICAL**: No user story work begins until this phase is complete.

- [X] T004 [P] **RED** — write `tests/TrainingLoadAnalyzer.Web.Tests/Theme/ContrastRatio.cs`, a WCAG 2.1 relative-luminance helper (test-side only, never in `src/`), plus its own unit tests against two known pairs: black on white = 21.0, and `#00639B` on `#FDFCFF` = 6.31 (±0.01). Fails because the file does not exist yet
- [X] T005 **GREEN** — implement `ContrastRatio` until T004 passes
- [X] T006 **RED** — write `tests/TrainingLoadAnalyzer.Web.Tests/Theme/PaletteContrastTests.cs` asserting every pair in the contrast obligation table of [contracts/design-tokens.md](contracts/design-tokens.md) for **both** `PaletteLight` and `PaletteDark`: `TextPrimary`/`Background` ≥ 4.5, `TextPrimary`/`Surface` ≥ 4.5, `TextSecondary`/`Background` ≥ 4.5, `TextSecondary`/`Surface` ≥ 4.5, `Primary`/`Background` ≥ 4.5, `Error`/`Background` ≥ 4.5, `TextDisabled`/`Surface` ≥ 4.5, and each of the three chart colours against `Surface` ≥ 3.0. Include `Divider` as a **named, commented exclusion** with the WCAG 1.4.11 rationale — an explicit entry, not an omission. Fails because the theme does not exist
- [X] T007 **RED** — write `tests/TrainingLoadAnalyzer.Web.Tests/Theme/PaletteSlotTests.cs` asserting `PaletteLight` and `PaletteDark` populate the identical set of slots (FR-025, FR-028). Fails for the same reason
- [X] T008 **GREEN** — create `src/TrainingLoadAnalyzer.Web/Theme/TrainingLoadTheme.cs`, a static `MudTheme` with the exact values from [contracts/design-tokens.md](contracts/design-tokens.md): light `Background #FDFCFF`, `Surface #F1F3F9`, `TextPrimary #1A1C1E`, `TextSecondary #43474E`, `Primary #00639B`, `AppbarBackground #00639B`, `Divider #C3C7CF`, `Error #BA1A1A`; dark `Background #111318`, `Surface #1E2025`, `TextPrimary #E2E2E6`, `TextSecondary #C3C7CF`, `Primary #96CCFF`, `AppbarBackground #1E2025`, `Divider #43474E`, `Error #FFB4AB`. Set `Typography` per the contract's five roles and `LayoutProperties.DefaultBorderRadius = 12px`. Run until T006 and T007 pass
- [X] T009 [P] **GREEN** — create `src/TrainingLoadAnalyzer.Web/Theme/ChartPalette.cs` holding the positional trio, light `#0072B2`, `#D55E00`, `#009E73` and dark `#56B4E9`, `#E69F00`, `#009E73`. Order is positional and must match the order series are supplied to the chart
- [X] T010 **RED** — write `tests/TrainingLoadAnalyzer.Web.Tests/Theme/ColourDisciplineTests.cs` scanning every `.razor` and `.css` file under `src/TrainingLoadAnalyzer.Web` for colour literals (hex, `rgb(`, `hsl(`, CSS named colours) and asserting they appear only under `src/TrainingLoadAnalyzer.Web/Theme/` (FR-005, SC-009). Expect it to fail initially against `MetricsChartView.razor.css`
- [X] T011 **GREEN** — remove the colour declarations from `src/TrainingLoadAnalyzer.Web/Components/Dashboard/MetricsChartView.razor.css` until T010 passes
- [X] T012 Convert `src/TrainingLoadAnalyzer.Web/Components/Layout/MainLayout.razor` to `<MudThemeProvider Theme="TrainingLoadTheme.Instance" @bind-IsDarkMode="isDarkMode" />` + `<MudLayout>` + `<MudAppBar>` + `<MudMainContent>`. Register **only** `MudThemeProvider` — no popover, dialog or snackbar providers, per Principle III and [contracts/markup-contract.md](contracts/markup-contract.md)
- [X] T013 In the same file, render the app bar title as `<MudText Typo="Typo.h6" HtmlTag="span">Training Load</MudText>` — a `span`, **not** a heading. `Routes.razor` uses `<FocusOnNavigate Selector="h1" />`, so the page `<h1>` must stay unique ([research.md](research.md) R10)
- [X] T014 In the same file, cascade `isDarkMode` so `MetricsChartView` can select its palette (`<CascadingValue Value="isDarkMode" Name="IsDarkMode">`). Nothing else consumes it
- [X] T015 [P] Add the reduced-motion escape hatch to `src/TrainingLoadAnalyzer.Web/wwwroot/app.css`: `@media (prefers-reduced-motion: reduce)` reducing transition and animation durations to near-zero (FR-024). MudBlazor animates ripples and does not suppress them itself
- [X] T016 [P] Restyle `#blazor-error-ui` in `src/TrainingLoadAnalyzer.Web/Components/Layout/MainLayout.razor.css` onto theme colours and remove `color-scheme: light only`, which currently pins it to a light-only `lightyellow` (FR-015, FR-028)

**Checkpoint**: the theme exists, is proven to meet contrast in both palettes, and is the only place colour lives.

---

## Phase 3: User Story 2 - Nothing the athlete relied on is lost (Priority: P1) 🛡️ SAFETY NET

**Goal**: Freeze every user-visible string *before* any component is converted, so each later conversion is checked against it.

**Why this phase is first**: US1 and US2 are both P1. These tests are written against the **current** implementation, where they pass immediately — that is the point. They are characterization tests, and their value is entirely in existing before the markup changes underneath them. Running them after the conversion would prove nothing about what was lost.

**Independent Test**: run the new tests against unconverted `main` — they pass. Run them after every conversion task — they must still pass.

### Tests for User Story 2

- [X] T017 [P] [US2] **RED-by-construction** — write `tests/TrainingLoadAnalyzer.Web.Tests/InformationPreservationTests.cs` with a text-extraction helper (render, strip tags, normalise whitespace) and a populated-dashboard case asserting presence of: each metric figure to one decimal, `—` for absent values, `Fitness`, `Fatigue`, `Form`, `This week`, `Recent activities`. Verify it passes on current `main` before proceeding
- [X] T018 [P] [US2] Add the qualifier case to the same file: a metric with under 42 days of history renders the literal text `still settling`; a `Mixed` basis renders `partly estimated`; an `Estimated` basis renders `estimated` (FR-012, US2 sc2). **These three strings are the feature's likeliest silent regression** — with the chart's dash patterns gone under Amendment 1, they are the last non-colour guarantees left ([research.md](research.md) R16)
- [X] T019 [P] [US2] Add the provenance case: every recent-activity row renders the literal word `measured` or `estimated` as text, not only as chip styling (FR-012, FR-022, US2 sc3)
- [X] T020 [P] [US2] Add the sync-state cases: the button reads `Sync Activities` at rest and `Syncing…` while running; the last-checked timestamp still renders in `yyyy-MM-dd HH:mm`; each `SyncMessage` string still appears for its status (FR-002, US2 sc4)
- [X] T021 [P] [US2] Add the chart-surround cases: the legend reads `Fitness`, `Fatigue`, `Form`; the date range renders as two `yyyy-MM-dd` values; under 30 days the exact string `Not enough data to show trends (30+ days required)` appears (FR-002, FR-013a, US2 sc5)

**Checkpoint**: the net is in place and green. Every conversion below is now checked against it.

---

## Phase 4: User Story 1 - The dashboard looks like a designed product (Priority: P1) 🎯 MVP

**Goal**: Convert the populated dashboard to MudBlazor components on the theme from Phase 2.

**Independent Test**: load the dashboard with populated history; every region is a bounded surface with consistent spacing and a clear type hierarchy, and the Phase 3 tests still pass.

> **No RED step for most of this phase.** These are component swaps whose correctness is "the same text, now inside a Material surface". The Phase 3 tests are the RED that matters; a test asserting `MudPaper` renders a `div` would be testing the library. Where a conversion *does* have assertable behaviour, it is called out.

- [X] T022 [US1] Convert `src/TrainingLoadAnalyzer.Web/Components/Pages/Dashboard.razor` to `<MudContainer MaxWidth="MaxWidth.Large">` with the three metric tiles in a `<MudGrid>` / `<MudItem xs="12" sm="4">` (FR-016, FR-018). Keep the `<h1>` exactly as it is
- [X] T023 [P] [US1] Convert `src/TrainingLoadAnalyzer.Web/Components/Dashboard/MetricTile.razor` to `MudPaper` + `MudText Typo="Typo.caption"` for the label + `MudText Typo="Typo.h3"` for the value + `MudChip` for each qualifier. **The qualifier text goes inside the chip** — never replaced by an icon or a colour (FR-012). T018 guards this
- [X] T024 [P] [US1] Convert `src/TrainingLoadAnalyzer.Web/Components/Dashboard/WeeklyLoadPanel.razor` to `MudPaper` + `Typo.h3` for the weekly total + `MudChip` for the judgement, preserving the exact wording `Significant increase` / `Significant decrease` / `Steady` / `Week in progress`
- [X] T025 [US1] Convert `src/TrainingLoadAnalyzer.Web/Components/Dashboard/SyncPanel.razor` to `MudButton Class="sync"` and `MudButton Href="/connect" Class="connect"`. The rendered element must remain a `<button>` carrying class `sync`, and `Disabled` must stay bound to `Status.IsRunning` — both are asserted by existing tests
- [X] T026 [US1] **AMENDMENT 1** — convert `src/TrainingLoadAnalyzer.Web/Components/Dashboard/RecentActivityList.razor` from `<ul>`/`<li>` to `MudList` / `MudListItem`, keeping day, type, duration, load and the provenance chip in consistent positions (FR-011)
- [X] T027 [US1] **AMENDMENT 1 — rewrite, do not delete** — `Assert.Empty(list.FindAll("li"))` in `tests/TrainingLoadAnalyzer.Web.Tests/DashboardComponentTests.cs` would pass *vacuously* against `MudList`'s `<div>`s. Replace it with an assertion on the empty-state message `Nothing recorded yet.`, which is the behaviour it was reaching for. Record the rewrite in the completion review
- [X] T028 [US1] **AMENDMENT 1** — convert `src/TrainingLoadAnalyzer.Web/Components/Dashboard/MetricsChartView.razor` to `<MudChart ChartType="ChartType.Line">`, supplying the three series **named** `Fitness`, `Fatigue`, `Form` (the names drive the legend, which is now the only way to identify a series — FR-013a) and `ChartOptions.ChartPalette` from `ChartPalette` selected by the cascaded `IsDarkMode`. Keep the date-range line and the `Not enough data to show trends (30+ days required)` message, word for word, as the component's own markup
- [X] T029 [US1] **AMENDMENT 1 — record each removal** — delete `src/TrainingLoadAnalyzer.Web/Features/Dashboard/MetricsChart.cs` and `tests/TrainingLoadAnalyzer.Web.Tests/MetricsChartTests.cs` (4 tests), and remove the three `FindAll("polyline")` assertions from `tests/TrainingLoadAnalyzer.Web.Tests/DashboardComponentTests.cs`. Amended SC-002 permits removal **only** when each removal is named in the completion review (T045)
- [X] T030 [US1] **Do not add a `stroke-dasharray` override.** A CSS rule on `.mud-chart-line path.mud-chart-serie:nth-of-type(n)` would restore the lost dash patterns; it was offered and deliberately declined ([research.md](research.md) R5). This task is a no-op checkpoint: confirm no such rule was added, and that the loss is recorded in Amendment 1 rather than quietly patched
- [X] T031 [US1] Run `dotnet test --project tests/TrainingLoadAnalyzer.Web.Tests/TrainingLoadAnalyzer.Web.Tests.csproj`. All Phase 3 tests and all surviving pre-existing tests must pass

**Checkpoint**: the populated dashboard is fully Material, and nothing the athlete reads has changed.

---

## Phase 5: User Story 3 - Every state looks designed (Priority: P2)

**Goal**: The states an athlete hits first and worst.

**Independent Test**: force each state and confirm a designed surface with heading, explanation and — where one exists — a prominent action.

- [X] T032 [P] [US3] Convert the empty, loading and data-unavailable states in `src/TrainingLoadAnalyzer.Web/Components/Pages/Dashboard.razor` to `MudPaper` surfaces, keeping the existing headings and body text word for word (FR-014). The unconnected state's `Connect Strava` becomes a prominent `MudButton` (FR-010)
- [X] T033 [P] [US3] Convert `src/TrainingLoadAnalyzer.Web/Components/Pages/NotFound.razor` to `MudContainer` + `MudPaper` (FR-015)
- [X] T034 [P] [US3] Convert `src/TrainingLoadAnalyzer.Web/Components/Pages/Error.razor` to `MudContainer` + `MudPaper`, replacing the inert Bootstrap `class="text-danger"` with `Color="Color.Error"` — Bootstrap is not present in this project, so the class currently does nothing
- [X] T035 [US3] Extend `tests/TrainingLoadAnalyzer.Web.Tests/InformationPreservationTests.cs` with the empty-unconnected, empty-connected and data-unavailable cases, asserting each state's heading and explanatory text survive verbatim

**Checkpoint**: no surface is left in browser-default styling (SC-003).

---

## Phase 6: User Story 4 - Usable on the device the athlete has (Priority: P2)

**Goal**: Reflow correctly from 320px to 2560px.

**Independent Test**: render the populated dashboard at each width; no horizontal scrolling, no clipped or overlapping content.

- [X] T036 [US4] Verify the `MudGrid` breakpoints from T022 collapse the three metric cards to one column at `xs`. Adjust `MudItem` breakpoints if not
- [X] T037 [US4] Check `MudContainer`'s default gutters at exactly **320px** — below the `xs` breakpoint most examples assume — and confirm no horizontal page scrollbar appears (FR-017, SC-004). This is the width MudBlazor is least likely to have been tuned for
- [X] T038 [US4] Verify the `MudChart` scales to its container across widths and stays legible (US4 sc3). It is now the library's chart, so its responsive behaviour is the library's, not ours
- [X] T039 [US4] Confirm `MudButton`'s rendered size meets the 48×48 minimum for the sync and connect actions; add a minimum-size rule if not (FR-021). Verify, do not assume

**Checkpoint**: SC-004 holds across the full width range.

---

## Phase 7: User Story 5 - Accessible to everyone who used it before (Priority: P3)

**Goal**: Preserve what feature 006 built in, minus what Amendment 1 knowingly gave up.

**Independent Test**: keyboard traversal, colour-blind emulation, heading inspection.

- [X] T040 [US5] Tab through the dashboard and confirm every interactive element shows a visible focus indicator (FR-020). MudBlazor provides these by default — confirm the theme's `Primary` has not made any of them invisible against its surface
- [X] T041 [US5] Confirm exactly one `<h1>` on the dashboard and that `FocusOnNavigate` still finds it after the T012–T013 layout change (FR-023). The app bar title must be a `span`
- [X] T042 [US5] Emulate achromatopsia in browser devtools. Every distinction **outside the chart plot** must remain readable — provenance by its word, qualifiers by their text, trend by its judgement (FR-022 as amended, SC-007). Inside the plot, confirm the accepted regression is what it is: series distinguishable only by legend order and hover label
- [X] T043 [US5] Verify `TextDisabled` against the disabled sync button in both schemes. MudBlazor *derives* this colour rather than taking it from the theme, so T006's assertion is the check — confirm it is exercising the value MudBlazor actually renders, not a default the theme never supplies

**Checkpoint**: accessibility is at feature 006's level, minus exactly what Amendment 1 records.

---

## Phase 8: Polish & Cross-Cutting Concerns

- [X] T044 Walk the 8 × 2 surface/scheme matrix in [data-model.md](data-model.md) — every surface in both light and dark. Toggle the OS appearance with the page open and confirm it follows without a reload (FR-026, SC-011)
- [X] T045 **Write the completion review** at `specs/007-material-design-ui/compliance-review.md`, following features 001–006. It MUST name every test removed in T029 and the rewrite in T027 — amended SC-002 permits removal only when recorded. It MUST also assess the feature against Principles I, II and III, and against the three deviations in the plan's Complexity Tracking table
- [ ] T046 Judge the prerender flash: load the app first-time with the OS in dark mode and decide whether the light flash before interop resolves is tolerable ([research.md](research.md) R14). It is an accepted trade-off, not a bug — but it is worth a deliberate look now that it is real
- [X] T047 [P] Update `README.md` if it describes the interface or the dependency set; MudBlazor is now a runtime dependency
- [X] T048 Run the full solution suite: `dotnet test`. Confirm Domain and Infrastructure are untouched and still green
- [ ] T049 Work the manual checklist in [quickstart.md](quickstart.md) end to end

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: no dependencies. **T002 is a hard gate** — if the spike fails, everything downstream is invalid
- **Phase 2 (Foundational)**: depends on Phase 1. Blocks all user stories
- **Phase 3 (US2 safety net)**: depends on Phase 1 only — the tests are written against unconverted code. Should be complete before Phase 4 begins
- **Phase 4 (US1)**: depends on Phase 2 and Phase 3
- **Phases 5–7 (US3, US4, US5)**: depend on Phase 4, since they verify or extend converted components
- **Phase 8 (Polish)**: depends on everything

### Story Dependencies

- **US2 (P1)**: independent — deliberately first, as the net for everything after
- **US1 (P1)**: the MVP. Needs US2's tests in place to be safe, and the theme from Phase 2
- **US3 (P2)**: independent of US4 and US5; touches different states of the same page as US1
- **US4 (P2)**: verifies US1's layout; cannot precede it
- **US5 (P3)**: verifies US1's and US3's output

### Parallel Opportunities

- T004, T009, T015, T016 are independent files within Phase 2
- **All of Phase 3 (T017–T021) is parallel** — one new test file, separate cases, no production code
- T023, T024 are different component files; T026 and T028 touch different components and can proceed in parallel once T022 lands
- T032, T033, T034 are three separate pages

### Sequential by necessity

- T004 → T005 → (T006, T007) → T008: the RED/GREEN chain for the theme
- T010 → T011: the scan must fail before the colours move
- T022 before T023–T028: the container and grid come before what sits in them
- T026 → T027 and T028 → T029: convert, then deal with the tests that conversion invalidates

---

## Parallel Example: Phase 3 (the safety net)

```bash
# All five write separate cases in one new file; no production code moves.
Task: "T017 Text-extraction helper and populated-dashboard case"
Task: "T018 Qualifier text cases — still settling / partly estimated / estimated"
Task: "T019 Provenance cases — measured / estimated"
Task: "T020 Sync state and timestamp cases"
Task: "T021 Chart-surround cases — legend, date range, insufficient-data message"
```

---

## Implementation Strategy

### The gate comes first

T001–T002 are not ceremony. The plan rests on one unverified assumption — that 99 bUnit tests survive MudBlazor's interop requirements. Prove it against one converted element before converting eight components.

### MVP

Phases 1 → 2 → 3 → 4. That delivers a fully Material populated dashboard with a characterization net proving nothing was lost. Stop and validate there; US3–US5 refine states, layout and accessibility on top.

### What to watch

1. **The qualifier chips** (T023). With Amendment 1 removing the chart's dash patterns, `still settling` / `estimated` / `measured` are the last non-colour guarantees in the interface. T018 and T019 exist for this.
2. **Amendment 1 read too broadly** ([research.md](research.md) R16). It concedes four specific things about the chart and the list. It concedes nothing about chips, provenance, wording or numbers.
3. **Recorded removals** (T029 → T045). Amended SC-002 permits deleting the chart tests; it does not permit doing it quietly.

---

## Notes

- `[P]` = different files, no dependencies
- Commit after each task or logical group
- `src/TrainingLoadAnalyzer.Domain/` and `src/TrainingLoadAnalyzer.Infrastructure/` must not be touched — if a task seems to require it, the task is wrong
- No package beyond MudBlazor 9.10.0 is added
- No in-application theme toggle (FR-027), however trivial MudBlazor makes one
