# Contract: Dashboard UI (React)

**Feature**: 009-python-react-stack | **Date**: 2026-09-23

The rendered page is the athlete-facing interface. **Feature 008's
[UI contract](../../008-broadsheet-dashboard-redesign/contracts/dashboard-ui.md) applies in full**:
its regions, heading outline, states, responsive rules, contrast, focus, touch targets and
non-colour encoding. This document adds only what changes because the page is now a React client
of [http-api.md](./http-api.md), and pins the strings the reference renders so the parity tests
have one list to check.

## 1. Page lifecycle

| Moment | Content column | Rail |
| --- | --- | --- |
| Before `GET /api/dashboard` answers | "Reading your training history…" | As-of: the browser's local date and its ISO week, as the reference's `DateTime.Now` fallback. Max heart rate: **`—`** (research R4). Sync panel from `GET /api/sync/status`. |
| Answer with `isUnavailable` | "Data unavailable" + the reference paragraph | Values from the response |
| Answer with `!hasActivities` | "No activities recorded" + the connected or unconnected paragraph (+ "Connect Strava" link when unconnected) | Values from the response |
| Answer, populated | Metric row, chart, recent activities | Values from the response |
| Request failed (network or 5xx) | "Data unavailable" + the reference paragraph (009 FR-016). It never shows zeros or stale figures. | Values from the last good response if any, otherwise the loading-state rail |

The page title (`document.title`) is **Training Load** on `/`, **Not found** on any other path, and
**Error** in the error-page state.

## 2. Sync interaction

1. The athlete presses **Sync Activities**. The button immediately becomes disabled and reads
   **Syncing…**, and the message reads **Syncing activities…** (research R3(2), R9).
2. `POST /api/sync` resolves. The panel renders the returned status, then the page re-fetches
   `GET /api/dashboard`. The view is replaced, never patched.
3. A tab loaded while a sync runs shows the running state from `GET /api/sync/status`, and keeps
   it until reloaded (no polling, research R9).
4. `needsConnection` renders a **Connect Strava** link to `/connect`. It is a full navigation,
   not a client route.

## 3. Window control

`<input type="radio" name="window">` × 3 (30, 90, 180 days, labelled `{n} days`), inside
`role="radiogroup" aria-label="Chart window"`. Changing it re-slices `days` on the client, with no
request. The chart heading reads `Daily load and metrics · last {n} days`.
`hasEnoughHistoryForChart` comes from the server and does not depend on the window.

## 4. Chart

A port of `MetricsChartView.razor`, with identical markup structure and classes so the copied CSS
applies unchanged:
- `svg.plot` with `viewBox="0 0 1000 300"` and `preserveAspectRatio="none"`;
- `g.bars` holding `rect.load-bar`;
- an optional `line.zero-rule`;
- the three `polyline`s painted in the order Form, Fatigue, Fitness;
- `.hover-layer` with one `.day` slot per day;
- `.chart-axis` with 6 ticks.

The hover readout is CSS `:hover` over markup rendered once. **No pointer event handlers** are
attached (009 FR-015), so there is no state and no request. Slots past 55 % of the width get
`opens-left`.

## 5. Strings the reference renders (parity checklist)

Every string below must appear, verbatim, under the condition the reference renders it. The
parity golden (see [parity.md](./parity.md)) is authoritative. This list is for the reviewer.

- **Rail:**
  - "Training Load" (the page's one `h1`);
  - the region headings "As of", "Window", "Strava", "Max heart rate";
  - "ISO week {isoWeek}";
  - "{n} days";
  - "{value} bpm", "from configuration";
  - "Sync Activities" or "Syncing…";
  - "Connect Strava";
  - "Last checked {yyyy-MM-dd HH:mm}".
- **States:**
  - "Reading your training history…";
  - "Data unavailable" and "Your stored training history could not be read. Nothing has been lost
    — try again, and if it persists, the application log says why.";
  - "No activities recorded", with either "Your Strava account is connected, but nothing has been
    imported yet. Sync to bring your training in." or "Connect your Strava account to bring your
    training in.".
- **Metric row:**
  - "Fitness", "Fatigue", "Form", "This week";
  - "—";
  - "still settling", "estimated", "partly estimated";
  - "{change} ({percent})";
  - "Significant increase", "Significant decrease", "Steady", "Week in progress".
- **Chart:**
  - "Daily load and metrics · last {n} days";
  - "Fitness", "Fatigue", "Form";
  - "Not enough data to show trends (30+ days required)";
  - `aria-label` "Daily training load with fitness, fatigue and form";
  - readout labels "Fitness", "Fatigue", "Form", "Load".
- **Recent activities:**
  - "Recent activities";
  - "Nothing recorded yet.";
  - column headings "Day", "Type", "Moving time", "Basis", "Load";
  - "Running", "Cycling";
  - "measured", "estimated".
- **Not found:** "Not Found", "Sorry, the content you are looking for does not exist."
- **Error:**
  - page: "Error.", "An error occurred while processing your request.", "Request ID:";
  - boundary: "An unhandled error has occurred.", "Reload", "🗙".
- **Not ported** (research R4, Amendment 1(c)):
  - the circuit-reconnect modal's strings;
  - the Error page's "Development Mode" paragraph.

## 6. Styling

- **One colour owner.** `src/theme/broadsheet.css` is copied from the reference with a provenance
  comment. It is the only file that may name a colour, as a ported `ColourDisciplineTests`
  asserts over `src/**/*.css` and `src/**/*.tsx`.
- **No inline colour.** Per-component CSS files keep the reference's selectors. No inline `style`
  carries a colour; the inline `style`s that exist carry only the hover slots' `left`/`width`/`top`
  percentages, as in the reference.
- **Font.** The same Google Fonts link: Source Serif 4, 400/600, italic 400, `display=swap`.
