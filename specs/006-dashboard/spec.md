# Feature Specification: Dashboard

**Feature Branch**: `006-dashboard`

**Created**: 2026-09-17

**Status**: Draft

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
2. **Given** athlete has activities in both current and previous weeks, **When** dashboard loads, **Then** a trend indicator shows whether this week's load is higher, lower, or equal to last week (at least 5% change is "significant")
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
- **FR-005**: Dashboard MUST display a trend indicator comparing this week's load to last week's load (higher, lower, or equal; significant = ≥5% change)
- **FR-006**: Dashboard MUST display a time-series line chart showing how Fitness, Fatigue, and Form have evolved over time (up to 180 days of history available)
- **FR-007**: Dashboard MUST display the 7 most recent training activities in reverse chronological order (newest first), each showing date, activity type, moving time, and training load
- **FR-008**: Dashboard MUST indicate whether an activity's training load is measured (from heart-rate data) or estimated
- **FR-009**: Dashboard MUST provide a "Sync Activities" button that triggers a manual synchronization with Strava
- **FR-010**: Dashboard MUST display appropriate feedback during sync (loading state, success/failure/rate-limited messages, count of new activities)
- **FR-011**: Dashboard MUST gracefully handle the absence of data (no activities, no history) with a clear empty state and guidance on next steps
- **FR-012**: Dashboard MUST persist on athlete's device (browser session or storage) and restore state on page reload, maintaining display consistency
- **FR-013**: Dashboard MUST display all metrics and activities using only locally-stored data; no external API calls beyond the sync operation itself

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
- **SC-005**: Manual sync completes within 10 seconds for typical history size (up to 2 years of activities) when not rate-limited
- **SC-006**: Dashboard remains accessible and functional even if Strava connection is revoked or network is unavailable (displays cached data with appropriate messaging)
- **SC-007**: All interactions (viewing dashboard, clicking sync button) work identically in all major browsers (Chrome, Firefox, Safari, Edge) without console errors

## Assumptions

- **Athlete authentication**: An athlete has already connected their Strava account (Feature 5 — Strava Import is complete and accounts are persisted)
- **Data availability**: At least one training activity has been imported; if none exist, the feature gracefully handles empty state
- **Browser environment**: The application runs in a modern web browser with JavaScript enabled and supports local storage
- **Display rounding**: Metrics are rounded to 1 decimal place for display (Fitness: 45.3, Fatigue: 12.1, Form: 33.2)
- **Week definition**: ISO 8601 week definition is used (week starts Monday, week 1 is the week with the first Thursday of the year)
- **Chart timespan**: Time-series chart displays all available history up to 180 days (not limited to 180, but 180 is the window used for heart-rate streams in Strava import)
- **Sync initiation**: Manual sync uses the existing `StravaActivitySync.SyncAsync()` method (incremental sync, not full resync)
- **No artificial delays**: UI does not add artificial delays; all waits are due to actual data processing or network latency
- **Blazor Interactive Server**: The application uses Blazor Interactive Server render mode, meaning components run on the server and communicate via SignalR; no separate HTTP API is needed for this MVP
