# Feature Specification: Material Design Visual Refresh

**Feature Branch**: `007-material-design-ui`

**Created**: 2026-09-18

**Status**: Draft

**Input**: User description: "Current implmentation is not visually very pleasing and that has to change. Do not change functionalities, only UI should changes. Mateial Design principles and looks."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - The dashboard looks like a designed product (Priority: P1)

An athlete opens the application and sees their training status presented as a deliberate, modern interface rather than a wall of browser-default text. Fitness, Fatigue and Form read as three distinct cards; the weekly load, the trend chart and the recent-activity list each sit in their own clearly bounded region; headings, figures and secondary notes are visibly different in weight and size; spacing is even and rhythmic rather than accidental.

**Why this priority**: This is the entire point of the feature. The athlete's first impression of the application is the dashboard, and today that impression is "unfinished". Everything else in this feature is a refinement of this one outcome.

**Independent Test**: Can be fully tested by loading the dashboard with a populated training history and confirming that every region (status tiles, weekly load, chart, recent activities, sync controls) is visually separated, hierarchically typeset and consistently spaced — and that the same figures appear as before the change.

**Acceptance Scenarios**:

1. **Given** an athlete with training history, **When** the dashboard loads, **Then** Fitness, Fatigue and Form each appear in their own visually bounded container with the metric's value as the dominant element and its name as a smaller, secondary label
2. **Given** the dashboard is showing all regions, **When** the athlete looks at the page, **Then** the regions are separated by consistent, repeating spacing and each region is distinguishable from the page background by a surface treatment (shade, outline or shadow)
3. **Given** the dashboard is showing all regions, **When** headings, values, labels and supporting notes are compared, **Then** they follow a single consistent set of text sizes and weights, with no more than one style serving each role
4. **Given** any two places that convey the same kind of information (for example any two numeric figures, or any two secondary notes), **When** they are compared, **Then** they are styled identically
5. **Given** a device set to a dark appearance, **When** the dashboard loads, **Then** it renders in the dark colour scheme with the same regions, hierarchy and spacing as the light scheme, and no surface retains a light-scheme colour
6. **Given** the dashboard is open, **When** the device appearance preference changes between light and dark, **Then** the interface follows the change without the athlete reloading the page and without any displayed content changing

---

### User Story 2 - Nothing the athlete relied on is lost (Priority: P1)

An athlete who used the previous version finds every number, label, note, state message and link exactly where it was, saying exactly what it said. The application behaves identically: the same button starts a sync, the same link connects Strava, the same values are computed and displayed with the same rounding and the same wording.

**Why this priority**: Equal to US1 in priority because a visual refresh that quietly drops a caveat — "still settling", "estimated", "measured" — changes what the athlete believes about their data. The redesign is only acceptable if it is provably information-preserving.

**Independent Test**: Can be fully tested by comparing the rendered dashboard before and after the change for the same stored history and confirming that the set of displayed text, values and destinations is unchanged, and that the existing behavioural test suite passes without modification to its assertions.

**Acceptance Scenarios**:

1. **Given** the same stored training history, **When** the dashboard is rendered before and after the redesign, **Then** the same figures appear with the same rounding and the same placeholder for missing values
2. **Given** a metric backed by fewer than the required days of history, **When** the dashboard renders, **Then** the "still settling" qualifier is still present as readable text, not replaced by a purely visual cue
3. **Given** an activity whose load was estimated rather than measured, **When** the recent-activity list renders, **Then** the "estimated" / "measured" wording is still present as readable text alongside any visual treatment
4. **Given** the athlete starts a sync, **When** the sync is running and when it finishes, **Then** the same states, messages and last-checked timestamp appear as before, and the control that starts a sync remains a single button that is unavailable while a sync runs
5. **Given** the trend chart, **When** it renders, **Then** the same three series are plotted over the same range with the same legend wording and the same non-colour differentiation between series

---

### User Story 3 - Every state looks designed, not just the happy path (Priority: P2)

An athlete who has just installed the application, or whose data could not be read, or who has not yet connected Strava, sees a considered empty or error state rather than a bare paragraph — a clear heading, an explanation, and where an action exists, a prominent, obviously clickable control.

**Why this priority**: These states are the athlete's first and worst moments with the application. They are not the primary journey, so they rank below US1 and US2, but leaving them unstyled would undo the impression the redesign creates.

**Independent Test**: Can be fully tested by loading the application with no stored activities, with an unreadable store, and with an unconnected account, and confirming each state presents a styled container, a heading, explanatory text and — where one exists — a prominent action control.

**Acceptance Scenarios**:

1. **Given** no activities have been imported and no account is connected, **When** the dashboard loads, **Then** an empty state appears with a heading, an explanation and a visually prominent "Connect Strava" action styled as a primary action control
2. **Given** no activities have been imported but an account is connected, **When** the dashboard loads, **Then** an empty state appears explaining that a sync will bring training in, without offering a redundant connect action
3. **Given** stored history cannot be read, **When** the dashboard loads, **Then** the unavailable state is presented as a distinct, visually marked notice rather than ordinary body text
4. **Given** the dashboard is still reading stored history, **When** the page first appears, **Then** a loading indication appears in place of the content regions rather than an empty page or a bare sentence
5. **Given** the athlete navigates to an address that does not exist, or an unhandled error notice is shown, **When** that page renders, **Then** it uses the same visual language as the rest of the application

---

### User Story 4 - Usable on the device the athlete has (Priority: P2)

An athlete checking their form on a phone after a session sees a layout that reflows to a single readable column, with no sideways scrolling, text that stays legible without zooming, and controls large enough to tap accurately. On a desktop the same content uses the extra width without stretching into an unreadable line length.

**Why this priority**: Training data is most often checked on a phone, but the current layout has no responsive intent at all. It ranks below the core visual refresh only because a desktop-correct redesign is already a large improvement.

**Independent Test**: Can be fully tested by rendering the populated dashboard at narrow, medium and wide viewport widths and confirming the layout reflows without horizontal scrolling and without clipped or overlapping content.

**Acceptance Scenarios**:

1. **Given** a viewport 320 pixels wide, **When** the dashboard renders, **Then** all content is readable in a single column with no horizontal page scrolling and no clipped or overlapping elements
2. **Given** a wide desktop viewport, **When** the dashboard renders, **Then** the three status metrics sit side by side and the content is constrained to a comfortable reading width rather than spanning the full window
3. **Given** any viewport width between the narrowest phone and the widest desktop, **When** the dashboard renders, **Then** the trend chart scales to the available width while remaining legible
4. **Given** a touch device, **When** the athlete taps the sync button or the connect link, **Then** the target is large enough to hit reliably on the first attempt

---

### User Story 5 - The redesign is accessible to everyone who used it before (Priority: P3)

An athlete using a screen reader, keyboard navigation, high browser zoom or with limited colour vision can use the refreshed interface at least as well as the previous one. Colour is never the only carrier of meaning, interactive elements show where the keyboard is, and text remains readable against its background.

**Why this priority**: The previous implementation deliberately expressed qualifiers as text rather than styling. A redesign is the most likely moment to lose that, so it is stated explicitly — but it is a preservation requirement rather than a new capability, so it sits last.

**Independent Test**: Can be fully tested by inspecting the rendered dashboard for text contrast, visible keyboard focus, heading structure and non-colour encoding of every distinction the interface draws.

**Acceptance Scenarios**:

1. **Given** any text in the refreshed interface, **When** it is measured against its background in either the light or the dark scheme, **Then** it meets at least a 4.5:1 contrast ratio for body text and 3:1 for large text
2. **Given** the dark scheme is active, **When** the trend chart renders, **Then** its three series remain legible against the dark surface and remain distinguishable from one another without relying on colour
3. **Given** the athlete navigates with a keyboard, **When** focus reaches the sync button or any link, **Then** a clearly visible focus indicator appears
4. **Given** any distinction the interface draws with colour — chart series, load provenance, trend direction — **When** colour is disregarded, **Then** the distinction remains discernible through text, shape or pattern
5. **Given** a screen reader user, **When** they traverse the page, **Then** the heading structure describes the regions of the dashboard in a sensible order

### Edge Cases

- What happens when a metric has no value at all — does the placeholder still occupy the card so the three status cards stay aligned?
- What happens when an activity type name is unusually long, or when the recent-activity list holds the maximum of seven entries — does a row wrap rather than clip or push its load figure off the edge?
- What happens when a metric carries two qualifiers at once ("still settling" and "partly estimated") — do both remain visible without breaking the card's layout?
- How does the interface behave at 200% browser zoom — does it degrade to the narrow layout rather than clipping content?
- What happens to the weekly trend when there is no previous week to compare against and the change reads as a placeholder — does the panel still look complete?
- How does the sync region look while a sync is running, given the button is unavailable and a message may or may not be present — does the region change height in a way that makes the page jump?
- What happens when the athlete has fewer than the days required for the chart and the chart region shows its "not enough data" message instead — is that region still a designed surface rather than a gap?
- How does the interface appear to someone who has asked their system to reduce motion, if any transitions are introduced?
- What happens when the athlete changes their device appearance preference while the dashboard is open mid-sync — does the scheme follow without disturbing the running sync or the displayed content?
- How do the chart's series colours read against a dark surface, given they were chosen against a light one?
- Does a surface that relies on a shadow for separation in the light scheme remain visibly separated in the dark scheme, where shadows read weakly?

## Requirements *(mandatory)*

### Functional Requirements

**Scope guard**

- **FR-001**: The system MUST NOT change any computation, stored data, imported data, navigation destination, or the conditions under which any message, value or state is shown. Only the presentation of existing content may change.
- **FR-002**: The system MUST continue to display every text string, figure, label, qualifier, message, timestamp and link that the current interface displays, with identical wording and identical numeric formatting.
- **FR-003**: The system MUST NOT add new pages, new destinations, new athlete-facing settings, or new interactive controls beyond restyling those that already exist.

**Visual system**

- **FR-004**: The system MUST apply a single defined visual system based on Material Design across every athlete-facing surface, comprising a named colour scheme, a text size and weight scale, a spacing scale, a corner-shape scale and a surface-elevation treatment.
- **FR-005**: The system MUST derive all colours used in the interface from that defined colour scheme, such that no surface, text or control introduces a colour outside it.
- **FR-006**: The system MUST apply spacing between and within regions from the defined spacing scale only, so that gaps repeat rather than vary arbitrarily.
- **FR-007**: The system MUST present each dashboard region — status metrics, weekly load, trend chart, recent activities, sync controls — as a distinct surface separated from the page background.
- **FR-008**: The system MUST present Fitness, Fatigue and Form as three peer surfaces in which the numeric value is the visually dominant element and the metric name and any qualifier are subordinate to it.
- **FR-009**: The system MUST render the sync control as a Material-style primary action button with visually distinct resting, hovered, focused and unavailable appearances.
- **FR-010**: The system MUST render the "Connect Strava" action as a prominent action control rather than an unstyled inline link.
- **FR-011**: The system MUST present the recent-activity list as evenly structured rows in which the day, activity type, duration, load and provenance occupy consistent positions across all rows.
- **FR-012**: The system MUST present the load-provenance and reliability qualifiers with a visual treatment (such as a chip or badge) that supplements, and never replaces, their existing text.
- **FR-013**: The system MUST restyle the trend chart's plot, legend, date range and insufficient-data message to the same visual system, preserving the existing series colours' distinguishability and the existing non-colour differentiation between series.
- **FR-014**: The system MUST present empty, loading and data-unavailable states as designed surfaces with a heading, explanatory text and, where an action exists, a prominent action control.
- **FR-015**: The system MUST apply the same visual system to the not-found page, the unhandled-error notice and the connection-lost notice.

**Layout and responsiveness**

- **FR-016**: The system MUST lay out the dashboard responsively, reflowing to a single column on narrow viewports and to a multi-column arrangement of the three status metrics on wide viewports.
- **FR-017**: The system MUST avoid horizontal page scrolling at any viewport width from 320 pixels upward.
- **FR-018**: The system MUST constrain the content to a maximum comfortable reading width on very wide viewports rather than stretching content across the full window.

**Accessibility**

- **FR-019**: The system MUST meet a contrast ratio of at least 4.5:1 for body text and 3:1 for large text and meaningful non-text indicators, against the surface each sits on.
- **FR-020**: The system MUST show a clearly visible focus indicator on every interactive element when it receives keyboard focus.
- **FR-021**: The system MUST provide touch targets of at least 48 by 48 device-independent pixels for every interactive control.
- **FR-022**: The system MUST NOT convey any distinction by colour alone.
- **FR-023**: The system MUST preserve a meaningful heading structure that names the regions of the dashboard in reading order.
- **FR-024**: The system MUST honour a reduced-motion preference by suppressing any non-essential animation or transition it introduces.

**Theming**

- **FR-025**: The system MUST provide both a light and a dark colour scheme, each a complete set of the colour roles defined in FR-004.
- **FR-026**: The system MUST select between the two schemes automatically from the device or browser appearance preference, and MUST follow a change to that preference without the athlete reloading the page.
- **FR-027**: The system MUST NOT offer an in-application control for choosing the scheme, since that would be new functionality (FR-003).
- **FR-028**: The system MUST present identical content in both schemes: no element may be visible in one scheme and absent, illegible or reduced in meaning in the other.
- **FR-029**: The system MUST keep the trend chart's series legible and mutually distinguishable in both schemes, preserving the non-colour differentiation required by FR-013 in each.

### Non-Functional Requirements

- **NFR-001**: The refreshed interface MUST render its first meaningful content no more slowly than the current interface does for the same stored history, as perceived by the athlete.
- **NFR-002**: The visual system's definitions (colours, sizes, spacing, shapes) MUST live in one place, so that a change to a colour or a spacing step takes effect everywhere it is used.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of the text strings, figures, links and state messages present in the current interface are still present after the redesign, verified by comparing rendered output for identical stored history.
- **SC-002**: The existing behavioural test suite passes without any assertion being weakened or removed to accommodate the new presentation.
- **SC-003**: Every athlete-facing surface — populated dashboard, empty state, unconnected state, data-unavailable state, loading state, not-found page, error notice — renders with the shared visual system, with zero surfaces left in browser-default styling.
- **SC-004**: The dashboard renders without horizontal scrolling or clipped content at every viewport width from 320 pixels to 2560 pixels.
- **SC-005**: 100% of text and meaningful indicators meet their required contrast ratio (4.5:1 body text, 3:1 large text and indicators), verified independently in both the light and the dark scheme.
- **SC-006**: 100% of interactive controls present a visible keyboard focus indicator and a touch target of at least 48 by 48 device-independent pixels.
- **SC-007**: Every distinction the interface draws remains discernible when the page is viewed without colour.
- **SC-008**: An athlete unfamiliar with the application can identify their current Fitness, Fatigue and Form values within 5 seconds of the dashboard loading.
- **SC-009**: All colour, text-size, spacing and shape values used in the interface trace to the single defined visual system, with zero one-off values introduced at the point of use.
- **SC-010**: Every athlete-facing surface listed in SC-003 renders completely in both the light and the dark scheme, with zero surfaces retaining a colour from the opposite scheme.
- **SC-011**: Switching the device appearance preference while the dashboard is open changes the scheme without a reload and without any loss or change of displayed content.

## Assumptions

- The feature description's "Material Design" is taken to mean the current generation of Material Design (Material 3) as a set of visual principles — colour roles, a type scale, a spacing rhythm, elevated and shaped surfaces, and standard action-control treatments. Pixel-exact conformance to any published specification is not required; the goal is that the result reads as Material.
- "Do not change functionalities" is taken strictly: no new behaviour, no removed behaviour, no changed wording, no changed numbers, no new destinations. Purely presentational chrome (for example a titled application bar carrying the existing page title) is considered presentation, not functionality, and is permitted so long as it introduces no new destination or control.
- The athlete-facing surfaces in scope are exactly those that exist today: the dashboard and all of its states, the not-found page, the unhandled-error notice and the connection-lost notice. The Strava connect flow itself is a redirect with no page of its own and needs no visual work beyond the dashboard entry points that lead into it.
- The existing chart series colours were chosen for colour-blind distinguishability and their existing dash patterns carry the same distinction without colour. They are assumed to be kept, or replaced only by values that preserve both properties.
- The existing rule that qualifiers such as "still settling", "estimated" and "measured" are expressed as text is a deliberate accessibility decision and is assumed to remain binding; visual badges may only supplement that text.
- Only modern evergreen browsers need to be supported. No legacy browser support is assumed.
- The dark scheme follows the device or browser appearance preference only. There is no stored athlete preference, no per-device override and no in-application switch, because a switch would be new functionality (FR-027).
- Where a light-scheme surface is separated from its background by a shadow, the dark scheme is assumed to achieve the same separation by another means from the same visual system (for example a lighter surface tone or an outline), rather than by a stronger shadow.
- Printing, offline appearance and internationalisation of the interface are assumed out of scope.
- Verification of visual outcomes is assumed to be a mix of automated rendering assertions where markup can carry the check and human review where it cannot; no screenshot-comparison tooling is assumed to be introduced.
