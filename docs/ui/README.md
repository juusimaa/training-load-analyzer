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

## Not yet covered

Empty state, not-connected state, revoked-credential state and the rate-limited sync message. Ask before building them out.
