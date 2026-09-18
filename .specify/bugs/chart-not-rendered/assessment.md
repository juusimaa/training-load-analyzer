# Bug Assessment: Dashboard metrics chart renders but is invisible

- **Slug**: chart-not-rendered
- **Created**: 2026-09-18
- **Source**: pasted text (no URL supplied, so no fetch was performed)
- **Verdict**: valid
- **Severity**: high

## Report (verbatim or summarized)

> After Strava sync numeric data is visible but graph is not.

Clarified interactively during assessment:

- The chart area between the weekly load panel and the recent activity list shows **empty space** —
  no lines and, as reported, no chart.
- The **"Not enough data to show trends (30+ days required)"** empty-state message is **not** what
  the athlete sees, so the component is taking its *has enough history* branch and emitting the SVG.
- The chart's **legend (Fitness / Fatigue / Form) and the date-span caption below it ARE visible**.
  Both are plain text inside that same branch, which confirms the component renders in full and
  that only the plotted lines are missing.

## Symptom

After a Strava sync the dashboard's numeric output is correct — the Fitness / Fatigue / Form tiles,
the weekly load panel and the recent activity list all populate — but the 180-day trend chart
(FR-006, SC-003) shows nothing. Expected: three plotted lines (fitness, fatigue, form) with a
legend, over the same history the tiles summarise.

## Reproduction

1. Connect a Strava account and run a sync that imports at least 30 days of activity history.
2. Open the dashboard at `/`.
3. Observe the tiles and the recent activity list render with values.
4. Observe the `<section class="trends">` area: the SVG element is present in the DOM but nothing is
   drawn.

Confirmed by the reporter: the legend and the date-span caption **are** on screen. The component
therefore renders in full and the SVG is in the DOM — only the three polylines are unpainted.

## Suspected Code Paths

- `src/TrainingLoadAnalyzer.Web/wwwroot/app.css` — the application's only global stylesheet. It is
  37 lines and contains **only** the default Blazor template rules (`h1:focus`, `.valid.modified`,
  `.invalid`, `.validation-message`, `.blazor-error-boundary`, `.darker-border-checkbox`,
  `.form-floating`). No rule anywhere in it mentions `chart`, `line`, `stroke` or `legend`.
- `src/TrainingLoadAnalyzer.Web/Components/Dashboard/MetricsChartView.razor:21` — the polylines are
  emitted as `<polyline class="line line-@series.CssClass" points="…" fill="none" …>`. They carry
  **no `stroke` attribute and no `stroke-width`**, and rely entirely on a `.line` /
  `.line-fitness|fatigue|form` rule that does not exist.
- `src/TrainingLoadAnalyzer.Web/Components/Dashboard/MetricsChartView.razor:14-19` — the `<svg
  class="chart-plot">` carries a `viewBox` and `preserveAspectRatio="none"` but no `width`/`height`
  attributes, again depending on a `.chart-plot` rule that does not exist.
- `src/TrainingLoadAnalyzer.Web/Components/App.razor:9-10` — only `app.css` and the scoped-CSS
  bundle `TrainingLoadAnalyzer.Web.styles.css` are linked. The scoped bundle can only contain what
  the `*.razor.css` files hold, and the only two in the project are
  `Components/Layout/MainLayout.razor.css` (one `#blazor-error-ui` rule) and
  `Components/Layout/ReconnectModal.razor.css`. There is no `MetricsChartView.razor.css`.
- `src/TrainingLoadAnalyzer.Web/Features/Dashboard/MetricsChart.cs` — **ruled out**. The geometry
  code is sound: invariant-culture coordinates, one shared vertical scale, `Count == 1` handled, and
  an empty series returns `[]` rather than half-formed markup. It is not the fault.
- `src/TrainingLoadAnalyzer.Web/Features/Dashboard/DashboardView.cs:HasEnoughHistoryForChart` —
  **ruled out by the clarification above**: the 30-day empty state is not what the athlete sees.
- `tests/TrainingLoadAnalyzer.Web.Tests/DashboardComponentTests.cs:251,289` — explains why the suite
  is green. The tests assert `chart.FindAll("polyline").Count == 3`, i.e. that the *markup* exists.
  bUnit does not apply or compute CSS, so an unstyled — therefore invisible — chart passes.

## Root Cause Hypothesis

The chart is drawn, correctly, and then painted with nothing. SVG's initial value for the `stroke`
property is `none`, so a `<polyline>` with `fill="none"` and no stroke of any kind produces exactly
zero pixels. `MetricsChartView.razor` delegates every visual property — line colour, line width,
plot height, legend swatches — to CSS classes (`chart`, `chart-plot`, `line`, `line-fitness`,
`line-fatigue`, `line-form`, `chart-legend`, `legend-*`, `chart-span`), and **no stylesheet in the
repository defines a single one of them**. Feature 006's `tasks.md` has no styling task at all
(T064 creates the geometry; nothing creates the presentation), so this is an unimplemented step
rather than a regression. The Strava sync is incidental: the sync is simply what first puts enough
history on screen for the athlete to notice the chart is blank. Every other dashboard class
(`tile`, `weekly`, `recent`, `sync`) is equally unstyled, but those components render **text**,
which is visible unstyled — which is precisely why "numeric data is visible but graph is not".
**Confidence: high.**

## Proposed Remediation

**Preferred**: add a component-scoped stylesheet
`src/TrainingLoadAnalyzer.Web/Components/Dashboard/MetricsChartView.razor.css`, which Blazor's CSS
isolation compiles into the already-linked `TrainingLoadAnalyzer.Web.styles.css` bundle. This
colocates the chart's presentation with the markup that owns it, matches the pattern already used by
`MainLayout.razor.css` and `ReconnectModal.razor.css`, and keeps the rules from leaking to unrelated
markup. It must define at minimum:

- `.chart-plot` — an explicit box, e.g. `width: 100%; height: 200px; display: block`. The component
  sets `preserveAspectRatio="none"`, so stretching the 600×200 viewBox to the container is the
  intended behaviour and no aspect ratio needs preserving.
- `.line` — `fill: none; stroke-width: 2px; stroke-linejoin: round`. The
  `vector-effect="non-scaling-stroke"` already on the element keeps the stroke an even weight under
  the non-uniform scale.
- `.line-fitness`, `.line-fatigue`, `.line-form` — three distinguishable `stroke` colours. Pick
  hues that survive the common colour-vision deficiencies; if that is hard to guarantee, pair the
  colour with a `stroke-dasharray` so the three series differ by more than hue alone.
- `.chart-legend` and `.legend-*` — a horizontal, unbulleted list with a colour swatch per entry
  matching its line, so the legend actually does the job US3 scenario 3 assigns it in place of a
  tooltip.
- `.chart`, `.chart-empty`, `.chart-span` — modest container, empty-state and caption styling.

**Alternatives**:

- Put the same rules in `wwwroot/app.css`. Simpler to find, one fewer build step — but global, and
  it starts a habit of accumulating every dashboard component's styling in one unscoped file.
- Set `stroke`/`stroke-width` as presentation **attributes** in the Razor markup instead of CSS.
  This makes the chart immune to a missing stylesheet and makes the stroke assertable in a bUnit
  test, at the cost of hard-coding the palette into the component and reopening the culture-safety
  question for any numeric attribute. Worth considering for `stroke-width` alone.

**Files likely to change**:

- `src/TrainingLoadAnalyzer.Web/Components/Dashboard/MetricsChartView.razor.css` (new)
- `src/TrainingLoadAnalyzer.Web/Components/Dashboard/MetricsChartView.razor` (only if the attribute
  alternative is adopted, or to add a wrapper element the styling needs)
- `tests/TrainingLoadAnalyzer.Web.Tests/DashboardComponentTests.cs`

**Tests to add or update**:

- A test that fails on the bug as it stands: assert the chart's presentation exists, not only its
  geometry. bUnit computes no CSS, so the honest options are (a) assert the stylesheet file exists
  and defines a `stroke` for each of the three `line-*` classes, or (b) if the attribute alternative
  is taken, assert each rendered `<polyline>` carries a non-`none` `stroke`. (b) is the stronger
  test; (a) is the one that fits the preferred fix.
- Keep the existing `The_chart_renders_three_polylines_inside_an_svg` and the empty-state test
  unchanged — they still guard the geometry and the 30-day threshold.
- A visual confirmation belongs in `/speckit-bug-test`: no unit test in this stack can see a pixel.

## Risks & Considerations

- **No automated test can fully close this bug.** The regression lock is indirect; the real
  verification is a human (or a screenshot) looking at the dashboard.
- **Scope creep.** The whole dashboard is unstyled — the tiles, weekly panel, recent list and sync
  panel all reference classes that do not exist. The fix should stay on the chart, which is what was
  reported; the rest is worth a separate ticket and should not be quietly folded in here.
- **Accessibility.** Three lines distinguished by colour alone fail for colour-blind athletes. The
  `aria-label` on the SVG already covers a screen reader; the dasharray suggestion covers the rest.
- **No API, schema, migration, performance or security impact.** This is presentation only, and
  nothing outside the Web project is touched.
- **Dark mode / theming** is not currently handled anywhere in the project; hard-coded stroke
  colours are consistent with that and need not be solved now.

## Open Questions

Both resolved by the reporter on 2026-09-18. None outstanding.

- ~~Are the chart legend and the date-span caption visible on screen?~~ **Yes.** The component
  renders in full; only the polylines are unpainted. Root cause confirmed, confidence high.
- ~~Is there a palette the fix should follow?~~ **No** — the fix picks its own three colours. They
  should be readable against the page's default white background and distinguishable to a
  colour-blind reader (see the `stroke-dasharray` note under Proposed Remediation).
