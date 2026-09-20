# Feature Specification: Dashboard

**Feature Branch**: `006-dashboard`

**Created**: 2026-09-17

**Status**: Complete — implemented and signed off 2026-09-20 (see the [completion review](./compliance-review.md))

**Input**: User description: "Feature 6 — Dashboard. The athlete views a dashboard showing current fitness (CTL), fatigue (ATL), form (TSB), weekly training load, load trend indicators, recent activities, and a time-series chart of metrics evolution over time. Built with Blazor Interactive Server."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - View Current Training Status (Priority: P1)

An athlete opens the dashboard and immediately sees their current training state: Fitness, Fatigue, and Form values. These are the three most important metrics for understanding training readiness and should be prominently displayed.

**Why this priority**: This is the core value of the application — understanding training status at a glance. Without this, the dashboard provides no immediate insight.

**Independent Test**: Can be fully tested by opening the dashboard when training history exists, and verifying all three metrics display correctly derived from stored activities.

**Acceptance Scenarios**:

1. **Given** athlete has 60+ days of training history stored, **When** dashboard loads, **Then** Fitness (CTL), Fatigue (ATL), and Form (TSB) values display with current values calculated from the full history
2. **Given** athlete has no training history, **When** dashboard loads, **Then** metrics display as zero or "—" with a message "No activities recorded"
3. **Given** metric values are known and activities match, **When** dashboard loads, **Then** displayed values match calculations within rounding for display (e.g., CTL as single decimal: "45.3")

---

### User Story 2 - View Weekly Load and Trend (Priority: P1)

The athlete sees how much training was completed this week and how it compares to the previous week. A simple trend indicator (up/down/flat) helps them understand if volume is increasing or decreasing.

**Why this priority**: Weekly progression is the second most important question — "am I training more or less than last week?" This drives weekly planning decisions.

**Independent Test**: Can be fully tested by loading the dashboard with multi-week history and asserting that current-week load and week-over-week comparison are correct.

**Acceptance Scenarios**:

1. **Given** athlete has activities in the current week (ISO week), **When** dashboard loads, **Then** weekly load displays the total training load for the current ISO week
2. **Given** athlete has activities in both current and previous weeks, **When** dashboard loads, **Then** a trend indicator shows this week's change against last week and the classification Feature 4 already computes for it — significant increase, significant decrease, steady, or indeterminate
3. **Given** athlete has no activities in the current week, **When** dashboard loads, **Then** weekly load displays zero and trend shows as neutral or "—"

---

### User Story 3 - View Time-Series Chart (Priority: P2)

The athlete sees how their Fitness, Fatigue, and Form have evolved over the last 180 days in a simple line chart. This helps them understand trends over time and see the impact of their training patterns.

**Why this priority**: Visual trends help identify patterns (seasonal, injury recovery, build phases) that raw numbers don't convey. Not essential for immediate usage but critical for longer-term training management.

**Independent Test**: Can be fully tested by loading the dashboard with 180+ days of history and verifying the chart renders with correct data points without network calls.

**Acceptance Scenarios**:

1. **Given** athlete has 30+ days of metric history, **When** dashboard loads, **Then** a line chart displays CTL, ATL, and TSB trends over the available history (up to 180 days)
2. **Given** athlete has less than 30 days of history, **When** dashboard loads, **Then** chart displays whatever history exists or a message "Not enough data to show trends (30+ days required)"
3. **Given** chart is displayed, **When** user hovers over a data point, **Then** a tooltip shows the date and exact values for that day (or a simple legend is present showing what each line represents)

---

### User Story 4 - View Recent Activities (Priority: P2)

The athlete sees a list of their most recent training sessions (last 7–10 activities) with key details: date, type (running/cycling), duration, and training load (measured or estimated).

**Why this priority**: Recent activities provide context for why metrics changed and what type of training the athlete has been doing. Helps answer "what did I do yesterday?"

**Independent Test**: Can be fully tested by loading the dashboard with activity history and asserting that recent activities display in reverse chronological order with all required fields.

**Acceptance Scenarios**:

1. **Given** athlete has stored activities, **When** dashboard loads, **Then** last 7 most recent activities display in reverse chronological order (newest first) with: date, activity type, moving time, and training load
2. **Given** an activity has measured heart-rate load, **When** activity displays, **Then** load is marked as "measured" (e.g., a badge or icon)
3. **Given** an activity has only estimated load, **When** activity displays, **Then** load is marked as "estimated" (e.g., different styling or badge)
4. **Given** athlete has fewer than 7 stored activities, **When** dashboard loads, **Then** all stored activities display

---

### User Story 5 - Manually Trigger a Sync (Priority: P3)

The athlete can click a "Sync Activities" button to fetch new activities from Strava without waiting for an automated sync. This gives them immediate control over when their data is current.

**Why this priority**: Enables athletes to ensure their dashboard is up-to-date before an important training decision. Not essential for every user but valuable for engaged athletes checking their progress frequently.

**Independent Test**: Can be fully tested by clicking the sync button and asserting that a sync is initiated, new activities are fetched, and the dashboard updates with results (success/failure/rate-limited messages).

**Acceptance Scenarios**:

1. **Given** athlete is connected to Strava, **When** "Sync Activities" button is clicked, **Then** sync begins and a loading indicator appears
2. **Given** sync completes successfully, **When** new activities were found, **Then** dashboard updates to show new metrics and activities, and a success message displays ("X activities imported")
3. **Given** sync completes but found no new activities, **When** dashboard updates, **Then** a message displays ("Already up to date")
4. **Given** sync is rate-limited or interrupted, **When** result is received, **Then** an appropriate message displays with retry information (e.g., "Rate limited. Available again at [time]")
5. **Given** athlete attempts to sync when not connected to Strava, **When** button is clicked, **Then** an error message displays directing them to reconnect ("Strava connection required")

---

### Edge Cases

- What happens if stored activities are corrupted or malformed? (Dashboard should display available data gracefully, not crash)
- What if the sync is in progress when the page refreshes? (State should persist and display correctly on reload)
- What if no data exists at all (fresh account, not yet synced)? (Dashboard should display empty state with clear next steps)
- What if the athlete's Strava connection has been revoked? (Dashboard should display a clear message and prompt to reconnect)
- What if metric calculations fail? (Dashboard should show "—" or "Data unavailable" rather than crashing)

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Dashboard MUST display athlete's current Fitness (CTL) value, updated from the complete activity history
- **FR-002**: Dashboard MUST display athlete's current Fatigue (ATL) value, updated from the complete activity history
- **FR-003**: Dashboard MUST display athlete's current Form (TSB) value, updated from the complete activity history
- **FR-004**: Dashboard MUST display the total training load for the current ISO week, calculated from activities within that week
- **FR-005**: Dashboard MUST display a trend indicator comparing this week's load to last week's load, showing the change and the classification **as Feature 4 — Load Trends already computes it** (significant increase, significant decrease, steady, or indeterminate). The dashboard MUST NOT apply a threshold rule of its own; the thresholds are Feature 4's (004 FR-010) and live in the domain
- **FR-005a**: Dashboard MUST show the current ISO week's trend as **indeterminate** while that week is still in progress, because a week the range cuts short is not a like-for-like comparison — this is Feature 4's own rule (004 FR-017), not a display choice
- **FR-006**: Dashboard MUST display a time-series line chart showing how Fitness, Fatigue, and Form have evolved over time (up to 180 days of history available)
- **FR-007**: Dashboard MUST display the 7 most recent training activities in reverse chronological order (newest first), each showing date, activity type, moving time, and training load
- **FR-008**: Dashboard MUST indicate whether an activity's training load is measured (from heart-rate data) or estimated
- **FR-009**: Dashboard MUST provide a "Sync Activities" button that triggers a manual synchronization with Strava
- **FR-010**: Dashboard MUST display appropriate feedback during sync (loading state, success/failure/rate-limited messages, count of new activities)
- **FR-011**: Dashboard MUST gracefully handle the absence of data (no activities, no history) with a clear empty state and guidance on next steps
- **FR-012**: Dashboard MUST restore its state on page reload — the figures, and the outcome and progress of the most recent sync — so that a reload, or a second tab, shows the same thing rather than a blank slate
- **FR-012a**: That state MUST be held by the application rather than by the browser. A reload of a server-rendered page starts a new connection, so anything held only by the previous one is already gone by the time the athlete sees the new page
- **FR-013**: Dashboard MUST display all metrics and activities using only locally-stored data; no external API calls beyond the sync operation itself

#### The athlete's maximum heart rate

- **FR-014**: The application MUST obtain the athlete's maximum heart rate from configuration, because every measured training load is computed from it and no part of the system stores it today (Features 1, 2 and 5 each deliberately declined to own it)
- **FR-015**: The application MUST refuse to start when that value is absent or not a positive number, rather than starting and displaying figures computed from a default nobody chose

#### Connecting a Strava account

- **FR-016**: The application MUST provide a way for the athlete to connect a Strava account: a page or control that sends them to Strava's consent screen, and a callback that completes the exchange using Feature 5's existing authorization code
- **FR-017**: When no Strava account is connected, the dashboard MUST display its empty state with a route to FR-016's connect flow rather than a dead-end message
- **FR-018**: When Strava rejects the stored credential, the dashboard MUST say so and route the athlete to the same connect flow (reconnection)

### Key Entities

- **TrainingActivity**: Represents a single recorded training session (date, type, duration, heart-rate series if available)
- **Fitness (CTL)**: Chronic Training Load — long-term fatigue accumulation derived from all activities over 42 days
- **Fatigue (ATL)**: Acute Training Load — recent fatigue derived from activities over 7 days
- **Form (TSB)**: Training Stress Balance — Fitness minus Fatigue; indicates readiness and recovery status
- **Weekly Load**: Sum of training load for all activities in the current ISO calendar week
- **Trend**: Week-over-week comparison of training load (percentage change)

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Dashboard loads and displays all five main sections (status, weekly load, chart, activities, sync button) within 2 seconds for an athlete with 100+ days of history
- **SC-002**: Displayed metrics (Fitness, Fatigue, Form, weekly load) match values calculated by the domain feature set to within display rounding (e.g., single decimal place)
- **SC-003**: Time-series chart renders correctly with data points for each day in the available history; no data points are skipped or duplicated
- **SC-004**: Recent activities list displays in correct chronological order and all required fields are visible
- **SC-005**: A manual sync of an **already-current** history — a handful of new activities — completes within 10 seconds when not rate-limited. A first import of a multi-year history is explicitly **not** bounded by this criterion: Feature 5 established that it costs more Strava requests than one rate-limit window allows, so it stops and resumes by design, and FR-010's messaging is what covers it
- **SC-006**: Dashboard remains accessible and functional even if Strava connection is revoked or network is unavailable (displays cached data with appropriate messaging)
- **SC-007**: All interactions (viewing dashboard, clicking sync button) work identically in all major browsers (Chrome, Firefox, Safari, Edge) without console errors

## Assumptions

- **Athlete connection**: Feature 5 built the authorization code but no host to run it from, so this feature provides the connect flow itself (FR-016). Once connected, the connection persists and the dashboard is usable offline against stored data
- **Data availability**: At least one training activity has been imported; if none exist, the feature gracefully handles empty state
- **Browser environment**: The application runs in a modern web browser with JavaScript enabled, which Blazor's server-side connection requires. It does **not** require browser storage of any kind (FR-012a)
- **Display rounding**: Metrics are rounded to 1 decimal place for display (Fitness: 45.3, Fatigue: 12.1, Form: 33.2). The decimal point shown is the one in these examples on every machine: the figures are formatted against a fixed culture rather than the server's, so the display does not change with the operating system's locale
- **Week definition**: ISO 8601 week definition is used (week starts Monday, week 1 is the week with the first Thursday of the year)
- **Chart timespan**: The chart covers the **last 180 days**, or all available history when there is less. The cap is not arbitrary: 180 days is the window feature 5 uses for heart-rate streams, so it is exactly the span over which loads are measured rather than estimated. Older training is **not** discarded — it still feeds Fitness and Fatigue, because those accumulate from the first recorded day. It is only the chart that stops at 180 days
- **Sync initiation**: Manual sync uses the existing `StravaActivitySync.SyncAsync()` method (incremental sync, not full resync)
- **Maximum heart rate is one athlete's, set once**: consistent with the MVP's single-athlete scope. Changing it means changing configuration and restarting, and there is no settings screen (FR-014)
- **No artificial delays**: UI does not add artificial delays; all waits are due to actual data processing or network latency
- **Blazor Interactive Server**: The application uses Blazor Interactive Server render mode, meaning components run on the server and communicate via SignalR; no separate HTTP API is needed for this MVP
- **State lives on the server, not in the browser**: the dashboard holds nothing in browser session storage or local storage. Every figure is recomputed from the local database on each load, which is cheap enough to need no cache, and the one piece of genuinely transient state — whether a sync is running and how the last one ended — is held by the application process. Nothing the athlete sees depends on their browser remembering anything (FR-012a)
