# Bug Verification: Dashboard metrics chart renders but is invisible

- **Slug**: chart-not-rendered
- **Tested**: 2026-09-18
- **Assessment**: ./assessment.md
- **Fix**: ./fix.md
- **Result**: verified

## Summary

The bug no longer reproduces. The dashboard was run against the real synced database and the
reporter confirmed by eye that the three trend lines and their legend now render. All 409 tests
pass, in both the default and a comma-decimal locale, and the feature's compliance checks are clean.

## Checks Performed

| Check | Command / Action | Result | Notes |
|-------|------------------|--------|-------|
| Reproduction (post-fix) | Ran the app, opened `http://localhost:5062/`, viewed the chart area | **pass** | Reporter confirmed three lines and the legend are visible. This is the check that closes the bug. |
| Served markup | `curl http://localhost:5062/` | pass | 3 `<polyline>` elements, all 3 carrying the `b-0ktnoero9q` scope; all 3 legend entries present; no `chart-empty` empty state. |
| Served stylesheet | `curl .../TrainingLoadAnalyzer.Web.pkdipjtcgm.styles.css` | pass | `.line-fitness[b-0ktnoero9q] { stroke: var(--fitness) }` etc. — the selector matches the element actually served. |
| New tests | `./tests/TrainingLoadAnalyzer.Web.Tests/bin/Debug/net10.0/TrainingLoadAnalyzer.Web.Tests` | pass | Both new tests green; previously confirmed red against the unfixed tree. |
| Regression suite | Same, for Domain / Infrastructure / Web | pass | 204 + 106 + 99 = **409 passed, 0 failed**. |
| Locale suite | `DOTNET_SYSTEM_GLOBALIZATION_PREDEFINED_CULTURES_ONLY=false LANG=fi_FI.UTF-8 …Web.Tests` | pass | 99 passed. Run because the chart's SVG coordinates are culture-sensitive by design. |
| Build | `dotnet build TrainingLoadAnalyzer.sln` | pass | 0 errors, 0 warnings. |
| Compliance | `./scripts/compliance-006.sh` | pass | All 10 checks ok, including "Principle II: Domain/Infrastructure unchanged since main". |
| `dotnet test` | `dotnet test` | **not-run** | Reports `Zero tests ran` (exit 5) for all three projects on this machine, including projects this fix never touched. Pre-existing runner issue, not caused by the fix — see Residual Risks. |

## Output Excerpts

Reproduction, as served to the browser:

```
<polyline class="line line-fitness" points="0,96.24 3.35,94.27 …" fill="none"
          vector-effect="non-scaling-stroke" b-0ktnoero9q>
```

```css
.chart[b-0ktnoero9q]        { --fitness: #0072b2; … }
.line[b-0ktnoero9q]         { stroke-width: 2px; … }
.line-fitness[b-0ktnoero9q] { stroke: var(--fitness); }
```

Test runs:

```
TrainingLoadAnalyzer.Domain.Tests          Total: 204, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0
TrainingLoadAnalyzer.Infrastructure.Tests  Total: 106, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0
TrainingLoadAnalyzer.Web.Tests             Total:  99, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0
```

Compliance:

```
Feature 006 compliance
  ok    Principle II: src/TrainingLoadAnalyzer.Domain unchanged since main
  ok    C85: no Strava namespace imported into Features/Dashboard
  …
  ok    SC-007: no JavaScript beyond the framework's own
```

## Residual Risks

- **One browser, one viewport.** The chart was confirmed in the reporter's browser at its current
  window size. `.chart-plot` is `width: 100%; height: 200px` with `preserveAspectRatio="none"`, so
  narrow viewports compress the plot horizontally rather than scrolling — not examined.
- **No automated visual regression.** The two new tests prove the stylesheet exists, declares
  strokes, and is scoped to the polylines. Nothing in this stack renders a pixel, so a future change
  that leaves the wiring intact but makes the chart unreadable — a white stroke, a zero height —
  would still pass. The manual look remains load-bearing.
- **Colour rendering unexamined for colour-vision deficiency.** The palette is colour-blind-safe by
  construction and every stroke clears 3:1 contrast on white by calculation, and each line also
  carries a distinct dash pattern, but this was not checked with a simulator.
- **`dotnet test` is broken on this machine**, which is how `README.md:122` tells a contributor to
  run the suite. The assemblies were run directly instead. Unrelated to this bug, but it means the
  documented verification path currently reports success-looking output while running nothing.
- **Environment.** Run with `Athlete__MaximumHeartRate=190` from the README's example rather than
  the athlete's real figure, and with no Strava credentials in the process. Neither affects whether
  the chart draws; the former does affect the tile figures shown during the check.
- **The rest of the dashboard is still unstyled** (`tile`, `weekly`, `recent`, `sync`, …). Out of
  scope for this bug by decision, and tracked in the fix report's follow-ups.

## Recommendation

**Close the bug — verified end-to-end.** The symptom is gone at the only place it was ever visible,
the change is confined to one new stylesheet plus its tests, and nothing else moved: the domain and
infrastructure projects are byte-identical to `main` per the compliance script, and all 409 tests
pass in two locales. The one thing worth doing before merge is a glance at the chart on a narrow
window, since the plot stretches rather than scrolls. The `dotnet test` failure deserves its own
issue — it is unrelated to this fix but it silently disarms the project's documented way of running
the suite.
