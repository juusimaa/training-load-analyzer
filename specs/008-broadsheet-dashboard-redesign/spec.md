# Feature Specification: Broadsheet Dashboard Redesign

**Feature Branch**: `008-broadsheet-dashboard-redesign`

**Created**: 2026-09-19

**Status**: Draft (Amendment 1 applied 2026-09-19 — see [Amendments](#amendments))

**Input**: User description: "I added UI design docs (static web page and readme) to \"/docs/ui\" folder. Specify a new feature 008 from these design docs."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - The dashboard reads as a single considered page, not a grid of cards (Priority: P1)

An athlete opens the application and sees their training status laid out like a newspaper page: one standing rail on the left holds every control and piece of context (as-of date, chart window, Strava connection and sync, configured max heart rate), and the content column to its right holds the figures, the chart and the recent-session list, set with a clear type hierarchy and generous whitespace instead of boxes, shadows and dividers separating everything.

**Why this priority**: This is the entire point of the feature. The current interface groups content into elevated cards; this feature replaces that with a rail-plus-column layout and a distinct type scale, matching the reference design.

**Independent Test**: Can be fully tested by loading the populated dashboard and confirming the left rail contains exactly the as-of/window/Strava/max-heart-rate controls and nothing else, the content column contains the metric figures, chart and recent-session table, and no region is presented as a boxed or shadowed card.

**Acceptance Scenarios**:

1. **Given** an athlete with training history, **When** the dashboard loads, **Then** a single persistent rail shows the as-of date with its ISO week, the chart window control, the Strava connection state with a manual sync control, and the configured maximum heart rate — and no other region duplicates or relocates any of these controls
2. **Given** the dashboard is showing all regions, **When** the athlete looks at the metric row, **Then** Fitness, Fatigue, Form and the current week's load appear as large-format figures in one row, with Fitness and Fatigue each in one consistent colour and Form and the week figure in the page's ordinary text colour
3. **Given** the athlete selects a different chart window (30, 90 or 180 days), **When** the selection changes, **Then** the chart, its heading and its date axis update to the newly selected window without a full page reload
4. **Given** the trend chart is showing, **When** the athlete looks at it, **Then** daily load appears as bars behind the Fitness, Fatigue and Form lines, with a zero reference line for Form, and each line is identifiable by name in a legend
5. **Given** the recent-sessions region, **When** it renders, **Then** it shows the seven most recent activities with day, activity type, moving time, load, and whether the load is measured or estimated shown as a tag rather than by colour alone

---

### User Story 2 - Nothing the athlete relied on is lost (Priority: P1)

An athlete who used the previous (Material-styled) dashboard finds every number, label, qualifier, state message and link exactly where it was, saying exactly what it said. The redesign changes how the page looks, not what it says or does.

**Why this priority**: Equal to User Story 1. A visual replacement that silently drops a qualifier — "still settling", "estimated", "measured", a rate-limit message — changes what the athlete believes about their data or their connection. The redesign is only acceptable if it is provably information-preserving.

**Independent Test**: Can be fully tested by comparing the rendered dashboard before and after the change for the same stored history and connection state, and confirming the set of displayed text, figures and destinations is unchanged, and that the existing behavioural test suite passes without weakened assertions.

**Acceptance Scenarios**:

1. **Given** the same stored training history, **When** the dashboard is rendered before and after the redesign, **Then** the same figures appear with the same rounding, and any figure that cannot be computed still renders as an em dash rather than a zero or a blank
2. **Given** a metric backed by fewer than the required days of history, **When** the dashboard renders, **Then** the "still settling" qualifier (or equivalent existing wording) is still present as readable text
3. **Given** an activity whose load was estimated rather than measured, **When** the recent-sessions list renders, **Then** the existing "estimated" / "measured" wording is still present, carried by the tag rather than replaced by it
4. **Given** the athlete starts a sync, **When** the sync is running and when it finishes, **Then** the same states, result wording and last-checked information appear as before, the sync control remains a single button, and it is unavailable while a sync is already running
5. **Given** the trend chart, **When** it renders, **Then** the same three series are plotted from the same stored metrics over the same range, and the legend still names them Fitness, Fatigue and Form
6. **Given** sync fails, requires reconnection, or is rate-limited by Strava, **When** the rail reflects that outcome, **Then** the existing wording for that outcome (including any retry time) still appears

---

### User Story 3 - Every existing state looks like part of the same design, not left behind (Priority: P2)

An athlete who has not yet connected Strava, has no activities yet, whose stored history could not be read, whose Strava credential has been revoked, or whose sync was rate-limited, sees that state presented in the same rail-and-column visual language as the populated dashboard — not in the previous card-based style and not as unstyled text.

**Why this priority**: The reference design in `docs/ui` shows only the populated dashboard; it explicitly leaves these states unaddressed. But the application today already presents all of them, so the redesign must extend its visual language to cover them rather than regress them to a mix of two designs. It ranks below User Stories 1 and 2 because it is a consistency requirement on top of the primary layout, not the primary layout itself.

**Independent Test**: Can be fully tested by loading the application with no stored activities (with and without a connected account), with an unreadable store, with a revoked Strava credential, with a rate-limited sync, and while history is still loading, and confirming each state uses the new type scale, spacing and rail layout rather than the previous card styling.

**Acceptance Scenarios**:

1. **Given** no activities have been imported and no account is connected, **When** the dashboard loads, **Then** the content column explains that training needs to be imported and offers a "Connect Strava" action styled as a prominent control
2. **Given** no activities have been imported but an account is connected, **When** the dashboard loads, **Then** the content column explains that a sync will bring training in, without a redundant connect action, and the rail's Strava control still offers the manual sync action
3. **Given** stored history cannot be read, **When** the dashboard loads, **Then** a distinct, clearly marked notice is shown in the content column instead of the figures, chart and session list
4. **Given** the dashboard is still reading stored history, **When** the page first appears, **Then** a loading indication appears in the content column in place of the figures, chart and session list
5. **Given** the stored Strava credential has been revoked, **When** the athlete views the rail, **Then** the Strava control shows that reconnection is required and offers a control to reconnect
6. **Given** Strava has rate-limited the last sync attempt, **When** the athlete views the rail, **Then** it states that sync is rate-limited and, where a retry time is known, states when sync can be attempted again

---

### User Story 4 - Usable on the device the athlete has (Priority: P2)

An athlete checking their form on a phone sees the rail and content column stack into a single readable column, with no sideways scrolling and controls large enough to tap accurately. On a desktop, the same content keeps the rail fixed alongside a content column that does not stretch into an unreadably long line length.

**Why this priority**: Training data is most often checked on a phone. The reference design already defines a stacked layout for narrow viewports, so this is a direct requirement of the new design rather than new scope, but it ranks below the visual system and information-preservation stories.

**Independent Test**: Can be fully tested by rendering the populated dashboard, and each state from User Story 3, at narrow, medium and wide viewport widths and confirming the layout reflows without horizontal scrolling, clipped content or overlapping elements.

**Acceptance Scenarios**:

1. **Given** a viewport 320 pixels wide, **When** the dashboard renders, **Then** the rail and content column stack into a single column with no horizontal page scrolling and no clipped or overlapping elements
2. **Given** a wide desktop viewport, **When** the dashboard renders, **Then** the rail stays fixed in place while the content column scrolls, and the four metric figures sit side by side
3. **Given** any viewport width between the narrowest phone and the widest desktop, **When** the dashboard renders, **Then** the trend chart scales to the available width while remaining legible
4. **Given** a touch device, **When** the athlete taps the sync button, the window control or the connect/reconnect action, **Then** the target is large enough to hit reliably on the first attempt

---

### User Story 5 - The redesign is accessible to everyone who used it before (Priority: P3)

An athlete using a screen reader, keyboard navigation, high browser zoom or with limited colour vision can use the redesigned dashboard at least as well as the previous one. Colour is never the only carrier of meaning, interactive elements show where the keyboard is, and text remains readable against its background in both light and dark appearance.

**Why this priority**: This is a preservation requirement carried over from how the application already behaves, restated because a full visual replacement is the moment most likely to lose it. It sits last because it constrains the redesign rather than defining it.

**Independent Test**: Can be fully tested by inspecting the redesigned dashboard for text contrast, visible keyboard focus, heading structure and non-colour encoding of every distinction the interface draws, in both the light and dark appearance.

**Acceptance Scenarios**:

1. **Given** any text in the redesigned interface, **When** it is measured against its background in either the light or the dark appearance, **Then** it meets at least a 4.5:1 contrast ratio for body text and 3:1 for large text
2. **Given** the dark appearance is active, **When** the trend chart renders, **Then** its three series remain legible against the dark surface
3. **Given** the athlete navigates with a keyboard, **When** focus reaches the sync button, the window control or any link, **Then** a clearly visible focus indicator appears
4. **Given** any distinction the interface draws with colour outside the trend chart — load provenance, trend direction, connection state — **When** colour is disregarded, **Then** the distinction remains discernible through text, shape or a tag
5. **Given** a screen reader user, **When** they traverse the page, **Then** the heading structure describes the rail and the content regions in a sensible order
6. **Given** the athlete's device appearance preference changes between light and dark while the dashboard is open, **Then** the interface follows the change without a reload and without any displayed content changing

---

### Edge Cases

- What happens when a metric has no value at all — does the em dash still occupy the figure's position so the metric row stays aligned?
- What happens when the recent-sessions table holds the maximum of seven rows and an activity type name is unusually long — does the row wrap rather than clip or push the load figure out of its column?
- What happens to the week-load trend caption when there is no previous week to compare against?
- How does the chart look when the athlete has fewer days of history than the chart requires — is the "not enough history yet" message still shown in place of the chart, in the new visual language?
- How does the sync region look while a sync is running, given the button is unavailable and the result text may change length — does the rail change height in a way that shifts the rest of the page?
- Does a region that is separated from the page by whitespace and a rule in the light appearance remain visibly separated in the dark appearance, where a hairline rule may read differently?
- What happens at very high browser zoom (200%) — does the layout degrade to the stacked narrow-viewport arrangement rather than clipping content?
- What happens when the athlete has no maximum heart rate configured — does the rail show a placeholder rather than an empty line or a zero?

## Requirements *(mandatory)*

### Functional Requirements

**Scope guard**

- **FR-001**: The system MUST NOT change any computation, stored data, imported data, navigation destination, or the conditions under which any message, value or state is shown, **except for the three additions named in Amendment 1** (the chart window control, the maximum heart rate shown in the rail, and the ISO week designation). Apart from those, only the presentation of existing content may change.
- **FR-002**: The system MUST continue to display every text string, figure, label, qualifier, message, timestamp and link that the current interface displays, with identical wording and identical numeric formatting.

**Layout**

- **FR-003**: The system MUST present a single persistent rail containing the as-of date and ISO week, the chart window control, the Strava connection state with the manual sync control, and the configured maximum heart rate, with none of these duplicated or relocated outside the rail.
- **FR-004**: The system MUST present Fitness, Fatigue, Form and the current week's load as one row of large-format figures, with Fitness and Fatigue each rendered in one consistent colour distinct from Form and the week figure.
- **FR-005**: The system MUST let the athlete choose between at least a 30-day, a 90-day and a 180-day chart window, and MUST update the chart, its heading and its date axis to the selected window without a full page reload.
- **FR-006**: The system MUST render the trend chart with daily load as bars behind the Fitness, Fatigue and Form lines, a zero reference line for Form, and a legend naming each line.
- **FR-007**: The system MUST present the seven most recent activities as a table with consistent columns for day, activity type, moving time, load, and a tag showing whether the load is measured or estimated.
- **FR-008**: The system MUST render any figure that cannot be computed as an em dash rather than a zero or a blank cell.
- **FR-009**: The system MUST present all athlete-facing states not shown in the reference design — the connect-required empty state, the connected-but-empty state, the data-unavailable state, the loading state, the reconnection-required state, and the rate-limited sync state — in the same rail-and-column visual language defined by FR-003 through FR-008, rather than in the previous card-based styling.
- **FR-010**: The system MUST apply the same visual language to the not-found page and any unhandled-error notice.

**Layout and responsiveness**

- **FR-011**: The system MUST reflow the rail and content column into a single stacked column on narrow viewports, with the rail no longer fixed in place.
- **FR-012**: The system MUST avoid horizontal page scrolling at any viewport width from 320 pixels upward.
- **FR-013**: The system MUST constrain the content column to a maximum comfortable reading width on very wide viewports rather than stretching content across the full window.
- **FR-014**: The system MUST scale the trend chart to the available width at every supported viewport width while keeping it legible.

**Accessibility**

- **FR-015**: The system MUST meet a contrast ratio of at least 4.5:1 for body text and 3:1 for large text and meaningful non-text indicators, against the surface each sits on. The trend chart's daily-load bars are exempt, as a contextual backdrop rather than a required indicator (Amendment 1); the chart's zero rule and its three metric lines are not exempt.
- **FR-016**: The system MUST show a clearly visible focus indicator on every interactive element when it receives keyboard focus.
- **FR-017**: The system MUST provide touch targets of at least 48 by 48 device-independent pixels for every interactive control.
- **FR-018**: The system MUST NOT convey any distinction by colour alone, except the trend chart's series, which are additionally identified by name in the legend.
- **FR-019**: The system MUST preserve a meaningful heading structure that names the rail and the content regions in reading order.

**Theming**

- **FR-020**: The system MUST provide both a light and a dark appearance, each covering every element defined by FR-003 through FR-010.
- **FR-021**: The system MUST select between the two appearances automatically from the device or browser preference, and MUST follow a change to that preference without the athlete reloading the page.
- **FR-022**: The system MUST present identical content in both appearances: no element may be visible in one and absent, illegible or reduced in meaning in the other.
- **FR-023**: The system MUST keep each trend-chart series legible against the plot background in both appearances, meeting the contrast ratio in FR-015 in each.

### Non-Functional Requirements

- **NFR-001**: The redesigned dashboard MUST render its first meaningful content no more slowly than the current interface does for the same stored history, as perceived by the athlete.
- **NFR-002**: The visual system's definitions (colours, type scale, spacing, rules) MUST live in one place, so that a change to one of them takes effect everywhere it is used.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of the text strings, figures, links and state messages present in the current interface are still present after the redesign, verified by comparing rendered output for identical stored history and connection state.
- **SC-002**: The existing behavioural test suite passes with every assertion intact, except assertions that test markup the feature deliberately replaces; any such assertion is rewritten against the new markup to check the same behaviour, or removed with the removal explicitly recorded — no assertion is weakened or left vacuous.
- **SC-003**: Every athlete-facing surface — populated dashboard, connect-required empty state, connected-but-empty state, data-unavailable state, loading state, reconnection-required state, rate-limited state, not-found page, error notice — renders in the rail-and-column visual language, with zero surfaces left in the previous card-based styling.
- **SC-004**: The dashboard renders without horizontal scrolling or clipped content at every viewport width from 320 pixels to 2560 pixels.
- **SC-005**: 100% of text and meaningful indicators meet their required contrast ratio (4.5:1 body text, 3:1 large text and indicators), verified independently in both the light and the dark appearance.
- **SC-006**: 100% of interactive controls present a visible keyboard focus indicator and a touch target of at least 48 by 48 device-independent pixels.
- **SC-007**: Every distinction the interface draws outside the trend chart's plot remains discernible when the page is viewed without colour. Within the plot, each series remains identifiable by name through the legend, and the three lines remain distinguishable from one another by stroke pattern as well as colour.
- **SC-008**: An athlete unfamiliar with the redesign can identify their current Fitness, Fatigue and Form values within 5 seconds of the dashboard loading.
- **SC-009**: Every athlete-facing surface listed in SC-003 renders completely in both the light and the dark appearance, with zero surfaces retaining a colour from the opposite appearance.
- **SC-010**: Switching the device appearance preference while the dashboard is open changes the appearance without a reload and without any loss or change of displayed content.

## Assumptions

- The layout, type treatment, colour use and control placement described in `docs/ui/README.md` and shown in `docs/ui/index.html` are the source of truth for the populated-dashboard target design; where the reference and this specification could be read differently, the reference design's behaviour (as sampled by its inline script) governs.
- The reference design covers only the populated dashboard. This specification extends its visual language, at the level of type scale, spacing, rail-and-column layout and tag-based encoding, to the states the reference does not depict (empty, unconnected, data-unavailable, loading, reconnection-required, rate-limited, not-found, error), following the same precedent set by the current interface's Material-based redesign.
- This redesign replaces the current (Material-based) dashboard visual system in place; the two are not intended to coexist behind a setting.
- "Do not change functionality" is taken strictly, consistent with the current interface's own scope guard: no new behaviour, no removed behaviour, no changed wording, no changed numbers, no new destinations. Purely presentational chrome introduces no new destination or control.
- Only modern evergreen browsers need to be supported.
- The dark appearance follows the device or browser preference only; there is no stored athlete preference and no in-application switch, consistent with current behaviour.
- Printing, offline appearance and internationalisation of the interface are out of scope.
- Verification of visual outcomes is a mix of automated rendering assertions where markup can carry the check and human review where it cannot.

## Amendments

### Amendment 1 — admit three deliberate additions, and exempt the chart's load bars (2026-09-19)

**Requested by**: the developer, during planning, after a pre-implementation audit of the reference
design against this specification.

#### (a) FR-001's scope guard is narrowed to admit three additions

FR-001 was carried over from feature 007, where the instruction genuinely was "change nothing but
the appearance". It was never narrowed to admit the new content that FR-003 and FR-005 — in this
same specification — require. Three items are therefore new athlete-facing behaviour or content,
and are deliberately in scope:

1. **The chart window control** (FR-005). The chart currently renders a fixed range; choosing
   between 30, 90 and 180 days is new behaviour.
2. **The configured maximum heart rate in the rail** (FR-003). The value exists in configuration
   and already feeds load estimation, but is displayed nowhere today. Showing it adds a member to
   the dashboard read model.
3. **The ISO week designation in the rail** (FR-003). The weekly figure is currently labelled
   "This week" with no week identifier.

Nothing else is added. FR-002 — that every existing string, figure, qualifier, message and link
survives with identical wording and formatting — stands unamended.

#### (b) The trend chart's daily-load bars are exempt from FR-015

Measured against the page background, the bars as drawn in the reference design reach 1.33:1 and
the zero rule 1.80:1, against FR-015's 3:1 bar for meaningful non-text indicators. The lightest
step of the design system's neutral ramp that clears 3:1 is the same value as the Form line, so
complying would give the backdrop the weight of a data line and collapse the figure-and-ground
separation the design is built on.

The bars are therefore treated as a contextual backdrop rather than an indicator required to
understand the content: the three metric lines carry the chart's message, the chart is titled with
its window, and the same daily figures are published as text in the recent-sessions table.

**What is explicitly *not* conceded**: the zero rule is darkened to meet 3:1, since it is a genuine
reference indicator and costs nothing visually. The three metric lines are not exempt. Every other
measured shortfall found in the same audit — the primary button label, links, table headings,
muted text, the chart's date axis and the "Form" legend label — is fixed by stepping down the
design system's existing ramps, not by exemption.

#### (c) Chart series regain stroke-pattern differentiation

Feature 007's Amendment 1 recorded the loss of per-series dash patterns as "a real accessibility
regression", forced by the Material chart component drawing every series with a solid stroke.
Returning to a hand-drawn plot removes that constraint, and the reference design dashes the Form
line. SC-007 is amended to require the three lines be distinguishable by stroke pattern as well as
colour, restoring what feature 006 specified.

### Amendment 2 — the read model carries the daily load the chart's bars are drawn from (2026-09-20)

**Raised by**: implementation, at the point of writing the chart geometry.

**The gap**: FR-006 requires the trend chart to render "daily load as bars behind the Fitness,
Fatigue and Form lines". The dashboard read model has no daily-load series. `DashboardView.Metrics`
is a list of `DailyTrainingMetrics`, which carries fitness, fatigue, form, reliability and basis —
and no load. `DashboardView.Recent` carries a load per activity, but only for the seven most recent
sessions, not for the chart's window. So FR-006 as written cannot be satisfied by any arrangement
of the data currently on the view, and the data model's `LoadBars(metrics, width, height)`
signature does not describe a function that could exist.

**The resolution**: `DashboardView` gains `DailyLoad`, the same day-by-day
`IReadOnlyList<DailyTrainingLoad>` the builder already computes on its way to the metrics series,
sliced to exactly the range `Metrics` covers so the two are aligned index for index.

This is a fourth admitted addition alongside Amendment 1(a)'s three, and it is recorded here rather
than taken silently in code (Principle VII). It is narrower than those three: it displays no new
*content* — the bars are required by FR-006, which is unamended — and it introduces no computation.
`DashboardViewBuilder` already calls `TrainingLoadAggregator.AggregateDaily` and already holds the
result; this carries that value onto the view instead of discarding it. There is no new query and
no new arithmetic.

**Alternative rejected**: reconstructing each day's load by inverting the fatigue exponential
moving average (`load = fatigue(t−1) + (fatigue(t) − fatigue(t−1)) / α`). It is exactly invertible
in principle, but it adds arithmetic to the presentation layer that FR-001 forbids, it is
numerically noisy, and it has no answer for the first day of the window — where there is no prior
value to invert from.
