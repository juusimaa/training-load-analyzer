# Completion Review: Broadsheet Dashboard Redesign

**Feature**: 008-broadsheet-dashboard-redesign | **Date**: 2026-09-20 | **Reviewer**: AI agent, pending human sign-off

The constitution requires each feature's completion review to check explicitly against Principles I,
II and III, and SC-002 requires any weakened or removed assertion to be named here.

## Test accounting

| | Count |
|---|---|
| Web tests before the feature | 126 |
| **Removed** | **0** |
| Repointed onto replacement markup | 9 |
| Added by this feature | 80 |
| **Web tests now** | **206** |
| Whole solution | **516, all passing** |

Feature 007's review had to name seven tests it deleted, six of them the chart's. This feature
deletes none — it **restores** four of those seven. `MetricsChartTests` came back from `b1e8726^`
verbatim, including the culture test 007 recorded as "the most consequential of the seven losses".

### Every assertion that moved, named

No assertion was weakened, vacated or removed. Nine tests were repointed at components this feature
deliberately replaced, which SC-002 permits; in each case the assertion text is byte-identical and
only the component under test changed.

| Test | Was rendering | Now renders |
|---|---|---|
| `A_metric_figure_shows_its_label_and_its_value` | `MetricTile` | `MetricRow` |
| `A_metric_figure_with_nothing_to_show_is_a_dash` | `MetricTile` | `MetricRow` |
| `A_figure_that_has_not_warmed_up_says_so` | `MetricTile` | `MetricRow` |
| `The_weekly_panel_shows_the_change_and_withholds_a_judgement_mid_week` | `WeeklyLoadPanel` | `MetricRow` |
| `The_weekly_panel_shows_the_judgement_once_the_week_is_complete` | `WeeklyLoadPanel` | `MetricRow` |
| `With_no_previous_week_the_comparison_is_a_dash_and_the_total_remains` | `WeeklyLoadPanel` | `MetricRow` |
| `An_absent_figure_is_still_a_dash` | `MetricTile` | `MetricRow` |
| `A_qualifier_is_still_a_word_and_not_only_a_colour` (×3 cases) | `MetricTile` | `MetricRow` |
| `Two_qualifiers_at_once_both_survive` | `MetricTile` | `MetricRow` |

`git diff` over both bUnit test files returns **no changed line containing `Assert.`**. That was
checked rather than asserted.

### Tests whose subject moved but whose claim did not

`PaletteSlotTests` and `PaletteContrastTests` were repointed from MudBlazor's `Palette` type to the
custom properties and rules of `wwwroot/Theme/broadsheet.css` (R7). `ContrastRatio.cs` is unchanged.
`ColourDisciplineTests` is unchanged and passes, which is the check that the vendored sheet landed
under a `Theme` path segment.

The contrast suite is **stronger** than the one it replaces. It reads the value each rule actually
draws with, not the token the rule was supposed to use — the distinction that made R4's audit
possible, since the reference design's primary button clears no bar at all while the ramp it could
have used clears it comfortably. It also measures `:hover` and `:active`, which is how the
accessibility fix's own new defect was caught.

## Principle I — Strict TDD

**Held, with two sequencing departures recorded below rather than glossed.**

Every behaviour in this feature had a failing test before its implementation, and each RED was
confirmed to fail *for its stated reason* rather than merely to fail:

- T001 failed with "the vendored stylesheet is missing", not a parse error.
- T003 failed with the exact ratios R4 predicted — 3.65:1 for the button label and for links,
  4.23:1 for table headings — before any remedy was applied.
- T008–T015 failed to compile on members that did not exist.
- T047 failed on the seven `--mud-palette-*` references it exists to remove.

T012 was verified by mutation, not by reading: removing `CultureInfo.InvariantCulture` from
`MetricsChart.Coordinate` made `Coordinates_are_machine_readable_whatever_the_current_culture_is`
fail, and it was restored. A recovered test that cannot fail is not a recovered test.

**Departure 1 — the dashboard's state branches.** T022 rewrote `Dashboard.razor`, and a page
component cannot render half its states. The five state branches were therefore written on the
Broadsheet visual language in T022 rather than in T045, so T041–T044 verified them instead of
driving them. The alternative — leaving the page unable to render four of its five states between
two tasks — was not available.

**Departure 2 — the responsive rules.** Written with the rest of `Dashboard.razor.css` in T033, so
T050's assertions verified rules that already existed. One assertion in that task was RED and was
then **withdrawn rather than satisfied**: it demanded a `max-width` on `.content` which, inside a
page already capped at 1240px, could never bind. A declaration that can never take effect reads as
a guarantee and is not one, so the test was rewritten to pin the two caps that do the work.

## Principle II — Domain independence from Strava

**Held, and mechanically verified.**

```
git diff --stat main -- src/TrainingLoadAnalyzer.Domain          (empty)
git diff --stat main -- src/TrainingLoadAnalyzer.Infrastructure  (empty)
```

Both checks are in `scripts/compliance-008.sh` and both pass. This feature is presentation-only; it
touched neither library, added no Strava type anywhere, and changed no computation. `DashboardView`
gained three display members, all sourced from values the builder already held.

## Principle III — Simplicity before abstraction

**Held. The feature is strongly net-subtractive.**

Removed: the MudBlazor 9.10.0 dependency, `Theme/TrainingLoadTheme.cs`, `Theme/ChartPalette.cs`,
`Fakes/MudChartBounds.cs`, `MetricTile` and `WeeklyLoadPanel` with their stylesheets, two dead
`.mud-button-root` rules, two asset links, one service registration, the `isDarkMode` field, the
`GetSystemDarkModeAsync()` first-render read, the `IsDarkMode` cascade, and the root-level render
mode that existed only to serve the theme provider.

Added: one vendored stylesheet (a **subset** — the print-treatment machinery stayed out, and
`compliance-008.sh` asserts it), one revived chart module, one merged component, one dark token set.

Two abstractions were introduced, both test-side and both justified by duplication that already
existed:

- `DashboardRenderContext` (R11). Its return arrived immediately: removing the component library
  deleted three lines from one place instead of from every class that renders a page.
- `BroadsheetTokens` / `Stylesheet`. Two consumers from the start (the slot tests and the contrast
  tests), four by the end.

One speculative abstraction was **declined**: `LoadBar` carries four pre-formatted strings rather
than four doubles, which is not generality but the module's whole purpose — a `double` handed to
Razor is formatted with the current culture, and `width="3,56"` is the same silent geometry failure
arriving through a different door.

## Feature 007's Amendment 1 concessions, reversed

| 007 conceded | 008 |
|---|---|
| Per-series dash patterns lost — "a real accessibility regression" | **Restored.** The Form line carries `stroke-dasharray`, asserted by `The_form_line_is_distinguished_by_its_stroke_pattern_and_not_only_its_colour` |
| Culture-safe SVG coordinate handling lost to the library | **Restored**, verbatim from history, and mutation-checked |
| Exact line interpolation lost | **Restored.** The polyline passes through every plotted point |
| Four `MetricsChartTests` deleted | **Restored**, and extended with eleven more |
| Two chart-painting guards deleted with nothing replacing them | **Replaced.** Colour reaches the plot through classes the contrast suite measures in both schemes |

What 007 gained and 008 gives up: `MudChart`'s hover tooltips and clickable legend. Feature 006 had
deliberately chosen a legend *instead of* a tooltip, so this returns the chart to its originally
specified behaviour rather than removing something specified.

## NFR-001, measured rather than argued

The plan called the performance claim "an argument, not a measurement". Measured, on the same
machine, both builds serving the same store, interleaved and warm (15 requests each, median):

| | Pre-redesign (`967b760`) | Redesigned | Change |
|---|---|---|---|
| Server time to first byte for `/` | 113.9 ms (min 107.2, max 134.2) | 112.1 ms (min 107.6, max 120.9) | no regression |
| Bytes before first paint (document + render-blocking stylesheets, gzipped) | 662,633 | 64,943 | **−90%** |
| Render-blocking local stylesheets | 3 | 3 | — |
| Scripts to download, parse and execute | 3 | 2 | `MudBlazor.min.js` (70 KB) gone |
| JS round-trip before the correct appearance shows | 1 | **0** | the media query applies at first paint |

**NFR-001 is met.** Server render time is unchanged and the first-paint payload is an order of
magnitude smaller. The theme provider's round-trip is gone outright, which also removes the
flash-of-wrong-appearance a dark-device athlete saw on every load.

**One regression risk found and fixed during the measurement.** The vendored stylesheet's
`@import` of Source Serif 4 is discovered only *after* `broadsheet.css` has been fetched and
parsed, chaining two render-blocking requests that could run in parallel. It was moved to a
`<link>` with `preconnect` in `App.razor`; the token still names the family in `broadsheet.css`, so
NFR-002's single definition site is intact and only the fetch moved.

**What this measurement does not cover.** These are server timings and transfer sizes, not a
browser's paint timeline, and the local store held no activities — so both builds rendered their
empty state rather than a populated dashboard. The populated comparison is left to the human review
below.

## Two spec amendments, and one departure from the contract

Per Principle VII, none of these was decided silently in code.

1. **Amendment 2 (new, recorded in `spec.md`)** — FR-006 requires daily load as bars, and no
   artefact said where the series comes from. `DailyTrainingMetrics` carries fitness, fatigue and
   their difference and no load, so `LoadBars(metrics, width, height)` as specified in
   `data-model.md` §3 describes a function that could not exist. `DashboardView` gains `DailyLoad`,
   the aggregation the builder already computes and was discarding. No new query, no new
   arithmetic. The alternative — inverting the fatigue moving average — was rejected: it adds
   presentation-layer arithmetic FR-001 forbids and has no answer for the window's first day.

2. **`?connect=declined|scope|mismatch` — a gap recorded, not filled.** T037 asks that "the
   existing message for that outcome still appears with its current wording". There is none.
   `/strava/callback` redirects to those URLs and nothing on the dashboard reads the parameter, so
   an athlete who declines consent, withholds a scope or authorizes as a different athlete returns
   to an unchanged page with no explanation. That was true before this feature and is true after
   it. A preservation test would have been vacuous, so the test asserts what could actually
   regress — that the redesigned page still renders under each query string — and says in its own
   doc comment that there is no message here to preserve. **Adding one is a decision for the
   developer, not for this feature.**

3. **Contract §3 departed from: the rail's nameplate is the `<h1>`.** The contract specifies a
   styled `<div>`, following a reference mock that carries no `<h1>` at all. The rail comes first
   in the document, so a `div` nameplate puts four `<h2>`s — As of, Window, Strava, Max heart rate
   — ahead of the page's only `<h1>`, which is not a heading structure and fails FR-019. The
   contract's stated reason for the rule (exactly one `<h1>`, and one that
   `FocusOnNavigate Selector="h1"` finds) is better served this way, and keyboard focus lands at
   the top of the page instead of part-way down it. `InformationPreservationTests`' single-`h1`
   assertion passes unchanged.

Two wordings from the reference design were **not** adopted, because FR-002 outranks it: the week
figure is labelled "This week", not "Week load", and the sessions table is headed "Recent
activities", not "Recent sessions".

## Amendment 3 — the chart's hover readout

Added after the developer reviewed the running redesign. Recorded in `spec.md` before it was built,
because it is new athlete-facing behaviour: feature 006 chose a legend *instead of* a tooltip, and
R1 of this feature treated losing `MudChart`'s tooltip as accepted rather than regrettable.

**How it works, and what it deliberately does not use.** Every day's slot — an emphasis band over
its bar, a guide, a point on each line and a readout naming all four figures — is rendered once and
revealed by CSS `:hover`. No JavaScript, which the design's own "no JavaScript, no build step"
intent and R3/R5 both rule out; and no Blazor event handler, which on this Interactive Server
circuit would mean a server round-trip per mouse movement.

**Why the readout is HTML rather than SVG.** The plot is stretched with
`preserveAspectRatio="none"` so it fills its column, which stretches anything drawn inside it. Text
in SVG would render horizontally distorted at every width but one. The readout is therefore HTML
positioned over the plot in percentages — which map exactly onto the same box.

**Correctness guards.** `HoverSlots` places each point by asserting against `Plot`'s own output
rather than recomputing it, so a dot cannot drift off the line it marks. Every position goes
through the same invariant formatter as the SVG geometry — here the stakes are slightly worse than
in an SVG attribute, because `left:12,34%` is not a parse error but a declaration the browser drops
silently, and every slot would stack at the left edge. Fifteen tests cover it.

**A pre-existing guard caught a false positive**, recorded because the fix was to the comment and
not to the guard: `No_threshold_number_lives_in_the_web_project` scans `src/` for feature 004's
threshold numbers, and an example percentage ending in `5%` inside a doc comment matched it. The
comment was reworded; the guard is untouched.

### The cost, measured

| | Before the readout | With it |
|---|---|---|
| Server time to first byte for `/` (median of 12, warm) | 113.2 ms | 113.2 ms |
| Document size, 180-day window | 31,123 bytes | 220,197 bytes |

**Time to first byte did not move.** The document is 7× larger, which is the honest cost of
rendering 180 slots rather than computing one in script. On the loopback interface this application
runs over, 190 KB is on the order of a millisecond, so **NFR-001 still holds** — but it is the one
number in this feature that moved materially, and it is the developer's call whether to spend it:

- **Leave it.** The application is local-only by design; the transfer is not observable.
- **Compress dynamic responses.** One line of middleware; this markup is extremely repetitive and
  would fall by roughly an order of magnitude. It would benefit every response, not just this one.
- **Trim the markup.** Moving the labels into CSS `content` and the values into data attributes
  would roughly halve it — but it would put the readout's words out of reach of
  `Each_readout_names_and_states_every_figure`, and weakening an assertion to save bytes is the
  wrong trade to make silently.

Nothing was chosen here. The measurement is the deliverable.

## Still outstanding — human review only

`quickstart.md` lists these as settleable only by a person, and none of them is claimed here:

- [ ] Whether the page **reads as newsprint** — the point of the feature, and not assertable.
- [ ] **SC-008**: show the dashboard to someone who has not seen it and confirm they can name their
      Fitness, Fatigue and Form within 5 seconds.
- [ ] `ReconnectModal` in both appearances. It now has three tests, which it never had, but they
      cover its content and its tokens — not whether the restyled dialog looks right.
- [ ] Dark-scheme legibility of the three series. The ratios are measured in both schemes; whether
      the dashed Form line still reads at a glance is not.
- [ ] The hover readout: whether it lands where the eye expects at 30, 90 and 180 days, whether the
      flip to the left half-way across the plot happens early enough, and whether the emphasis band
      reads as "this bar" at 180 days, where a bar is about two pixels wide.
- [ ] Whether whitespace and hairline rules separate regions as convincingly in dark as in light.
- [ ] **T040**: the before/after screenshot comparison on a populated history.
- [ ] **T049**: every row of the state contract, walked in both appearances.
- [ ] **T053**: 320px, 768px, 1440px, 2560px and 200% zoom.
- [ ] **T059**: tab the full page; toggle the OS appearance with the page open; view in greyscale.
- [ ] **T068a**: the populated-history paint comparison in a browser's network panel, to complete
      the measurement above.

## Verdict

Every automated check passes: 516 tests across the solution, `scripts/compliance-008.sh` clean,
`dotnet build` clean with no MudBlazor reference anywhere in the repository. Principles I, II and
III hold, with the two Principle I sequencing departures named above rather than smoothed over.

**Not complete until a human has signed off the review items above.** The constitution's definition
of done requires it, and this feature's central claim — that the page reads as a considered
newspaper rather than as component-library output — is precisely the one no test in this repository
can make.
