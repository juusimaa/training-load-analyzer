# UI Contract: Broadsheet Dashboard

**Feature**: 008-broadsheet-dashboard-redesign | **Date**: 2026-09-19

The application exposes no public API, no CLI and no wire format. Its one external interface is the
rendered page. This document is therefore the contract the implementation must satisfy and the
tests assert against.

---

## 1. Region contract

Every athlete-facing region, where it lives, and what it must render.

### Rail (`aside`, sticky at wide widths, static when stacked)

| Element | Source | Contract |
| --- | --- | --- |
| Masthead | static | "Training Load". **Must not be an `<h1>`** — see §3. |
| As-of date | `DashboardView.AsOf` | Date, with the ISO week designation beneath it (`DashboardView.IsoWeek`, e.g. `2026-W38`). |
| Window control | component state | Three options: 30, 90, 180 days. Exactly one selected; 180 is the default. Each option ≥48px high (R9). |
| Strava state | `SyncStatus` | Connection state, `SyncMessage.For(status)` verbatim, last-checked time in its existing format. |
| Sync action | `SyncStatus.IsRunning` | A single `<button class="sync">`, `disabled` while running. |
| Reconnect action | `SyncStatus` | Rendered instead of / alongside sync when reconnection is required; links `/connect`. |
| Max heart rate | `DashboardView.MaximumHeartRate` | Value with unit and provenance. |

**No control may appear outside the rail** (FR-003). This is the layout's organising rule.

### Metric row (four peers, one row)

| Figure | Source | Colour |
| --- | --- | --- |
| Fitness | `Current.Fitness` | `--color-accent-700` |
| Fatigue | `Current.Fatigue` | `--color-accent-2-700` |
| Form | `Current.Form` | `--color-text` |
| Week load | `CurrentWeek.Points` | `--color-text`, with the trend caption beneath |

Each carries a small label above and the figure at display size. Any missing value renders as an
em dash and **still occupies its position**, so the row stays aligned (FR-008, edge case 1).

### Chart

Heading names the window ("Daily load and metrics · last 180 days"). Legend names all three series.
Plot layers, back to front: load bars, zero rule, Form, Fatigue, Fitness. Below 30 days of history,
the insufficient-history message renders **instead**, and no `<svg>` is emitted (R8).

### Recent sessions

A `<table class="table">`, seven rows maximum, columns: Day, Type, Moving time, Basis, Load
(right-aligned). Basis is a `<span class="tag">` reading "measured" or "estimated" — the tag carries
the existing word, it does not replace it (FR-002, FR-018).

---

## 2. State contract

Each state, its trigger, and what must be present. Wording is pinned by
`InformationPreservationTests` and `SyncMessageTests` and must not drift.

| State | Trigger | Content column | Rail |
| --- | --- | --- | --- |
| Loading | `view is null` | Loading indication in place of figures, chart and table | rendered |
| Data unavailable | `view.IsUnavailable` | Distinct, visibly marked notice | rendered |
| Empty, not connected | `!HasActivities && !IsStravaConnected` | Explanation + prominent "Connect Strava" action | rendered |
| Empty, connected | `!HasActivities && IsStravaConnected` | Explanation that a sync will bring training in; **no** redundant connect action | rendered, sync offered |
| Populated | `HasActivities` | Metric row, chart, recent sessions | rendered |
| Sync running | `SyncStatus.IsRunning` | unchanged | button `disabled`, progress stated |
| Reconnection required | `Failure is not null` or `Outcome == ReconnectionRequired` | unchanged | reconnect action offered |
| Rate limited | `Outcome == RateLimited` | unchanged | rate-limit message, retry time when known |

Also in scope, under the same visual system (FR-010): `NotFound.razor`, `Error.razor`, the
`#blazor-error-ui` notice in `MainLayout`, and `ReconnectModal`.

The `/connect` route is a redirect-only endpoint with no page of its own, but the dashboard must
keep rendering its `?connect=declined|scope|mismatch` outcomes.

---

## 3. Markup hooks (load-bearing — existing tests bind to these)

These are not styling choices. Changing one breaks a test that exists to catch a regression (R8).

| Hook | Required by |
| --- | --- |
| `.recent-row` on each session row | `DashboardComponentTests:365` |
| `.tile-value` on each metric figure | `DashboardComponentTests:200` (asserts **absent** when empty) |
| `button.sync`, a real `<button>`, `disabled` while running | `DashboardComponentTests:377,415` |
| Exactly one `<h1>` per page | `InformationPreservationTests`; `FocusOnNavigate Selector="h1"` |
| One `<h2>` per content region, in reading order | `InformationPreservationTests` |
| No `<svg>` in the insufficient-history branch | `DashboardComponentTests:294` |
| `href="/connect"` present in the connect states | `DashboardComponentTests` |

The rail's masthead must therefore be a styled `<div>`, not a heading — matching what the reference
page does.

---

## 4. Token contract

Colour, type, spacing, radius and elevation resolve **only** from the custom properties listed in
[data-model.md §2](../data-model.md). Two rules are machine-checked:

1. **No colour literal outside a `Theme` path segment** — `ColourDisciplineTests` scans every
   `*.css` and `*.razor` under `src/TrainingLoadAnalyzer.Web`. This is why chart strokes and fills
   come from CSS classes rather than SVG presentation attributes.
2. **Every required token exists in both schemes, and every pairing in the contrast table meets its
   minimum** — the repointed `PaletteSlotTests` and `PaletteContrastTests`, with the load-bar
   exemption named explicitly rather than omitted.

---

## 5. Responsive contract

| Width | Layout |
| --- | --- |
| ≥ 60rem | Rail (250px, sticky) + content column, gap 60px, page capped at 1240px |
| < 60rem | Single stacked column, rail static, metric row wraps to two columns |
| ≥ 320px | No horizontal page scrolling, no clipped or overlapping content, at any width |

The chart scales to the available width at every size. At 200% zoom the layout degrades to the
stacked arrangement rather than clipping (edge case 7).

---

## 6. Accessibility contract

- Text 4.5:1, large text and meaningful non-text indicators 3:1, in **both** schemes — except the
  chart's load bars (Amendment 1(b)).
- Visible focus indicator on every interactive element; the vendored sheet's
  `:focus-visible { outline: 2px solid var(--color-accent) }` provides the base.
- Touch targets ≥48×48 (R9).
- No distinction carried by colour alone. The chart's three lines are distinguished by **stroke
  pattern as well as colour** and named in the legend (SC-007, as amended).
- Heading structure names the regions in reading order.
- The existing global `prefers-reduced-motion` reset in `app.css` is retained.
