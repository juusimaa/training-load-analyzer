# Feature Specification: Re-implementation on an Alternative Stack

**Feature Branch**: `009-python-react-stack` (branched from `new_stack`, not `main`)

**Created**: 2026-09-23

**Status**: Draft (Amendment 1 applied 2026-09-23; see [Amendments](#amendments))

## Clarifications

### Session 2026-09-23

- Q: Does the new implementation reuse the existing local store? → A: No. It has its own empty store, and the athlete reconnects and re-imports (FR-022). Parity is verified on shared fixtures (FR-022a).
- Q: What is the delivery scope? → A: Full parity with features 001–008, User Stories 1–5 (FR-023).

**Input**: User description: "I want to test different stack for this project: Python with FastAPI for the backend and React with TypeScript for the frontend. Since this is a test base branch is current 'new_stack' branch, not main."

## Overview

Features 1–8 delivered the complete MVP on the original stack. This feature rebuilds the same
application on a second stack, as an experiment, so the two can be compared. The athlete should
not be able to tell which implementation they are using from anything the application computes,
stores, says or shows.

"Parity" in this specification means **parity with the behaviour specified in features 001–008
as amended**. The existing implementation is the reference for any detail those specifications
leave to it.

The new stack is the premise of this feature, not a design choice made here, so it is recorded
under [Assumptions](#assumptions) and [Dependencies](#dependencies) and not in the requirements.
The requirements describe only what must still be true once the stack has changed.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - The same figures from the same training (Priority: P1)

An athlete's stored training produces exactly the same training loads, daily and weekly totals,
Fitness, Fatigue, Form, reliability markings, bases and week-over-week trend classifications in the
new implementation as in the existing one.

**Why this priority**: Every other part of the application displays these figures. If the new
implementation computes a different Form value from the same sessions, the experiment has failed,
however good the rest looks. This is also the part that can be ported and verified with no user
interface and no Strava connection, so it is the natural first slice.

**Independent Test**: Run the calculation rules of features 001–004 against the same
purpose-built session sets that the existing test suite uses. Every result must match the
existing implementation's results within that feature's stated tolerance, and every refusal must
happen under the same conditions and name the same rule.

**Acceptance Scenarios**:

1. **Given** a session with a heart-rate series and a maximum heart rate, **When** its load is computed, **Then** the Edwards TRIMP value and its "measured" marking match the existing implementation exactly
2. **Given** a session without heart-rate data, **When** its load is computed, **Then** it equals moving minutes × 2 and is marked "estimated"
3. **Given** a set of sessions across several time zones and a date range, **When** daily and weekly totals are produced, **Then** each day's and ISO week's points, session count and basis match the existing implementation, including zero-load days and weeks extended to whole weeks
4. **Given** a continuous daily load history, **When** Fitness, Fatigue and Form are calculated, **Then** every day matches the existing implementation to within 0.0001 points, with the same reliability and basis
5. **Given** a weekly history, **When** trends are calculated, **Then** each week's absolute change, relative change (or its absence), classification, completeness and basis match the existing implementation
6. **Given** any input the existing implementation refuses (e.g. a range whose end precedes its start, a heart-rate sample outside 20–250 bpm, a history with a gap), **When** it is supplied to the new implementation, **Then** it is refused and the refusal names the same violated rule

---

### User Story 2 - Connect Strava and sync, with the same safeguards (Priority: P1)

An athlete connects their Strava account through Strava's own consent page, and syncs. The sync
imports the same activities, maps them the same way, and stops, resumes, reconciles and reports
under the same rules as the existing implementation.

**Why this priority**: Without import, the application has no data from a real athlete. It shares
top priority with User Story 1, but it can be tested independently of it by using recorded Strava
responses in place of the network.

**Independent Test**: Drive the connect flow and several syncs against recorded, anonymized Strava
responses, with no network, real account or real credentials. The resulting stored sessions, sync
state and sync summaries must match what the existing implementation produces from the same
responses.

**Acceptance Scenarios**:

1. **Given** no connection, **When** the athlete connects and grants every requested access, **Then** the connection is stored durably and survives a restart
2. **Given** the athlete declines access to private activities on Strava's consent page, **When** the callback completes, **Then** the connection is refused, the missing access is named, and the athlete is told what to approve when they reconnect
3. **Given** a connected account, **When** a sync runs, **Then** only running and cycling sport types in the fixed mapping table are imported, heart-rate series are fetched only for activities inside the 180-day measured window, and the summary reports imported, updated, removed and skipped counts, with the reasons
4. **Given** a sync that hits Strava's rate limit partway through, **When** it stops, **Then** every session already stored is kept, no session is removed, the resume point matches what was stored, and the summary states the earliest retry time
5. **Given** a later sync, **When** it runs, **Then** it reads only from the resume point (minus the 7-day look-back) onward and never duplicates a stored session
6. **Given** Strava rejects the stored renewal credential, **When** a sync runs, **Then** it ends with a distinct reconnection-required outcome and leaves stored activities untouched
7. **Given** a sync is already running, **When** another sync is requested, **Then** it is refused

---

### User Story 3 - The Broadsheet dashboard, unchanged (Priority: P1)

An athlete opens the new implementation's dashboard and sees the Broadsheet page from feature 008:
the rail with the as-of date and ISO week, the chart window control, Strava state and sync, and
the maximum heart rate; the row of large Fitness, Fatigue, Form and weekly-load figures; the trend
chart with its load bars, three dashed and solid lines, zero rule, legend and hover readout; and
the seven most recent sessions with their measured/estimated tags. Every word, figure and state is
the same as in the existing implementation.

**Why this priority**: The dashboard is where the athlete sees the product. The feature is only
comparable to the existing implementation if the same page is rebuilt, not a new one designed.

**Independent Test**: Load both implementations against the same stored history and connection
state and compare every displayed string, figure, qualifier, link and state message. Repeat for
each state listed in User Story 4.

**Acceptance Scenarios**:

1. **Given** the same stored history, **When** the dashboard loads in both implementations, **Then** the same figures appear with the same rounding and the same fixed decimal point, and any figure that cannot be computed shows as an em dash
2. **Given** the athlete selects 30, 90 or 180 days, **When** the selection changes, **Then** the chart, its heading and its date axis update without a full page reload
3. **Given** the athlete points at a day on the chart, **When** the readout appears, **Then** it shows the date and that day's Fitness, Fatigue, Form and load as text, with no request to the application per pointer movement
4. **Given** a sync is started, **When** it is running and when it finishes, **Then** the sync control is a single button that cannot be pressed while a sync runs, and the progress and result wording match the existing implementation
5. **Given** the page is reloaded, or opened in a second tab, while or after a sync runs, **When** it loads, **Then** it shows the same figures and the same sync progress or outcome as before, without relying on anything stored in the browser

---

### User Story 4 - Every non-populated state, and every device and appearance (Priority: P2)

The connect-required and connected-but-empty states, the data-unavailable notice, the loading
state, reconnection-required, rate-limited, the not-found page and the error notice all appear as
they do today. The layout still stacks on a phone, and the light and dark appearances still follow
the device setting.

**Why this priority**: These states and constraints are part of the specified behaviour (features
006 and 008) and must not regress. They follow the populated page because they reuse its visual
language.

**Independent Test**: Render each state at 320, 768 and 2560 pixels wide in both appearances, and
check it against feature 008's layout, contrast, focus, touch-target and non-colour requirements.

**Acceptance Scenarios**:

1. **Given** each state listed above, **When** it renders, **Then** it uses the rail-and-column visual language with the existing wording
2. **Given** a 320-pixel viewport, **When** any surface renders, **Then** there is no horizontal scrolling and no clipped or overlapping content
3. **Given** the device switches between light and dark, **When** the dashboard is open, **Then** the appearance follows without a reload and the correct appearance applies from the first paint
4. **Given** keyboard, screen-reader and colour-blind use, **When** the dashboard is used, **Then** it meets feature 008's FR-015 to FR-019 as amended

---

### User Story 5 - A like-for-like comparison of the two stacks (Priority: P3)

The developer can put the two implementations side by side and judge the experiment: the same
behaviour, verified by an equivalent test suite built test-first, with a written comparison of
what the new stack cost and gained.

**Why this priority**: This is the reason for the experiment, but it has value only once User
Stories 1–4 exist.

**Independent Test**: Review the completion report. It must list each behaviour area from features
001–008 with its parity verification, and record the measured comparison points in the
[Success Criteria](#success-criteria-mandatory).

**Acceptance Scenarios**:

1. **Given** the completed feature, **When** the developer reviews it, **Then** every functional requirement of features 001–008 is mapped to at least one test in the new implementation, or listed as deliberately excluded with a reason
2. **Given** both implementations, **When** the developer runs each one's full suite, **Then** both pass, and neither shares production code with the other

---

### Edge Cases

- A floating-point difference between the two stacks rounds a displayed figure differently (e.g. 45.25 → "45.2" in one, "45.3" in the other). Display rounding must follow the rule the existing implementation uses, not whatever the new stack defaults to.
- The machine's or browser's locale uses a comma decimal separator. Every figure and chart coordinate must still render as it does under the existing implementation's fixed-culture formatting.
- A session's start carries a fractional UTC offset (e.g. +05:30 or +05:45). It must still fall on the athlete's local calendar day (the fix from `fix/strava-utc-offset-decimal`).
- An ISO week near a year boundary (week 53, or 1 January falling in week 52 of the previous year) must get the same week designation and totals as in the existing implementation.
- The page is reloaded mid-sync. The state is held by the application, not the browser, so the reload shows the running sync rather than a blank slate.
- The dashboard's content is now delivered to the browser separately from the page. If the browser cannot reach the application, it must show a clearly marked unavailable notice, not a blank page or zeros.
- The maximum heart rate is missing or not a positive number. The application must refuse to start, as today.
- Two browser tabs press Sync at almost the same moment. Exactly one sync runs; the other request is refused and both tabs show the running sync.

## Requirements *(mandatory)*

### Functional Requirements

#### Scope guard

- **FR-001**: The new implementation MUST satisfy every functional requirement of features 001–008 as amended, except requirements that name the original stack's technologies. Those are replaced by FR-018 to FR-021.
- **FR-002**: The new implementation MUST NOT add, remove or change any computation, fixed constant, stored field, athlete-facing text, figure format, destination or state condition compared with the existing implementation. A difference found during implementation MUST be resolved by amending this specification (Principle VII), not in code.
- **FR-003**: The existing implementation MUST remain in the repository, buildable and passing its own test suite, for the whole of this feature, so it can serve as the parity reference.
- **FR-004**: The two implementations MUST NOT share production code or runtime components. Each one MUST be buildable, testable and runnable on its own.

#### Computation parity (features 001–004)

- **FR-005**: The new implementation MUST compute session load (Edwards TRIMP, or moving minutes × 2 when there is no heart-rate data), daily and ISO-weekly totals, Fitness/Fatigue/Form (42- and 7-day constants, true exponential smoothing, zero seed, 42-day warm-up), and weekly trends (0.15 relative and 50-point absolute thresholds, partial weeks indeterminate) exactly as features 001–004 specify.
- **FR-006**: Results MUST match the existing implementation for the same inputs: to within 1e-20 points for loads, totals and absolute changes, and to within 0.0001 points for Fitness, Fatigue and Form (as amended by [Amendment 1(a)](#amendment-1--precision-reference-deviations-and-stack-specific-surfaces-2026-09-23)).
- **FR-007**: The new implementation MUST refuse the same invalid inputs as the existing implementation, and each refusal MUST name the rule that was violated.
- **FR-008**: The calculation rules MUST NOT depend on storage, the network, the current time, or any Strava concept, as features 001–004 require.

#### Import parity (feature 005)

- **FR-009**: The new implementation MUST provide the Strava connect flow, credential storage and renewal, sport-type mapping, heart-rate retrieval rules, incremental sync with its 7-day look-back, reconciliation, rate-limit handling, transient-failure retry, single-sync guard, and sync summary exactly as feature 005 specifies.
- **FR-010**: Credentials MUST NOT appear in logs, console output, sync summaries, anything sent to the browser, or any tracked file.
- **FR-011**: The import integration MUST be testable end to end without a network, a real Strava account or real credentials, and its test data MUST be purpose-built and anonymized.

#### Dashboard parity (features 006 and 008)

- **FR-012**: The new implementation MUST present the Broadsheet dashboard specified by feature 008, including its amendments, with the same layout, content, states, wording, rounding and formatting as the existing implementation.
- **FR-013**: The dashboard MUST display figures from locally stored data only. The only calls to Strava are the connect flow and sync.
- **FR-014**: The dashboard's figures, and the progress and outcome of the most recent sync, MUST be held by the application rather than the browser, so that a reload or a second tab shows the same state (006 FR-012, FR-012a).
- **FR-015**: The chart's hover readout MUST work without contacting the application for each pointer movement, and MUST use only data already on the page.
- **FR-016**: When the browser cannot reach the application, or a request for dashboard data fails, the dashboard MUST show a clearly marked unavailable notice in the Broadsheet visual language, and MUST NOT show zeros, blanks or stale figures presented as current.
- **FR-017**: Figures and chart geometry MUST be formatted independently of the operating system's and the browser's locale, matching the existing implementation's output.

#### The new stack

- **FR-018**: The new implementation MUST refuse to start when the athlete's maximum heart rate is missing or not a positive number (006 FR-015).
- **FR-019**: Configuration secrets (the Strava client secret, and the maximum heart rate) MUST be supplied outside any tracked file, and the repository MUST document how to supply them for the new implementation.
- **FR-020**: The new implementation MUST keep one-way dependencies: the calculation rules depend on nothing else in the application, and Strava-specific code is confined to the import integration (Principles II and V).
- **FR-021**: The new implementation MUST be built test-first (Principle I). Its test suite MUST cover the calculation rules, the import integration at its boundary, the application's service interface, and the dashboard's rendering and states, each tested separately.

#### Scope boundaries

- **FR-022**: The new implementation MUST keep its own local store, starting empty, and MUST NOT read, write or migrate the existing implementation's store. The athlete connects Strava again and re-imports through the new implementation's own sync. On a multi-year history that first import stops at Strava's rate limit and resumes on later syncs, as feature 005 specifies.
- **FR-022a**: Parity (SC-001 to SC-003) MUST be verified against shared, purpose-built fixtures loaded into both implementations, not by comparing the two real stores. Two stores synced on different days can legitimately differ, because the 180-day measured window (005 FR-017a) is counted from each sync's own date.
- **FR-023**: This feature MUST deliver full parity with features 001–008: User Stories 1 to 5 are all in scope, and none is deferred.

### Key Entities *(include if feature involves data)*

The entities are unchanged from features 001–005. They are listed here only to fix the parity
boundary:

- **Training Activity**, **Heart Rate Series**, **Training Load** (with measured/estimated provenance): feature 001
- **Daily / Weekly Training Load** and **Load Basis**: feature 002
- **Daily Training Metrics** (Fitness, Fatigue, Form, reliability, basis): feature 003
- **Weekly Load Trend**: feature 004
- **Strava Connection**, **Access Credentials**, **Imported Session**, **Sync State**, **Sync Result**, **Skipped Activity**: feature 005
- **Dashboard view**: the read model the dashboard displays (features 006 and 008), including the maximum heart rate, the ISO week, the chart window and the daily load series added by 008's amendments. In the new implementation it crosses from the application to the browser, but it MUST carry the same content.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For every purpose-built session set in the existing test suite, 100% of loads, totals and trend changes match the existing implementation exactly, and 100% of Fitness, Fatigue and Form values match to within 0.0001 points.
- **SC-002**: For the same stored history and connection state, 100% of the displayed text, figures, qualifiers, links and state messages are identical between the two implementations, across every surface listed in 008 SC-003.
- **SC-003**: Replaying the same recorded Strava responses produces identical stored sessions, sync state and sync summary counts in both implementations.
- **SC-004**: The dashboard shows its first meaningful content within 2 seconds for a history of 100+ days, and no more slowly than the existing implementation on the same machine, as perceived by the athlete.
- **SC-005**: A sync of an already-current history with a handful of new activities completes within 10 seconds when not rate-limited.
- **SC-006**: All of 008's SC-004 to SC-010 (layout from 320 to 2560 pixels, contrast, focus, 48 × 48 touch targets, no colour-only distinctions, both appearances, live appearance switching) hold for the new implementation.
- **SC-007**: The new implementation's suite passes identically under a comma-decimal locale and under an English locale.
- **SC-008**: The completion report maps every functional requirement of features 001–008 to at least one passing test in the new implementation, or lists it as excluded with a reason, and records for both stacks: test-suite run time, number of runtime dependencies, bytes the browser downloads before first paint, and the developer's assessment of how well strict TDD held.

## Assumptions

- **The stack is given**: the backend is Python with FastAPI and the frontend is React with TypeScript, as the developer stated. The plan chooses the remaining technologies (storage, testing tools, build tooling) under the constitution's Simplicity principle.
- **Separate frontend and backend**: unlike the existing server-rendered implementation, the browser page and the application now communicate over the application's own service interface. That interface is internal to the application, is not a public API, and serves one athlete on the local machine.
- **Single athlete, local use**: as in the MVP, there is one athlete, no user accounts, and the application runs on the developer's machine. No authentication for the application itself is added.
- **Coexistence**: the new implementation lives alongside the existing one on this branch, in its own directories, so the two can be run and compared side by side. Nothing is merged into `main` as part of this feature.
- **Visual source of truth**: `docs/ui` and feature 008 as amended remain the design source. The existing implementation's rendered output is the reference wherever they leave a detail open.
- **The existing implementation is the parity oracle**: where features 001–008 are silent, the existing implementation's observed behaviour is what "the same" means.
- **Modern evergreen browsers only**, as in feature 008.
- **No data carry-over**: the new implementation's history comes only from its own Strava import (FR-022). A side-by-side look at the two real dashboards is a useful sanity check, but it is not the parity test, and a small difference there may come from sync timing rather than a defect (FR-022a).

## Dependencies

- **Constitution amendment — satisfied by constitution v1.1.0 (2026-09-23)**, which adds the "Alternative-stack experiment (feature 009)" constraints. Originally recorded as: the constitution's Technology Constraints section (v1.0.0) requires C#/.NET, ASP.NET Core, Entity Framework Core, Blazor and xUnit, and says a different frontend technology "MUST NOT be introduced without a documented reason". Under its Governance section, the constitution overrides any conflicting specification or plan. It therefore has to be amended, via `/speckit-constitution`, before planning, for example to allow this alternative stack on this experimental branch. Principles I–VII are unaffected and apply unchanged.
- The Strava API application settings already configured for the existing implementation (client ID, client secret, callback domain `localhost`).

## Amendments

### Amendment 1 — precision, reference deviations and stack-specific surfaces (2026-09-23)

**Raised by**: planning, from reading the existing implementation's code and probing its
arithmetic and formatting on this machine ([research.md](./research.md) R1–R4).
**Approved by**: the developer, 2026-09-23.

#### (a) Load precision

FR-006's "exactly" for loads, totals and absolute changes becomes **"to within 1e-20 points"**.

The existing implementation computes loads in .NET `decimal`. That type rounds division to fit a
96-bit coefficient: `1m/60m = 0.0166666666666666666666666667`. It also drops digits when an
addition overflows: `(1m/60m) × 3600m = 60.000000000000000000000000120`. No Python decimal
precision rounds in the same places, so a one-second heart-rate gap produces values that differ
around the 27th significant digit. Copying .NET's arithmetic would make the domain describe
another runtime rather than features 001 and 002.

The difference is about 18 orders of magnitude below display resolution, and below double
precision. Fitness, Fatigue and Form are therefore unaffected. A displayed figure may differ only
at an exact rounding tie that the two stacks' arithmetic places on opposite sides. Any such case
found is listed in the parity report.

Display formatting itself is **not** relaxed. FR-002 and SC-002 still require identical strings.
The new implementation reproduces .NET's formatting rules (research R2).

#### (b) Where the existing implementation departs from features 001–008

The Overview already says parity is with features 001–008 *as specified*. This amendment says how
that applies to three places where the existing implementation does not do what its specification
says. The rule: **the specification wins where meeting it needs no new athlete-facing wording.**
Where meeting it would need wording the existing implementation never defined, the existing
behaviour stands.

1. **Token renewal (005 FR-004).** The existing sync never renews an expired access token, so a
   sync more than six hours after connecting ends reconnection-required. The new implementation
   renews before syncing. **Specification followed.**
2. **In-flight sync state in the tab that started it (006 US5 scenario 1, FR-010; this spec US3
   scenario 4).** The existing page shows "Syncing…" only in tabs opened after the sync began. The
   new implementation shows the existing "Syncing…" / "Syncing activities…" state, with a disabled
   button, in the tab that started it. **Specification followed.**
3. **Connect outcomes (005 FR-002a).** The callback redirects to `/?connect=declined|scope|mismatch`
   and nothing explains the outcome. Explaining it would need new wording.
   **Existing behaviour kept.** The gap stays recorded, as feature 008 recorded it.

Items 1 and 2 are defects in the existing implementation, to be assessed separately against
`main`. Every deviation is listed in the parity report. The parity golden for an expired-token
sync records the existing outcome, and the new implementation's test asserts the specified one.

#### (c) Surfaces that exist only because of the existing stack

FR-001 and FR-002 are narrowed for these surfaces only:

- **Circuit-reconnect dialog** ("Rejoining the server…" and its variants): **not ported.** There
  is no server circuit. A failed request is covered by FR-016's unavailable notice.
- **Unhandled-error notice** ("An unhandled error has occurred." · "Reload" · "🗙"): **ported with
  the same wording.**
- **`/Error` page**: "Error.", "An error occurred while processing your request." and "Request ID"
  are **ported with the same wording**. The "Development Mode" paragraph, which names
  `ASPNETCORE_ENVIRONMENT`, is **not ported**, and no replacement text is added.
- **Rail maximum heart rate while the history loads**: shows **`—`** instead of `0 bpm`, as feature
  008's edge cases ask ("a placeholder rather than an empty line or a zero"). A client that fetches
  its data shows the loading state long enough for the `0` to be seen.

Nothing else is added or removed.
