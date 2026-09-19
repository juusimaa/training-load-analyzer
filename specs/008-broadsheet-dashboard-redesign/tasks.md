---

description: "Task list for 008-broadsheet-dashboard-redesign"
---

# Tasks: Broadsheet Dashboard Redesign

**Input**: Design documents from `/specs/008-broadsheet-dashboard-redesign/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/dashboard-ui.md](./contracts/dashboard-ui.md),
[quickstart.md](./quickstart.md)

**Tests**: Mandatory. Constitution Principle I (Strict TDD) is non-negotiable, and the Development
Workflow requires tasks expressed as RED → GREEN → REFACTOR → VERIFY steps tied to concrete
given/when/then scenarios. Every implementation task below is preceded by the failing test that
justifies it.

**Organization**: Grouped by user story. Note the honest dependency recorded under
[Dependencies](#dependencies--execution-order): US2 is a *constraint that holds throughout* rather
than a separable increment, and the MudBlazor removal cannot complete until US1 and US3 have both
rewritten their `.razor` files.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1–US5)
- **RED**/**GREEN**/**REFACTOR**/**VERIFY**: the TDD step this task performs

## Path Conventions

- Web project: `src/TrainingLoadAnalyzer.Web/`
- Web tests: `tests/TrainingLoadAnalyzer.Web.Tests/`
- Nothing in `TrainingLoadAnalyzer.Domain` or `TrainingLoadAnalyzer.Infrastructure` is touched by
  this feature. A task that finds itself editing either is a Principle II violation — stop.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Establish the vendored design tokens and make the contrast audit executable. This is
where research R4's measured table becomes a suite of RED tests rather than a review note.

- [ ] T001 RED: Repoint `tests/TrainingLoadAnalyzer.Web.Tests/Theme/PaletteSlotTests.cs` from `MudColor`/`Palette` reflection to parsing CSS custom properties out of `src/TrainingLoadAnalyzer.Web/wwwroot/Theme/broadsheet.css`; assert every token group in data-model.md §2 (ground, accents, neutral ramp 100–900, both accent ramps 100–900, type, spacing, radius, elevation) exists under `:root`. Fails: the file does not exist yet.
- [ ] T002 GREEN: Vendor the stylesheet subset from `docs/ui/styles.css` into `src/TrainingLoadAnalyzer.Web/wwwroot/Theme/broadsheet.css` — token block plus the base, `.btn`, `.tag`, `.table` and `.seg` rules only. Omit the print-treatment section (`.halftone`, `.cmyk`, `.cmyk-num`, `.cmyk-head`, `--color-process-yellow`) and the unused `.nav`, `.card`, `.dialog`, `.input` classes per R2. The `Theme` path segment is spelled exactly so — `ColourDisciplineTests` does an ordinal `.Contains("Theme")`.
- [ ] T003 RED: Repoint `tests/TrainingLoadAnalyzer.Web.Tests/Theme/PaletteContrastTests.cs` at the parsed tokens and assert every pairing in data-model.md §2 against its minimum, using the existing `Theme/ContrastRatio.cs` unchanged. Encode the load-bar exemption (Amendment 1(b)) as an explicitly named exclusion, not a missing assertion. Fails on the eight shortfalls measured in R4.
- [ ] T004 GREEN: Apply the R4 remedies in `broadsheet.css` — `.btn-primary` background and link/`.btn-ghost`/`.tag-outline` text to `--color-accent-700`; `.table th` and `.text-muted`/`figcaption` from 55–60% to 70% ink. Chart-specific values (axis, Form legend label, zero rule) follow in T028.
- [ ] T005 RED: Extend `PaletteSlotTests` and `PaletteContrastTests` to run the same token-completeness and contrast assertions against the `@media (prefers-color-scheme: dark)` block. Fails: the vendored sheet ships no dark scheme at all (R3).
- [ ] T006 GREEN: Author the dark token set in `broadsheet.css` as a `@media (prefers-color-scheme: dark)` block redefining the same custom properties. Per the sheet's own elevation comment, separation in dark comes from a hairline edge and ambient darkness rather than a stronger shadow.
- [ ] T007 VERIFY: Run `dotnet test tests/TrainingLoadAnalyzer.Web.Tests --filter FullyQualifiedName~Theme`. `ColourDisciplineTests` must pass **unmodified** — it is the check that the vendored sheet landed under a `Theme` segment.

**Checkpoint**: Tokens exist in both schemes and every contrast pairing is machine-checked. No
component has changed yet.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The read-model members and chart geometry every story renders, plus the shared test
base. **No user story work can begin until this phase is complete.**

**⚠️ CRITICAL**: T008–T019 block Phases 3–7.

### Read model (Amendment 1(a) additions)

- [ ] T008 [P] RED: In `tests/TrainingLoadAnalyzer.Web.Tests/` add a `DashboardViewBuilder` test — given an athlete configured with a maximum heart rate of 190, when the view is built, then `MaximumHeartRate` is 190. Fails: the member does not exist.
- [ ] T009 [P] RED: Add a `DashboardViewBuilder` test — given `AsOf` of 2026-09-18, when the view is built, then `IsoWeek` reads `2026-W38`; and given a date in the first days of January that ISO-8601 assigns to the prior year's final week, then the designation reflects that. Fails: the member does not exist.
- [ ] T010 GREEN: Add `MaximumHeartRate` (`int`) and `IsoWeek` (`string`) to `src/TrainingLoadAnalyzer.Web/Features/Dashboard/DashboardView.cs` and populate them in `DashboardViewBuilder.cs`. `DashboardReader.cs` already reads `AthleteSettings.MaximumHeartRate` for load estimation — carry the value through rather than reading configuration a second time. `IsoWeek` is derived from `AsOf`, never stored.

### Chart geometry (R5)

- [ ] T011 GREEN: Recover `src/TrainingLoadAnalyzer.Web/Features/Dashboard/MetricsChart.cs` and `tests/TrainingLoadAnalyzer.Web.Tests/MetricsChartTests.cs` verbatim from commit `b1e8726^` (`git show b1e8726^:<path>`). Do not retype them — the point is to recover the invariant-culture coordinate handling and its tests intact.
- [ ] T012 VERIFY: Run `dotnet test --filter FullyQualifiedName~MetricsChart`. All recovered tests must pass before anything is added to the module. If the culture test does not fail under a Finnish culture when invariant formatting is removed, the recovery is incomplete.
- [ ] T013 [P] RED: Add a `MetricsChartTests` case — given a 30-day metrics series with varying daily loads, when `LoadBars` is called, then one `<rect>` geometry per non-zero day is returned, scaled on its own axis independent of the metric band, with a minimum bar width that keeps a 180-day window visible. Given a series of all-zero loads, then no rects are returned.
- [ ] T014 [P] RED: Add a case — given a metrics series whose Form range spans zero, when `ZeroRule` is called, then the returned y-position sits on the **shared metric scale** so it aligns with the Form polyline; and given a band that excludes zero, then no rule is returned.
- [ ] T015 [P] RED: Add a case — given a 180-day series and a tick count of 6, when `AxisTicks` is called, then six date labels are returned, both ends of the window are labelled, and every label is formatted with `CultureInfo.InvariantCulture`.
- [ ] T016 GREEN: Implement `LoadBars`, `ZeroRule` and `AxisTicks` in `MetricsChart.cs`. Every coordinate goes through the existing invariant-culture formatting path — this module exists because `points="0,45,3 1,5,12,25"` is valid-looking markup, silently wrong geometry, no exception, and correct on en-US.
- [ ] T017 REFACTOR: Confirm `ChartSeries.CssClass` carries `series-fitness`/`series-fatigue`/`series-form`, and that bars and the rule are emitted with `load-bar`/`zero-rule`. No colour literal enters `MetricsChart.cs` — `ColourDisciplineTests` scans `.cs` files' siblings but the real reason is that an SVG presentation attribute cannot respond to the dark scheme (R5).

### Test infrastructure (R11)

- [ ] T018 REFACTOR: Extract the ~15-line `SeedAndRegister`/`RenderDashboard` DI block duplicated across `DashboardComponentTests.cs` and `InformationPreservationTests.cs` into `tests/TrainingLoadAnalyzer.Web.Tests/DashboardRenderContext.cs`, a `BunitContext` subclass. Justified by two existing duplications plus the new tests in Phases 3–7 — not speculative (Principle III).
- [ ] T019 VERIFY: Run the full Web test suite. Both existing bUnit classes pass through the shared base with **zero assertion changes**.

**Checkpoint**: Read model, chart geometry and test scaffolding ready. User stories can begin.

---

## Phase 3: User Story 1 — The dashboard reads as a single considered page (Priority: P1) 🎯 MVP

**Goal**: Replace the card grid with the rail-and-column newsprint layout — every control in the
rail, four display figures in one row, a hand-drawn chart, a tagged sessions table.

**Independent Test**: Load the populated dashboard with the fixture history and confirm the rail
holds exactly the as-of/window/Strava/max-HR controls and nothing else, the content column holds
the metric row, chart and table, and no region renders as a boxed or shadowed card.

### Shell and rail

- [ ] T020 [US1] RED: In `DashboardComponentTests.cs`, add — given a populated view, when the dashboard renders, then the rail contains the as-of date, the ISO week designation, the window control, the Strava state, `button.sync` and the maximum heart rate with its unit, and **no interactive control appears outside the rail** (FR-003). Fails against the current card layout.
- [ ] T021 [US1] GREEN: Rewrite `src/TrainingLoadAnalyzer.Web/Components/Layout/MainLayout.razor` and `.razor.css`, replacing `MudThemeProvider`/`MudLayout`/`MudAppBar`/`MudText`/`MudMainContent` with a plain shell. Remove the `isDarkMode` field, the `GetSystemDarkModeAsync()` first-render read and the `<CascadingValue Name="IsDarkMode">` — the media query from T006 now carries the scheme (R3). Restyle the `#blazor-error-ui` notice off `--mud-palette-error*` onto Broadsheet tokens.
- [ ] T022 [US1] GREEN: Rewrite `src/TrainingLoadAnalyzer.Web/Components/Pages/Dashboard.razor` and `.razor.css` as the rail-plus-content grid (250px sticky rail, 60px gap, page capped at 1240px). The masthead is a styled `<div>`, **not** an `<h1>` — `FocusOnNavigate Selector="h1"` and `InformationPreservationTests` both require exactly one `<h1>` per page (R8).
- [ ] T023 [US1] GREEN: Move `src/TrainingLoadAnalyzer.Web/Components/Dashboard/SyncPanel.razor` into the rail and rewrite it on plain markup. Keep `button.sync` as a real `<button>`, `disabled` while `IsRunning`, and render `SyncMessage.For(status)` verbatim — its strings are pinned by `SyncMessageTests` (R8, FR-002).

### Metric row

- [ ] T024 [US1] RED: Add — given a populated view, when the dashboard renders, then Fitness, Fatigue, Form and week load appear as four peers in one row, each with `.tile-value`, Fitness and Fatigue carrying their own series colour class and Form and week load the ordinary ink; and given a view whose `Current` is null, then each figure renders an em dash and **still occupies its position** so the row stays aligned (FR-008).
- [ ] T025 [US1] GREEN: Merge `MetricTile.razor` and `WeeklyLoadPanel.razor` into `src/TrainingLoadAnalyzer.Web/Components/Dashboard/MetricRow.razor` and `.razor.css`, rendering the four figures at display size with their labels above and the week trend caption beneath the week figure. Preserve the existing `Display.*` formatting and the trend classification wording exactly.

### Chart

- [ ] T026 [US1] RED: Add — given a populated view, when the chart renders, then an `<svg>` contains load bars behind three polylines, a zero rule and a legend naming Fitness, Fatigue and Form, and the Form line carries a dash pattern (SC-007 as amended); and given fewer than 30 days of history, then the insufficient-history message renders and **no `<svg>` is emitted at all** (R8, `DashboardComponentTests:294`).
- [ ] T027 [US1] GREEN: Rewrite `src/TrainingLoadAnalyzer.Web/Components/Dashboard/MetricsChartView.razor` from `MudChart` to hand-drawn SVG driven by `MetricsChart.Plot`/`LoadBars`/`ZeroRule`/`AxisTicks`. Stroke and fill come from CSS classes in the scoped stylesheet, never from SVG presentation attributes.
- [ ] T028 [US1] GREEN: In `MetricsChartView.razor.css`, bind the series, bar, rule and axis classes to tokens, applying the remaining R4 remedies: date axis and the "Form" legend label to `--color-neutral-700` (5.83:1), zero rule to `--color-neutral-600` (3.85:1, clearing the 3:1 bar). Load bars keep `--color-neutral-300` under the Amendment 1(b) exemption.

### Recent sessions

- [ ] T029 [US1] RED: Add — given a view with seven recent activities, when the list renders, then a `table.table` contains seven `.recent-row` rows with day, type, moving time, a basis `<span class="tag">` and a right-aligned load; and given an activity whose load was estimated, then the tag reads "estimated" as text, not as a colour difference alone (FR-002, FR-018).
- [ ] T030 [US1] GREEN: Rewrite `src/TrainingLoadAnalyzer.Web/Components/Dashboard/RecentActivityList.razor` and `.razor.css` from `MudList`/`MudListItem`/`MudChip` to a plain `<table>`, keeping the `.recent-row` hook.

### Window selector (Amendment 1(a), new behaviour)

- [ ] T031 [US1] RED: Add — given a populated view with 180 days of history, when the athlete selects the 90-day window, then the chart, its heading ("… last 90 days") and its date axis all reflect 90 days without a full page reload; and given 200 days of stored history with a 30-day window selected, then `HasEnoughHistoryForChart` still reports true, because it describes stored history rather than the chosen window (R6).
- [ ] T032 [US1] GREEN: Add the window state (30/90/180, defaulting to 180) to `Dashboard.razor` and render the control in the rail using the `.seg`/`.seg-opt` classes. Apply it by taking the trailing slice of `DashboardView.Metrics` before calling into `MetricsChart` — no new query and no storage round-trip.
- [ ] T033 [US1] GREEN: Give `.seg-opt` a 48px minimum height in `Dashboard.razor.css`, overriding the reference page's 44px, which would miss FR-017's 48×48 requirement (R9).

- [ ] T034 [US1] VERIFY: Run the full Web suite plus a manual pass of quickstart.md's "Layout and figures (US1)" checks against `docs/ui/index.html` side by side.

**Checkpoint**: The populated dashboard matches the reference design and is independently
demonstrable. This is the MVP.

---

## Phase 4: User Story 2 — Nothing the athlete relied on is lost (Priority: P1)

**Goal**: Prove the redesign is information-preserving.

**Independent Test**: Render the dashboard before and after for the same stored history and
connection state and confirm the set of displayed text, figures and destinations is unchanged.

> **This story is a constraint, not a later increment.** `InformationPreservationTests` must be
> green at the end of *every* task in Phase 3, not repaired here. The tasks below strengthen the
> net and verify it deliberately.

- [ ] T035 [US2] VERIFY: Run `dotnet test --filter FullyQualifiedName~InformationPreservation` with its assertions **unchanged**. Any failure means the redesign lost something an athlete relied on — fix the markup, never the assertion. SC-002 permits rewriting only assertions bound to markup this feature deliberately replaces, and forbids weakening, vacating or silently removing any.
- [ ] T036 [P] [US2] RED: Add a characterization test — given a populated view, when the dashboard renders, then the two Amendment 1(a) additions (maximum heart rate, ISO week designation) are **additional to**, not in place of, every string the previous interface showed.
- [ ] T037 [P] [US2] RED: Add — given each `?connect=declined`, `?connect=scope` and `?connect=mismatch` query outcome, when the dashboard renders, then the existing message for that outcome still appears with its current wording. `/connect` is a redirect-only endpoint with no page of its own, so the dashboard is the only place these surface.
- [ ] T038 [P] [US2] RED: Add — given a metric backed by fewer than the required days of history, when the dashboard renders, then the "still settling" qualifier is present as readable text rather than a visual cue alone (US2 scenario 2).
- [ ] T039 [US2] GREEN: Make T036–T038 pass by correcting the markup produced in Phase 3. No change to `Display.*`, `SyncMessage` or any computation is permitted here — if one seems necessary, FR-001 has been violated upstream.
- [ ] T040 [US2] VERIFY: Perform the quickstart.md before/after comparison: `git stash` the change, load the fixture history, screenshot the dashboard, restore, and compare every figure, qualifier, message, timestamp and link for identical wording and formatting (SC-001).

**Checkpoint**: The redesign is provably information-preserving.

---

## Phase 5: User Story 3 — Every existing state looks like part of the same design (Priority: P2)

**Goal**: Carry the rail-and-column visual language into the six states the reference design does
not depict, plus the not-found and error surfaces.

**Independent Test**: Load the application with no activities (connected and not), an unreadable
store, a revoked credential, a rate-limited sync, and mid-load, and confirm each uses the new type
scale, spacing and layout rather than the previous card styling.

- [ ] T041 [P] [US3] RED: In `DashboardComponentTests.cs`, add — given no activities and no connected account, when the dashboard renders, then the content column explains that training must be imported, offers a prominent `href="/connect"` action, and contains **no** `.tile-value` (`DashboardComponentTests:200`).
- [ ] T042 [P] [US3] RED: Add — given no activities but a connected account, then the content column explains that a sync will bring training in, offers **no** redundant connect action, and the rail still offers manual sync.
- [ ] T043 [P] [US3] RED: Add — given `IsUnavailable`, then a distinct, visibly marked notice renders in place of the figures, chart and table; and given `view is null`, then a loading indication renders in their place.
- [ ] T044 [P] [US3] RED: Add — given a `SyncStatus` whose outcome is `ReconnectionRequired`, then the rail offers a reconnect action and shows `SyncMessage`'s existing "Strava connection required." wording; and given `RateLimited` with a known `RetryAfterLocal`, then the rail states the retry time in its existing format.
- [ ] T045 [US3] GREEN: Restyle the five dashboard state branches in `Dashboard.razor` onto the Broadsheet visual language, satisfying T041–T044. Each keeps its current wording verbatim (FR-002, FR-009).
- [ ] T046 [P] [US3] GREEN: Rewrite `src/TrainingLoadAnalyzer.Web/Components/Pages/NotFound.razor` and `Error.razor` from `MudText`/`MudPaper` onto plain markup and Broadsheet tokens (FR-010).
- [ ] T047 [US3] RED: Write the **first test** for `src/TrainingLoadAnalyzer.Web/Components/Layout/ReconnectModal.razor` — given the reconnect modal is shown, when it renders, then its heading, explanatory text and reconnect action are present. It has no coverage today and is the heaviest consumer of the old palette (7 `--mud-palette-*` references), so its restyling would otherwise be entirely unverified (plan.md Complexity Tracking).
- [ ] T048 [US3] GREEN: Repoint `ReconnectModal.razor.css`'s 7 `--mud-palette-*` references onto Broadsheet tokens and restyle the modal. The component is already plain `<dialog>` markup, so only its stylesheet and tokens change.
- [ ] T049 [US3] VERIFY: Walk every row of the [state contract](./contracts/dashboard-ui.md#2-state-contract) manually per quickstart.md, in both light and dark appearance.

**Checkpoint**: No surface remains in the previous card styling. **All `.razor` rewrites are now
complete — the MudBlazor removal in Phase 8 is unblocked.**

---

## Phase 6: User Story 4 — Usable on the device the athlete has (Priority: P2)

**Goal**: The rail and content column stack cleanly from 320px up, with the chart scaling and
targets tappable.

**Independent Test**: Render the populated dashboard and each US3 state at narrow, medium and wide
widths and confirm reflow without horizontal scrolling, clipping or overlap.

- [ ] T050 [US4] RED: In `DashboardComponentTests.cs`, add — given the dashboard markup, when it renders, then the stacking rules are expressed as a `max-width: 60rem` media query in `Dashboard.razor.css` that makes the rail static and reduces the metric row to two columns. (Markup-level assertion; true reflow is verified manually in T053.)
- [ ] T051 [US4] GREEN: Implement the responsive rules in `Dashboard.razor.css` — single stacked column below 60rem, rail `position: static`, metric row to two columns, content capped at a comfortable reading width above (FR-011, FR-013).
- [ ] T052 [US4] GREEN: Make the chart SVG scale to its container at every width while remaining legible (FR-014), and confirm `wwwroot/app.css`'s `.page` container no longer conflicts with the new grid.
- [ ] T053 [US4] VERIFY: Manually check 320px, 768px, 1440px, 2560px and 200% browser zoom for no horizontal scroll, no clipped or overlapping content, and degradation to the stacked arrangement at high zoom (edge case 7).

**Checkpoint**: Usable on a phone and a wide desktop.

---

## Phase 7: User Story 5 — Accessible to everyone who used it before (Priority: P3)

**Goal**: Preserve contrast, focus visibility, non-colour encoding and heading structure through
the visual replacement, in both schemes.

**Independent Test**: Inspect the redesigned dashboard for text contrast, visible keyboard focus,
heading structure and non-colour encoding in both light and dark appearance.

- [ ] T054 [US5] VERIFY: Re-run the Phase 1 token suite. Every pairing in data-model.md §2 passes in both schemes, with the load-bar exemption still the only exclusion and still named explicitly (FR-015, SC-005).
- [ ] T055 [P] [US5] RED: Add — given the dashboard markup, when it renders, then exactly one `<h1>` exists and each content region carries an `<h2>`, read in a sensible order (FR-019, R8).
- [ ] T056 [P] [US5] RED: Add — given the recent-sessions table, the week trend caption and the rail's connection state, when colour is disregarded, then each distinction survives in text or tag form (FR-018, SC-007).
- [ ] T057 [US5] GREEN: Correct any heading-structure or colour-encoding gaps T055–T056 expose.
- [ ] T058 [US5] GREEN: Confirm every interactive control meets 48×48 (FR-017) and that the vendored sheet's `:focus-visible { outline: 2px solid var(--color-accent) }` gives a visible ring on the window options, the sync button and every link (FR-016). Retain the existing global `prefers-reduced-motion` reset in `wwwroot/app.css`.
- [ ] T059 [US5] VERIFY: Manually tab the full page; toggle the OS appearance with the page open and confirm the scheme follows without a reload and without any displayed content changing (FR-021, SC-010); view the page in greyscale.

**Checkpoint**: Accessibility is at least as good as before the redesign, with dash patterns
restored.

---

## Phase 8: Polish & Cross-Cutting Concerns

### Remove MudBlazor (gated on Phases 3 and 5 — the last `.razor` rewrites)

- [ ] T060 GREEN: Remove `builder.Services.AddMudServices()` and `using MudBlazor.Services;` from `src/TrainingLoadAnalyzer.Web/Program.cs`, and `@using MudBlazor` from `src/TrainingLoadAnalyzer.Web/Components/_Imports.razor`.
- [ ] T061 GREEN: Remove the `MudBlazor.min.css` and `MudBlazor.min.js` `@Assets[...]` links from `src/TrainingLoadAnalyzer.Web/Components/App.razor`, keeping `broadsheet.css`, `app.css` and the scoped-CSS bundle.
- [ ] T062 GREEN: Remove the `MudBlazor` `PackageReference` from `src/TrainingLoadAnalyzer.Web/TrainingLoadAnalyzer.Web.csproj`.
- [ ] T063 [P] GREEN: Delete `src/TrainingLoadAnalyzer.Web/Theme/TrainingLoadTheme.cs` and `Theme/ChartPalette.cs`, and `tests/TrainingLoadAnalyzer.Web.Tests/Fakes/MudChartBounds.cs`. Remove the now-dead `.mud-button-root` rules from `src/TrainingLoadAnalyzer.Web/wwwroot/app.css`.
- [ ] T064 VERIFY: `dotnet build` and the full `dotnet test` both clean with no MudBlazor reference anywhere.

### Cleanup and compliance

- [ ] T065 REFACTOR: Narrow `@rendermode="InteractiveServer"` on `<Routes>` in `App.razor` now that `MudThemeProvider`'s `OnAfterRenderAsync` is no longer the reason it sits at the root. `Dashboard.razor` declares its own render mode, so the sync button and window selector keep working. Listed as its own task rather than bundled silently into T021 (R3); revert it if anything regresses.
- [ ] T066 [P] Write `scripts/compliance-008.sh` on the model of `scripts/compliance-006.sh`: assert `MudBlazor` appears nowhere in `src/` or `tests/` (no `PackageReference`, `@using`, `AddMudServices`, `mud-` class or `MudChartBounds`), that no `.razor` carries an SVG `stroke=`/`fill=` colour literal, and that `app.css` no longer carries the dead `.mud-button-root` rules.
- [ ] T067 [P] Update `docs/ui/README.md`'s "Not yet covered" section — it claims the empty, not-connected, revoked-credential and rate-limited states are unaddressed, which was already stale against the shipped application and is now doubly so.
- [ ] T068 VERIFY: Run the full quickstart.md validation, including the four items marked as settleable only by human review — whether the page reads as newsprint, the `ReconnectModal` in both schemes, dark-scheme legibility of the three series, and whether whitespace and hairline rules separate regions as convincingly in dark as in light.
- [ ] T069 Record the constitution compliance review required by the Development Workflow: Principle I (was this built test-first), Principle II (this feature should have touched neither `TrainingLoadAnalyzer.Domain` nor `TrainingLoadAnalyzer.Infrastructure` at all — confirm with `git diff --stat`), and Principle III (did the vendored subset stay a subset; did any speculative abstraction creep in). Include the human-review outcomes from T068 and note that feature 007's Amendment 1 concessions are reversed by this feature.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies — start immediately.
- **Phase 2 (Foundational)**: Depends on Phase 1. **Blocks Phases 3–7.**
- **Phase 3 (US1)**: Depends on Phase 2. The MVP.
- **Phase 4 (US2)**: Runs *alongside* Phase 3, not after it — see below.
- **Phase 5 (US3)**: Depends on Phase 2; shares files with Phase 3 (`Dashboard.razor`), so sequence it after Phase 3 rather than in parallel.
- **Phase 6 (US4)**, **Phase 7 (US5)**: Depend on Phases 3 and 5 having produced the markup they constrain.
- **Phase 8 (Polish)**: T060–T064 are gated on **both** Phase 3 and Phase 5 completing — MudBlazor cannot be removed while any `.razor` file still references it.

### The two honest deviations from story independence

1. **US2 is a constraint, not an increment.** "Nothing the athlete relied on is lost" cannot be
   implemented after the fact; `InformationPreservationTests` must be green at the end of every
   Phase 3 task. Phase 4 strengthens and verifies the net, it does not build the story.
2. **The MudBlazor removal spans US1 and US3.** Both rewrite `.razor` files that reference it, so
   the removal is deferred to Phase 8 rather than belonging to either story. Until then the
   dependency remains in the project and its stylesheet is still linked — which is harmless,
   because Broadsheet classes and MudBlazor classes do not collide.

### Within Each User Story

- The RED task always precedes its GREEN task. Verify the test fails, and fails for the stated
  reason, before implementing.
- Chart geometry (Phase 2) before the chart component (Phase 3).
- Read-model members (Phase 2) before the rail that displays them (Phase 3).

### Parallel Opportunities

- **Phase 2**: T008/T009 (read-model RED) run parallel to T013/T014/T015 (chart RED) — different files.
- **Phase 4**: T036, T037, T038 are independent characterization tests.
- **Phase 5**: T041–T044 are independent state tests; T046 touches different files from T045.
- **Phase 7**: T055 and T056 are independent.
- **Phase 8**: T063, T066, T067 are independent of each other.

---

## Parallel Example: Phase 2

```bash
# Read-model and chart RED tasks touch different files — run together:
Task: "T008 RED: MaximumHeartRate carried onto DashboardView"
Task: "T009 RED: IsoWeek derived from AsOf, including the January edge case"
Task: "T013 RED: LoadBars geometry on its own scale"
Task: "T014 RED: ZeroRule on the shared metric scale"
Task: "T015 RED: AxisTicks with invariant formatting"
```

---

## Implementation Strategy

### MVP First

1. Phase 1 (Setup) — tokens and the executable contrast audit.
2. Phase 2 (Foundational) — **blocks everything**.
3. Phase 3 (US1) + the Phase 4 constraint held continuously.
4. **STOP and VALIDATE**: the populated dashboard against `docs/ui/index.html`, with
   `InformationPreservationTests` green and its assertions untouched.

At this point the feature is demonstrable, though MudBlazor is still present and the non-populated
states still carry the old styling.

### Incremental Delivery

1. Setup + Foundational → tokens, chart geometry and read model ready.
2. US1 → the newsprint dashboard (MVP).
3. US3 → every state on the same visual system; MudBlazor removal unblocked.
4. US4 → phone and wide-desktop layouts.
5. US5 → accessibility verified in both schemes.
6. Polish → dependency removed, compliance recorded.

### Notes

- `[P]` tasks touch different files and have no dependency on incomplete work.
- Commit after each task or logical RED/GREEN pair.
- **Never repair a failing `InformationPreservationTests` assertion by editing the assertion.** It
  asserts on text extracted from stripped markup precisely so that a redesign cannot quietly drop a
  qualifier; a failure is a real regression.
- If a task requires editing `TrainingLoadAnalyzer.Domain` or `TrainingLoadAnalyzer.Infrastructure`,
  stop — this feature is presentation-only and that would be a Principle II violation.
