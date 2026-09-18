# Bug Fix: Dashboard metrics chart renders but is invisible

- **Slug**: chart-not-rendered
- **Fixed**: 2026-09-18
- **Assessment**: ./assessment.md
- **Status**: applied

## Summary

Added the chart's missing presentation as a component-scoped stylesheet. The three polylines were
being computed correctly and then painted with nothing — SVG's initial value for `stroke` is `none`,
and no stylesheet in the repository defined the `.line-*` classes the component delegates to. Two
regression tests now cover the two distinct ways the chart can go blank again.

## Changes

| File | Change | Notes |
|------|--------|-------|
| `src/TrainingLoadAnalyzer.Web/Components/Dashboard/MetricsChartView.razor.css` | added | The whole fix. Plot box, line strokes, three-colour palette, legend swatches, caption and empty-state styling. |
| `tests/TrainingLoadAnalyzer.Web.Tests/DashboardComponentTests.cs` | modified | Two new tests, a local `RepositoryRoot()` helper, and a `System.Text.RegularExpressions` using. |

No other file was touched. `MetricsChartView.razor`, `MetricsChart.cs`, `DashboardView.cs` and
`app.css` are all unchanged — the assessment ruled the first three out, and keeping the rules out of
`app.css` was the point of the preferred remediation.

## Diff Highlights

The declaration the entire bug reduces to — the polylines had no stroke of any kind, so they drew
zero pixels:

```css
.line {
    fill: none;
    stroke-width: 2px;
    stroke-linejoin: round;
    stroke-linecap: round;
}

.line-fitness { stroke: var(--fitness); }
.line-fatigue { stroke: var(--fatigue); stroke-dasharray: var(--fatigue-dash); }
.line-form    { stroke: var(--form);    stroke-dasharray: var(--form-dash); }
```

The palette is declared once on `.chart` and consumed by both the lines and their legend swatches,
so a line and the key naming it cannot drift to different colours:

```css
.chart {
    --fitness: #0072b2;   /* blue       */
    --fatigue: #d55e00;   /* vermillion */
    --form:    #009e73;   /* green      */
    --fatigue-dash: 7 4;
    --form-dash: 2 4;
}
```

The plot also needed an explicit box; without a height an inline SVG falls back to the
replaced-element default:

```css
.chart-plot { display: block; width: 100%; height: 200px; }
```

## Tests Added or Updated

- `DashboardComponentTests.Every_chart_line_carries_the_isolation_scope_that_paints_it` — asserts
  every rendered `<polyline>` carries a `b-*` CSS-isolation attribute. Blazor emits that attribute
  only when a companion `.razor.css` exists, so the test proves both that the stylesheet is present
  and that isolation reaches inside the SVG. It also catches the subtler regression: move the plot
  into a child component and scoping stops applying, the chart goes blank, and every other test in
  the file still passes.
- `DashboardComponentTests.The_chart_stylesheet_gives_every_line_a_stroke` — reads the stylesheet as
  text and asserts each of the three `line-*` classes declares a `stroke`, plus a `stroke-width` on
  `.line`. Crude, and the only thing standing between a stripped rule and an invisible chart
  shipping green: bUnit applies no CSS, so a chart whose lines are all `stroke: none` renders
  identically to a correct one.
- The three existing chart tests are untouched and still guard the geometry, the legend and the
  30-day empty state.

## Local Verification

- **RED confirmed first.** With `MetricsChartView.razor.css` temporarily moved aside and the project
  rebuilt, both new tests failed and the other 97 passed — a direct demonstration that the suite as
  it stood could not see this bug. The stylesheet was then restored and the failure message on the
  second test improved from a raw `FileNotFoundException` to an explicit assertion.
- `dotnet build TrainingLoadAnalyzer.sln` → succeeded, 0 errors. The one warning (`xUnit2031` in
  `SyncCoordinatorTests.cs:123`) is pre-existing and unrelated.
- Full suite, all three projects → **409 passed, 0 failed** (Domain 204, Infrastructure 106, Web 99).
- Web suite re-run under `DOTNET_SYSTEM_GLOBALIZATION_PREDEFINED_CULTURES_ONLY=false
  LANG=fi_FI.UTF-8` per the README → 99 passed, 0 failed. Run because the chart's coordinates are
  culture-sensitive by design and this fix touches the chart.
- **Verified by inspection rather than assumption**: the generated
  `obj/Debug/net10.0/scopedcss/Components/Dashboard/MetricsChartView.razor.rz.scp.css` shows the
  selectors correctly rewritten (`.line-fitness[b-0ktnoero9q]`, `.chart-plot[b-0ktnoero9q]`, …).
  Whether Blazor's CSS isolation attributes SVG children was the one part of this approach that
  could not be taken on trust; the scope test above settles it at runtime.
- **Not verified: the pixels.** Nothing in this stack renders a browser. No test here can confirm
  the athlete now sees three lines — that belongs to `/speckit-bug-test`.

### Note on `dotnet test`

`dotnet test` reports `Zero tests ran` (exit code 5) for all three projects on this machine,
including `Domain.Tests`, which this change never touches. It is a pre-existing environment issue
with the xUnit v3 / Microsoft Testing Platform runner, **not** a consequence of this fix. The test
assemblies are `OutputType: Exe` and were run directly instead
(`./tests/<project>/bin/Debug/net10.0/<project>`), which executes and reports the full suite. Worth
a separate look, since the README documents `dotnet test` as the way to run the suite.

## Deviations from Assessment

None of substance. Three notes:

- The assessment offered a choice between test (a) reading the stylesheet and (b) asserting a
  non-`none` stroke on the rendered polylines, calling (b) stronger. (b) is only available under the
  rejected presentation-attribute alternative, so the scope-attribute test was written instead —
  it is closer in strength to (b) than (a) is, since it fails on a missing stylesheet *and* on
  broken isolation. Both (a) and the scope test are included; they catch different regressions.
- The assessment suggested pairing colour with `stroke-dasharray` conditionally ("if that is hard to
  guarantee"). It was applied unconditionally: all three series carry a distinct pattern, so the
  chart is readable without colour vision at all.
- `RepositoryRoot()` was duplicated into `DashboardComponentTests` from `WeeklyLoadAndTrendTests`
  rather than shared, to keep the change minimal per the assessment's scope-creep warning. Flagged
  below.

## Follow-ups

- **The rest of the dashboard is still unstyled.** `tile`, `tile-label`, `tile-value`, `tile-note`,
  `weekly*`, `recent*`, `sync*`, `empty`, `connect` and `loading` are all referenced in markup and
  defined nowhere. They render as legible unstyled text, so nothing is invisible — which is why only
  the chart was reported — but this is a real gap and deserves its own ticket. Deliberately left
  out of this fix.
- **`dotnet test` runs zero tests.** Pre-existing, affects all three projects, and makes the
  documented command in `README.md:122` misleading. Worth its own investigation.
- **Consolidate `RepositoryRoot()`** into `Fixtures` when a third copy is wanted.
- **Colour choices are unreviewed by eye.** The palette (Okabe–Ito blue / vermillion / green) is
  colour-blind-safe by construction and every stroke clears 3:1 contrast against white by
  calculation, but nobody has looked at it yet.
- **No dark mode.** The caption and empty-state colours are fixed greys, consistent with the rest of
  the project, which has no theming anywhere.
