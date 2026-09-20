# Dashboard UI reference

A static reference for the redesigned dashboard landing page. Open `index.html` in a browser; no build step and no server.

## What this is

The current page reads as default component-library output: controls sit wherever they were dropped, and every element carries the same visual weight. This reference lays the same information out as a newsprint page — one standing rail for context and controls, the figures set in the serif scale, and whitespace instead of boxes and dividers.

## Layout

- **Left rail (250px, sticky)** — as-of date and ISO week, the chart window control, Strava connection state with the manual sync button, and the configured maximum heart rate. Every control lives here; nothing floats in the content column.
- **Metric row** — Fitness, Fatigue, Form and current-week load at display size. Fitness is cyan, Fatigue magenta, matching the chart lines; Form and week load stay in ink.
- **Chart** — daily load as neutral bars behind the three metric lines over the selected window (30 / 90 / 180 days), with a zero rule for Form.
- **Recent sessions** — the seven most recent activities as a table; the measured/estimated basis is a tag, not a colour difference.

## Bindings

The page ships with sample figures generated in the inline script. In the application these come from `DashboardView`:

| Element | Source |
| --- | --- |
| Fitness / Fatigue / Form | `DashboardView.Current` (CTL / ATL / TSB), via `Display.Metric` |
| Week load | `DashboardView.CurrentWeek`, via `Display.Points` |
| Week trend caption | `DashboardView.Trend` — classification from the domain, percentage via `Display.Percent` |
| Chart | `DashboardView.Metrics` (up to 180 days); empty-state copy when `HasEnoughHistoryForChart` is false |
| Recent sessions | `DashboardView.Recent` — `Display.Day`, `Display.Duration`, `Display.Points`, and `TrainingLoad` provenance for the basis tag |
| Sync rail | `SyncStatus` — `IsRunning`, `Result`, `Failure`, `FinishedAt`, `RetryAfterLocal` |

Missing figures render as an em dash (`Display.Missing`), never a zero.

Sync is manual only. The rail states the last result and never implies a scheduled pass.

## Styling

`styles.css` is the Broadsheet design system stylesheet, vendored unchanged. Take colour, type, spacing, radius and shadow from its variables; use its `.btn`, `.tag`, `.table`, `.seg` classes rather than new ones. The page adds layout-only rules in its `<style>` block, the same division the application already keeps between `app.css` and the theme.

## Not covered here — and what the application does instead

This page shows the populated dashboard only. It has never depicted the empty state, the
not-connected state, the revoked-credential state or the rate-limited sync message — but those
states were already shipped when this reference was written, so "not yet covered" described the
reference rather than the application. Feature 008 built all of them out in the language above, so
they are now covered in code and not here:

| State | Where it is defined | Where it is verified |
| --- | --- | --- |
| Loading, data unavailable, empty (connected and not) | `Components/Pages/Dashboard.razor` | `DashboardComponentTests` |
| Reconnection required, rate limited | `Components/Dashboard/SyncPanel.razor`, `Features/Sync/SyncMessage.cs` | `DashboardComponentTests`, `SyncMessageTests` |
| Not found, unhandled error, lost connection | `Components/Pages/NotFound.razor`, `Error.razor`, `Components/Layout/MainLayout.razor.css`, `ReconnectModal.razor.css` | `ReconnectModalTests`, and the colour and contrast suites |

The words each of them uses are fixed by `InformationPreservationTests` and `SyncMessageTests`.
Change this reference if the visual language changes; change the components if a state's wording
does — and expect a test to have an opinion about it.

## Two places the application diverges from this reference, deliberately

Recorded here so the difference reads as a decision rather than a drift. Both are argued in
`specs/008-broadsheet-dashboard-redesign/research.md`.

1. **Contrast.** Ten values on this page miss WCAG 2.1's 4.5:1 and 3:1 bars, including the primary
   button's label, links, table headings, muted text, the chart's date axis and its zero rule. The
   application steps each of them down the design system's own ramps (R4), and the daily-load bars
   — the one case with no painless fix — are exempted by a recorded amendment rather than shipped
   quietly. `PaletteContrastTests` measures every pairing in both appearances.
2. **Touch targets.** This page sets `.seg-opt { min-height: 44px }`; the application uses 48px,
   which is what its own FR-017 requires and what the previous interface already met (R9).

The application also vendors a **subset** of `styles.css` — the tokens and the base, `.btn`,
`.tag`, `.table` and `.seg` rules. The print-treatment machinery (`.halftone`, `.cmyk`,
`.cmyk-num`, `.cmyk-head`) resolves its filters against an SVG defs file the application never
ships, so those rules would arrive inert (R2). It adds a dark token set, which this page does not
have at all (R3).
