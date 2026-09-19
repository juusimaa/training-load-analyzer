# Phase 1 Data Model: Broadsheet Dashboard Redesign

**Feature**: 008-broadsheet-dashboard-redesign | **Date**: 2026-09-19

This feature introduces **no domain entity and no persisted data**. Nothing in
`TrainingLoadAnalyzer.Domain` or `TrainingLoadAnalyzer.Infrastructure` changes, and there is no
migration. What follows are the three models the presentation layer actually works with: the read
model, the design tokens, and the chart's geometry.

---

## 1. Read model

### `DashboardView` (`Features/Dashboard/DashboardView.cs`)

Existing members are unchanged and keep their current semantics.

| Member | Type | Status | Notes |
| --- | --- | --- | --- |
| `AsOf` | `DateOnly` | unchanged | shown in the rail |
| `IsStravaConnected` | `bool` | unchanged | selects the empty-state branch |
| `IsUnavailable` | `bool` | unchanged | storage read failed |
| `Metrics` | `IReadOnlyList<DailyTrainingMetrics>` | unchanged | capped at 180 days |
| `CurrentWeek` | `WeeklyTrainingLoad?` | unchanged | week-load figure |
| `Trend` | `WeeklyLoadTrend?` | unchanged | null when history does not reach the prior ISO week |
| `Recent` | `IReadOnlyList<RecentActivity>` | unchanged | seven most recent |
| `Current` | `DailyTrainingMetrics?` (derived) | unchanged | `Metrics[^1]` or null |
| `HasActivities` | `bool` (derived) | unchanged | `Metrics.Count > 0` |
| `HasEnoughHistoryForChart` | `bool` (derived) | unchanged | `Metrics.Count >= 30`; **not** a function of the selected window (R6) |
| **`MaximumHeartRate`** | **`int`** | **NEW** | from `AthleteSettings.MaximumHeartRate`, already read by `DashboardReader` for load estimation and simply carried through. Admitted by Amendment 1(a). |
| **`IsoWeek`** | **`string`** | **NEW** | the ISO-8601 week designation of `AsOf`, e.g. `2026-W38`. Derived, not stored. Admitted by Amendment 1(a). |

**Validation and formatting rules**

- Every figure that cannot be computed renders as `Display.Missing` (an em dash), never a zero or a
  blank cell (FR-008). This applies to `Current` being null, `CurrentWeek` being null and `Trend`
  being null.
- `MaximumHeartRate` is a configured integer and is always present; the rail states it with its
  unit and provenance ("186 bpm · from configuration"). If configuration is ever absent, the
  existing settings binding — not this feature — governs.
- `IsoWeek` uses ISO-8601 week numbering, which is the same basis the domain already uses for
  `WeeklyTrainingLoad`. It is a label only; no weekly computation reads it.

**What does not change**: `DashboardReader`, the SQLite query, `Display.*` formatting helpers, and
every wording in `SyncMessage`.

### `SyncStatus` (`Features/Sync/SyncStatus.cs`) — unchanged

`IsRunning`, `Result`, `Failure`, `FinishedAt`, `RetryAfterLocal` and `SyncStatus.Never` keep their
current shapes and meanings. The rail renders them through the existing `SyncMessage.For(status)`,
whose strings are pinned by `SyncMessageTests` and must not drift (FR-002).

---

## 2. Design tokens (`wwwroot/Theme/broadsheet.css`)

Tokens are CSS custom properties on `:root`, redefined in a `@media (prefers-color-scheme: dark)`
block (R3). This is the model the repointed `PaletteSlotTests` and `PaletteContrastTests` parse and
assert over (R7) — so the token set is a contract, not just a stylesheet.

**Required token groups** (both schemes must define every one; a missing token is a `PaletteSlotTests`
failure):

| Group | Tokens |
| --- | --- |
| Ground | `--color-bg`, `--color-surface`, `--color-text`, `--color-divider` |
| Accents | `--color-accent`, `--color-accent-2` |
| Neutral ramp | `--color-neutral-100` … `--color-neutral-900` |
| Accent ramps | `--color-accent-100` … `-900`, `--color-accent-2-100` … `-900` |
| Type | `--font-heading`, `--font-heading-weight`, `--font-body` |
| Spacing | `--space-1`, `-2`, `-3`, `-4`, `-6`, `-8` |
| Radius | `--radius-sm`, `--radius-md`, `--radius-lg` |
| Elevation | `--shadow-sm`, `--shadow-md`, `--shadow-lg` |

Omitted from the vendored subset (R2): `--color-process-yellow` and the print-treatment rules that
consume it, plus the `.nav`, `.card`, `.dialog` and `.input` component classes.

**Contrast pairings the tests assert**, in *both* schemes — the remedied values from R4:

| Foreground | Background | Minimum |
| --- | --- | --- |
| `--color-text` | `--color-bg` | 4.5:1 |
| `--color-bg` (button label) | `--color-accent-700` | 4.5:1 |
| `--color-accent-700` (links, `.tag-outline`) | `--color-bg` | 4.5:1 |
| 70% ink (`.table th`, `.text-muted`) | `--color-bg` | 4.5:1 |
| `--color-neutral-700` (axis, Form legend label) | `--color-bg` | 4.5:1 |
| `--color-accent-700`, `--color-accent-2-700` (display figures) | `--color-bg` | 3:1 |
| Chart Fitness / Fatigue / Form strokes | `--color-bg` | 3:1 |
| Chart zero rule (`--color-neutral-600`) | `--color-bg` | 3:1 |

**Exempt**: the chart's daily-load bars, per Amendment 1(b). The exemption is encoded as an
explicit, named exclusion in the test — not as a missing assertion — so it stays visible.

---

## 3. Chart geometry (`Features/Dashboard/MetricsChart.cs`)

Revived from commit `b1e8726^` (R5). Pure functions over the metrics list; no state, no rendering.

### Existing, recovered unchanged

```
record ChartSeries(string Label, string Points, string CssClass)

static string ViewBox(double width, double height)
static IReadOnlyList<ChartSeries> Plot(IReadOnlyList<DailyTrainingMetrics> metrics,
                                       double width, double height)
```

Invariants carried over from the original, each already covered by the revived tests:

- **Every coordinate is formatted with `CultureInfo.InvariantCulture`.** This is the module's
  reason for existing: on a Finnish machine the naive rendering produces `points="0,45,3 1,5,12,25"`
  — valid-looking markup, silently wrong geometry, no exception, correct on en-US.
- **One shared vertical scale across all three series**, so they can be read against each other and
  a negative Form sits inside the band rather than clipped at zero. Form is routinely negative.
- **Empty input yields no series at all**, never a series with an empty or half-formed `points`
  attribute.
- 8% vertical padding above and below the data band.

### New for this feature

| Function | Purpose | Key rules |
| --- | --- | --- |
| `LoadBars(metrics, width, height)` | `<rect>` geometry for daily load | Own scale, independent of the metric band — the bars are a backdrop, not a fourth series. Zero-load days produce no rect. Bar width derives from the point count, with a floor so a 180-day window stays visible. |
| `ZeroRule(metrics, width, height)` | y-position of the Form zero line | On the **shared metric scale**, so it lines up with the Form series. Absent when the band excludes zero. |
| `AxisTicks(metrics, count)` | evenly spaced date labels | Dates formatted invariantly; ends of the window always labelled. |

`CssClass` carries `series-fitness` / `series-fatigue` / `series-form`; bars and the rule get
`load-bar` and `zero-rule`. Colour resolves from tokens through those classes — never from an SVG
presentation attribute, which would both fail `ColourDisciplineTests` and ignore the dark scheme
(R5).

### Window selection

Not a model concern. The selected window (30/90/180) is component state on `Dashboard.razor` and is
applied by taking the trailing slice of `Metrics` before calling into `MetricsChart` (R6). No new
query, no storage round-trip.

---

## 4. State transitions

The dashboard's state machine is **unchanged** — this feature restyles its states, it does not add
or remove one (FR-009). Recorded here because every branch must be re-verified against the new
markup:

```
view is null                      → loading
view.IsUnavailable                → data unavailable
!view.HasActivities  &&  !connected → empty, connect required
!view.HasActivities  &&   connected → empty, sync will bring training in
view.HasActivities                → populated
```

Orthogonally, in the rail, from `SyncStatus`:

```
IsRunning                                    → sync running, button disabled
Failure is not null
  || Result.Outcome == ReconnectionRequired  → reconnection required, connect action offered
Result.Outcome == RateLimited                → rate limited, retry time when known
otherwise                                    → last result and last-checked time
```
