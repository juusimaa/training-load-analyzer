# Feature Specification: Training Load Aggregation

**Feature Branch**: `002-training-load-aggregation`

**Created**: 2026-09-16

**Status**: Complete — implemented and signed off 2026-09-20

**Input**: User description: "Training Load Aggregation: aggregate the training-load values of many recorded training activities into daily and weekly totals over a requested date range. Builds directly on the existing Training Activity Domain (feature 001), where a TrainingActivity already exposes a start time with timezone offset, a moving time, an activity type, and a TrainingLoad value carrying points plus a provenance of measured or estimated. This feature adds pure domain aggregation: given a collection of activities and a date range, produce the total load per calendar day and the total load per ISO-8601 week (Monday to Sunday), including days and weeks inside the range that contain no activities, so that a continuous, gap-free series is available for later CTL/ATL/TSB and trend calculations. It must handle activities that fall on a day or week boundary, activities outside the requested range (excluded), an empty activity collection, and multiple activities on the same day (summed). The aggregate must also make visible whether a day's or week's total was derived wholly from measured load, wholly from estimated load, or a mix, because a total combining estimates is less trustworthy than one built only from heart-rate-measured sessions. Open questions that must be marked as such rather than invented: which timezone determines the calendar day an activity belongs to (the activity's own recorded UTC offset versus a single athlete-level timezone), and how a requested range that is not aligned to whole ISO weeks should report its first and last partial week. The domain must not reference Strava or any other external activity provider. Out of scope: fetching or persisting activities, CTL/ATL/TSB calculations, week-over-week trend or anomaly detection, any API endpoint or UI."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - See how much training each day carried (Priority: P1)

An athlete has a set of recorded sessions and wants to see, day by day over a chosen stretch of
time, how much training each day actually carried. Two easy sessions on the same day should add up
into that day's figure, and a day the athlete rested should read as a real zero rather than
vanishing from the picture. Without this, there is no daily series for fitness, fatigue, form, or
trends to be computed from later.

**Why this priority**: This is the smallest slice that delivers standalone value — the athlete can
already read their daily training load — and every later capability in the MVP consumes exactly
this series. Weekly totals are derived from the same day assignment, so getting the day right first
is what makes the rest trustworthy.

**Independent Test**: Aggregate a hand-built set of sessions with known loads over a known range,
and check each day's total against a figure worked out by hand; include a day with several
sessions, a day with none, and days at both ends of the range.

**Acceptance Scenarios**:

1. **Given** three sessions on 2026-03-02 with loads of 30, 40, and 50 points, **When** daily loads
   are aggregated over a range covering that day, **Then** 2026-03-02 reports a total of 120 points.
2. **Given** a single session on 2026-03-02 with a load of 30 points and a requested range of
   2026-03-01 to 2026-03-05, **When** daily loads are aggregated, **Then** exactly five daily
   entries are produced, one per calendar day in order, and 2026-03-01, 03-03, 03-04, and 03-05
   each report a total of 0 points.
3. **Given** a requested range of 2026-03-01 to 2026-03-05 and a session on 2026-02-28 and another
   on 2026-03-06, **When** daily loads are aggregated, **Then** neither session contributes to any
   daily total, and no daily entry outside the requested range is produced.
4. **Given** a session whose start falls on the first day of the requested range and another whose
   start falls on the last day, **When** daily loads are aggregated, **Then** both are included —
   the range includes both of its endpoint days in full.
5. **Given** no sessions at all and a requested range of 2026-03-01 to 2026-03-05, **When** daily
   loads are aggregated, **Then** five daily entries are produced, each reporting 0 points, and the
   result is not an error or an empty list.
6. **Given** a set of sessions supplied in an arbitrary order, **When** daily loads are aggregated,
   **Then** the daily entries are returned in ascending date order and each day's total is
   unaffected by the order the sessions were supplied in.
7. **Given** the same sessions and the same requested range, **When** daily loads are aggregated
   repeatedly, **Then** the same totals are produced every time, with no dependence on the current
   date or on any external service.

---

### User Story 2 - See how much training each week carried (Priority: P2)

The athlete plans and reviews training in weeks rather than days: a hard week followed by an easy
week, a build block, a taper. They need each week in the chosen range totalled, with weeks running
Monday to Sunday, and with a week containing no training reported as a real zero week.

**Why this priority**: Weekly load is the unit the athlete actually reasons about, and it is what
the later trend and spike-detection feature compares week against week. It is worth less on its own
than Story 1 only because it is derived from the same day assignment.

**Independent Test**: Aggregate a hand-built set of sessions spanning several ISO weeks and check
each week's total by hand, including sessions placed either side of a Monday boundary and a week
with no sessions at all.

**Acceptance Scenarios**:

1. **Given** sessions with loads 30, 40, and 50 falling in the same ISO week, **When** weekly loads
   are aggregated, **Then** that week reports a total of 120 points.
2. **Given** a session on Sunday 2026-03-01 and a session on Monday 2026-03-02, **When** weekly
   loads are aggregated, **Then** they fall in two different weeks — the week boundary is Monday,
   and Sunday belongs to the week that precedes it.
3. **Given** a requested range spanning four ISO weeks in which the third week contains no sessions,
   **When** weekly loads are aggregated, **Then** four weekly entries are produced in ascending
   order and the third reports a total of 0 points.
4. **Given** a requested range covering whole ISO weeks only, **When** both daily and weekly loads
   are aggregated, **Then** each week's total equals the sum of the daily totals of its seven days —
   the two views never disagree.
5. **Given** a requested range of Wednesday 2026-03-04 to Thursday 2026-03-05 and a session on
   Monday 2026-03-02 carrying 60 points, **When** weekly loads are aggregated, **Then** a single
   weekly entry is produced for the whole week of Monday 2026-03-02 to Sunday 2026-03-08 and it
   includes those 60 points, **And** the daily series still contains only 2026-03-04 and
   2026-03-05, neither of which includes them.
6. **Given** a session in the days around 1 January, **When** weekly loads are aggregated, **Then**
   it is assigned to its ISO-8601 week, which may belong to the ISO week-numbering year before or
   after the calendar year its date falls in.

---

### User Story 3 - Know how much to trust a total (Priority: P3)

Some sessions carry heart-rate data and their load is measured; others do not and their load is
only estimated from duration. When the athlete reads a day or a week total, they need to know which
kind of figure they are looking at, because a week built entirely from estimates is a much weaker
basis for a training decision than one built from measured sessions — and a week that mixes the two
is weaker still to compare against its neighbours.

**Why this priority**: It qualifies the numbers from Stories 1 and 2 rather than producing new ones,
but without it the athlete cannot tell a solid week from a guessed one, and the eventual trend
feature would compare the two as if they were equivalent.

**Independent Test**: Aggregate days and weeks built from measured sessions only, from estimated
sessions only, from a mix of both, and from no sessions at all, and confirm each reports the
corresponding basis.

**Acceptance Scenarios**:

1. **Given** a day whose sessions all carry measured loads, **When** that day is aggregated,
   **Then** its total is reported as measured.
2. **Given** a day whose sessions all carry estimated loads, **When** that day is aggregated,
   **Then** its total is reported as estimated.
3. **Given** a day with one measured session and one estimated session, **When** that day is
   aggregated, **Then** its total is reported as mixed — not as measured, and not as estimated.
4. **Given** a day with no sessions, **When** that day is aggregated, **Then** its total of 0 points
   is reported as having no basis, distinguishable from a real 0-point measured day.
5. **Given** a week containing one estimated session and several measured ones, **When** that week
   is aggregated, **Then** the week is reported as mixed — a single estimate qualifies the whole
   week's total.
6. **Given** any aggregated day or week, **When** its total is read, **Then** the number of sessions
   contributing to it is also readable, so that a 0-point day with sessions in it is
   distinguishable from a rest day.

---

### Edge Cases

- A requested range covering exactly one day.
- A requested range whose end date precedes its start date.
- A requested range with no bounds, or with a missing start or end date.
- An implausibly long requested range (for example several decades), where a gap-free daily series
  becomes very large.
- A session starting at exactly 00:00 local time, and one starting at exactly 23:59.
- A session starting at exactly 00:00 on Monday, the first instant of an ISO week.
- A session whose start time carries a UTC offset that places it on a different calendar day than
  the same instant would fall on in UTC.
- Two sessions on the same local day whose start times carry different UTC offsets — for example
  the athlete travelled between time zones.
- A session starting during a daylight-saving transition.
- A session in the days around 1 January, where the ISO week-numbering year differs from the
  calendar year.
- A range whose first day is not a Monday and whose last day is not a Sunday, so its first and last
  ISO weeks are only partly covered.
- A range that starts and ends inside the same ISO week.
- A collection of sessions that is empty, and one that is absent altogether.
- A collection containing two sessions with the same external identifier — whether they are one
  session counted twice or two genuinely distinct records.
- Sessions whose loads sum to a total large enough to lose precision if the points were held
  imprecisely.
- A day or week whose sessions all carry a load of exactly 0 points, which must remain
  distinguishable from a day or week with no sessions.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST aggregate the training loads of a supplied collection of recorded
  sessions into a total per calendar day across a requested date range.
- **FR-002**: The system MUST aggregate the training loads of the same sessions into a total per
  ISO-8601 week, where a week runs Monday through Sunday.
- **FR-003**: The system MUST attribute each session wholly to the single calendar day on which the
  session started. A session MUST NOT be split across days, because a session record carries a start
  time and a moving time but no end time, and splitting it would require inventing one.
- **FR-004**: The system MUST determine the calendar day a session belongs to from the UTC offset
  recorded on that session itself — the athlete's own local calendar day at the moment the session
  started. A single athlete-level timezone MUST NOT be applied to every session, and the recorded
  offset MUST NOT be discarded in favour of UTC.
- **FR-005**: The system MUST produce a daily entry for **every** calendar day in the requested
  range, including days on which no session started, so that the resulting series is continuous and
  gap-free.
- **FR-006**: The system MUST produce a weekly entry for every ISO week touched by the requested
  range, each taken as a whole seven-day week per FR-013, including weeks in which no session
  started.
- **FR-007**: The system MUST report a total of 0 points for a day or week in which no session
  started, rather than omitting the entry, reporting an absent value, or failing.
- **FR-008**: The system MUST sum the loads of all sessions attributed to the same day, regardless
  of how many there are and regardless of their activity types — running and cycling loads are
  expressed on one shared scale and add together directly.
- **FR-009**: The system MUST include a session in the **daily** series when its assigned calendar
  day falls within the requested range, and MUST exclude it otherwise. Both endpoint days of the
  range are included in full. The weekly series covers a wider span than the daily one, per FR-013.
- **FR-010**: The system MUST return daily entries in ascending date order and weekly entries in
  ascending week order.
- **FR-011**: The system MUST produce totals that do not depend on the order in which sessions were
  supplied.
- **FR-012**: The system MUST aggregate deterministically: the same sessions and the same requested
  range MUST always yield the same totals, independent of the current date and time and of any
  external service.
- **FR-013**: The system MUST report weekly totals over whole ISO weeks only. Where the requested
  range's first day is not a Monday or its last day is not a Sunday, the system MUST extend the
  weekly series outward — back to the Monday that opens the range's first week and forward to the
  Sunday that closes its last week — and MUST total every session falling in those whole weeks,
  including sessions on days lying outside the requested range. Every weekly total therefore covers
  seven days and is directly comparable against every other. The daily series MUST NOT be extended
  in this way and MUST remain strictly within the requested range.
- **FR-014**: The system MUST report, for each daily and weekly total, the basis of that total as
  exactly one of: **measured** (every contributing session's load was measured), **estimated**
  (every contributing session's load was estimated), **mixed** (both kinds contributed), or **none**
  (no session contributed).
- **FR-015**: The system MUST report a total as mixed whenever at least one contributing session's
  load was estimated and at least one was measured, however small the estimated share.
- **FR-016**: The system MUST report, for each daily and weekly total, how many sessions contributed
  to it, so that a day totalling 0 points from real sessions remains distinguishable from a day with
  no sessions.
- **FR-017**: For any ISO week lying wholly inside the requested range, the system MUST make that
  week's total equal the sum of the daily totals of its seven days — the daily and weekly views MUST
  NOT disagree. For a first or last week extended outward under FR-013, the week's total MUST equal
  that same sum plus exactly the loads of the extended days lying outside the range, and MUST NOT
  differ from it for any other reason.
- **FR-018**: The system MUST accept an empty collection of sessions and return the full gap-free
  series of zero-load days and weeks for the requested range, rather than an empty result or an
  error.
- **FR-019**: The system MUST refuse a requested range whose end date precedes its start date, and
  the refusal MUST identify the range as the problem.
- **FR-020**: The system MUST refuse a request whose range is missing a start date or an end date; a
  range MUST be explicitly bounded at both ends.
- **FR-021**: Every refusal MUST state which rule was violated, and MUST NOT be silently discarded,
  substituted with an empty result, or reduced to a generic failure.
- **FR-022**: The system MUST preserve the exact sum of the contributing load values, without
  rounding, truncation, or accumulated precision loss, so that a total can be reproduced by hand
  from the sessions that make it up.
- **FR-023**: The system MUST treat every supplied session as a distinct contribution, including two
  sessions carrying the same external identifier. Recognising and removing duplicates belongs to
  import and storage, not to aggregation.
- **FR-024**: The system MUST compute aggregation purely from the supplied sessions and requested
  range, without reading from storage, contacting any external service, or consulting the current
  time.
- **FR-025**: The aggregation model MUST NOT reference, name, or encode concepts belonging to Strava
  or any other external activity provider, including provider-specific field names, data shapes,
  identifier formats, or enumerations.

### Key Entities *(include if data involved)*

- **Date Range**: The stretch of time the athlete asked about, bounded by a first and last calendar
  day, both included. Cannot exist with an end before its start or with either end unbounded.
- **Daily Training Load**: One calendar day's total — the day itself, the summed points of every
  session attributed to it, how many sessions contributed, and the basis of the total. Exists for
  every day in the requested range, including days with no training.
- **Weekly Training Load**: One ISO-8601 week's total — the week it identifies (its
  week-numbering year and week number, running Monday to Sunday), the summed points, the
  contributing session count, and the basis of the total. Exists for every week the range touches,
  and always covers all seven of that week's days even where the requested range covers only part
  of it.
- **Load Basis**: How much a total can be trusted: measured, estimated, mixed, or none. Derived from
  the provenance of every load that went into the total, and inseparable from it — a total read
  without its basis invites a comparison between a measured week and a guessed one.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For any requested range, the daily series contains exactly one entry per calendar day
  in that range, in ascending order, with no gaps and no duplicates — verified across ranges of one
  day, one week, and several months.
- **SC-002**: 100% of days and weeks with no sessions report a total of 0 points and a basis of
  none, and none are omitted from the series.
- **SC-003**: For every ISO week lying wholly inside the requested range, the week's total equals
  the sum of its seven daily totals with zero discrepancy; for an extended first or last week, the
  difference between the two equals exactly the load of the extended days outside the range.
- **SC-004**: A reviewer can reproduce any daily or weekly total by hand from the contributing
  sessions' load values alone, with no rounding differences.
- **SC-005**: Repeated aggregation of the same sessions over the same range produces identical
  totals every time, with zero variation across repetitions, supplied orderings, or the date on
  which the aggregation is run.
- **SC-006**: 100% of sessions whose assigned day falls outside the requested range contribute
  nothing to any daily total, and contribute to a weekly total only when their day lies in an
  extended first or last week under FR-013.
- **SC-007**: 100% of daily and weekly totals report their basis and their contributing session
  count, and no consumer can obtain a total without them.
- **SC-008**: A session starting at any time on a Monday is assigned to the week that Monday opens,
  and a session starting at any time on the preceding Sunday is assigned to the week before — in
  100% of boundary cases tested, including sessions at 00:00 and 23:59.
- **SC-009**: 100% of attempts to aggregate over a range that violates a stated rule are refused,
  and every refusal names the violated rule; no partial or empty result is returned in place of a
  refusal.
- **SC-010**: A reviewer inspecting the aggregation model finds no mention of Strava or any other
  named external provider.
- **SC-011**: A session's assigned day matches the athlete's local calendar day at that session's
  own recorded offset in 100% of cases, including sessions whose UTC instant falls on a different
  calendar day and pairs of sessions recorded at different offsets.
- **SC-012**: Every weekly entry returned covers exactly seven days, Monday through Sunday, for
  every requested range — including ranges shorter than one week and ranges that begin or end
  mid-week.

## Assumptions

- Sessions are supplied to this feature already recorded and validated by the Training Activity
  Domain (feature 001). Aggregation does not re-validate them, and a session that could not be
  created cannot reach this feature.
- Each session's training load is obtained through the mechanism feature 001 defines, which requires
  the athlete's maximum heart rate as an explicit input. Aggregation therefore needs that value
  supplied alongside the sessions; it does not own, store, or default it, per feature 001's FR-015.
- The product serves a single athlete, so all supplied sessions belong to one person and no grouping
  by athlete is needed.
- A session is attributed to the day it started because a session record has no end time. A session
  that runs past local midnight therefore counts wholly against the day it began — the
  simplest rule that the available data supports, and one the athlete can predict.
- The basis of a total (FR-014) has four states rather than three because a day with no sessions is
  genuinely different from a day whose sessions were all measured at 0 points; collapsing them would
  make a rest day indistinguishable from a recorded easy session.
- A single estimated session makes a whole total mixed (FR-015) rather than the basis being
  proportional. A proportion would be a second number needing its own interpretation rules; a
  three-way qualifier is enough to stop a guessed week being compared against a measured one, and a
  proportional measure can be added later if the trend feature turns out to need it.
- ISO-8601 weeks (Monday to Sunday, with the ISO week-numbering year) are used because the project
  plan specifies ISO week boundaries and because they give a single unambiguous rule for the days
  around 1 January.
- Day assignment uses each session's own recorded UTC offset (FR-004), so a day is the day the
  athlete actually experienced; feature 001 retains that offset for exactly this purpose. The
  consequence accepted here is that an athlete who travels between time zones can record two
  sessions on the same local day whose absolute instants lie more than 24 hours apart, and both
  count against that local day. A single athlete-level timezone was rejected because it would
  introduce an input this feature does not otherwise need; UTC was rejected because an evening
  session at a positive offset would fall on the wrong day.
- Weekly totals cover whole ISO weeks even where the requested range does not (FR-013), because the
  later trend feature compares one week's load against another's and a partly-covered week would
  read as a training drop that never happened. The accepted cost is that a weekly total can include
  load from days outside the requested range — which is why FR-017 requires that difference to be
  exactly attributable to those days, and why the daily series is deliberately not extended with
  it.
- No upper bound is placed on the length of a requested range. A range spanning decades produces a
  correspondingly long daily series; treating that as a rule the domain enforces would mean choosing
  an arbitrary threshold, and it is a question for whichever caller requests the range.
- Load values are summed exactly, as the loads themselves are expressed in points that must remain
  hand-reproducible (feature 001, SC-006). Any presentation rounding belongs to the eventual user
  interface, not to aggregation.
- The daily and weekly series are the inputs the later Fitness/Fatigue/Form feature will consume,
  which is why the series must be gap-free — those calculations decay day by day and cannot skip
  missing days. Producing them is this feature's responsibility; consuming them is not.
- Monthly totals, per-activity-type breakdowns, and totals by any dimension other than time are not
  required by the MVP and are therefore not part of this feature.
- The terms used in this specification (session, day, week, load, basis) are the product's own
  vocabulary and are deliberately independent of any provider's terminology.

## Out of Scope

- Fetching sessions from Strava or any other external source, and any authentication, token
  handling, pagination, rate limiting, or incremental synchronisation.
- Storing aggregated totals or retrieving sessions from storage.
- Fitness (CTL), Fatigue (ATL), and Form (TSB) calculations, which consume this feature's daily
  series but are specified separately.
- Week-over-week change, trend detection, load spike and drop detection, and any explanatory
  analysis text.
- Detecting or removing duplicate sessions.
- Monthly, yearly, or per-activity-type aggregation.
- Any API endpoint or user interface exposing these totals, including charts.
