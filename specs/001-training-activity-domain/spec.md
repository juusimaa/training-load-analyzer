# Feature Specification: Training Activity Domain

**Feature Branch**: `001-training-activity-domain`

**Created**: 2026-09-16

**Status**: Complete — implemented and signed off 2026-09-20

**Input**: User description: "Training Activity Domain: the core domain model for a single training activity. A TrainingActivity has an external identifier, a start time with timezone offset, a duration, an activity type (running or cycling), and optional heart-rate data. It exposes a training-load value computed from a single clearly-defined method (the specific method is NOT yet decided and must be marked as an open question rather than invented). Invalid activities (non-positive duration, unsupported activity type, missing start time) must be rejected at construction. The domain must not reference Strava or any other external activity provider. Out of scope: fetching activities from any external source, persistence, aggregation across multiple activities, and CTL/ATL/TSB calculations."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Record a completed training session (Priority: P1)

An athlete has finished a run or a ride. The product needs a faithful, self-contained record of
that single session: which session it was, when it started in the athlete's own local time, how
long they were actually moving, and what kind of session it was. Nothing else the product offers —
load, fitness, fatigue, form, trends — can be trusted if this record is not trustworthy first.

**Why this priority**: Every later capability reads from this record. It is the only story that
delivers standalone value on its own, and it is the foundation the remaining MVP features are
built on.

**Independent Test**: Record a session from a known set of details and read every detail back
unchanged; confirm that sessions with different external identifiers are treated as different
sessions and that the athlete's local start time survives the round trip.

**Acceptance Scenarios**:

1. **Given** a completed run identified as "A-1", started 2026-03-01 at 07:30 local time at an
   offset of +02:00 from UTC, with 45 minutes of moving time, **When** the session is recorded,
   **Then** the identifier, start time, offset, moving time, and the classification "running" are
   all readable back unchanged.
2. **Given** a completed ride identified as "A-2", **When** the session is recorded, **Then** it
   is classified as "cycling" and is distinguishable from the run recorded as "A-1".
3. **Given** a session whose start time carries a +02:00 offset, **When** the session is recorded,
   **Then** both the absolute instant and the athlete's local start time remain recoverable — the
   offset is not discarded.
4. **Given** a session recorded without heart-rate data, **When** the session is read back,
   **Then** the absence of heart-rate data is explicit rather than represented as a zero, an empty
   series, or anything otherwise indistinguishable from a real measurement.
5. **Given** a session recorded with a series of heart-rate samples, **When** the session is read
   back, **Then** the samples are returned in their original time order with their original
   values.

---

### User Story 2 - Know how demanding a session was (Priority: P2)

For each recorded session, the athlete needs one number expressing how demanding that session was,
so that a short hard session and a long easy one can be compared on the same scale, and so that
running and cycling sessions can be added together later. When heart-rate data was recorded, that
number is derived from how much time the athlete spent in each heart-rate zone. When it was not,
the product still produces a number, but says openly that it is an estimate.

**Why this priority**: This is the value the product is named for, but it is meaningless without
User Story 1. It can be developed and demonstrated as soon as a session can be recorded.

**Independent Test**: Compute the load for a handful of sessions with short, hand-constructed
heart-rate series and compare the results against values worked out by hand from the zone table
and the athlete's maximum heart rate; then compute the load for a session with no heart-rate data
and confirm it matches the estimate formula and is reported as estimated.

**Acceptance Scenarios**:

1. **Given** a session with heart-rate samples and the athlete's maximum heart rate, **When** its
   training load is requested, **Then** a single non-negative numeric load value is produced and
   is reported as measured.
2. **Given** an athlete with a maximum heart rate of 190 bpm and a session whose heart-rate series
   is 150 bpm at minute 0, 175 bpm at minute 10, and 160 bpm at minute 20, **When** its training
   load is requested, **Then** the load is 80 TRIMP points — 10 minutes held at 150 bpm (78.9% of
   maximum, zone 3, weight 3) gives 30, 10 minutes held at 175 bpm (92.1%, zone 5, weight 5) gives
   50, and the final sample contributes nothing.
3. **Given** two sessions whose heart-rate series are identical in intensity but one of which is
   twice as long, **When** their loads are compared, **Then** the longer session has the strictly
   greater load.
4. **Given** two sessions of equal duration, one spent entirely in zone 1 and one spent entirely in
   zone 5, **When** their loads are compared, **Then** the zone 5 session's load is five times the
   zone 1 session's load.
5. **Given** a session spent entirely below 50% of the athlete's maximum heart rate, **When** its
   training load is requested, **Then** the measured load is 0 — not an error, and not estimated.
6. **Given** a session with a heart-rate sample above the athlete's stated maximum heart rate,
   **When** its training load is requested, **Then** that time counts in zone 5 and the load is
   still produced.
7. **Given** the same session details and the same maximum heart rate, **When** the training load
   is computed repeatedly, **Then** the same value is produced every time, with no dependence on
   the current date, on other sessions, or on any external service.
8. **Given** a running session and a cycling session whose heart-rate series are identical,
   **When** their loads are compared, **Then** the values are equal — intensity is measured by
   heart rate alone, on one scale shared by both activity types.
9. **Given** a session with no heart-rate data and 40 minutes of moving time, **When** its training
   load is requested, **Then** the load is 80 TRIMP points (40 × 2) and is reported as estimated.
10. **Given** a measured load and an estimated load of the same numeric value, **When** each is
    read, **Then** they remain distinguishable from one another.

---

### User Story 3 - Refuse to record a malformed session (Priority: P3)

When session details are incomplete or nonsensical, the product must refuse to create the record
and say why, rather than storing something that quietly corrupts every later calculation.

**Why this priority**: It protects the value of Stories 1 and 2, but on its own it delivers no new
capability to the athlete.

**Independent Test**: Attempt to record sessions that each violate exactly one rule and confirm
that each attempt is refused and that the refusal names the violated rule.

**Acceptance Scenarios**:

1. **Given** session details with a moving time of zero, **When** recording is attempted, **Then**
   the attempt is refused and the refusal identifies the moving time as the problem.
2. **Given** session details with a negative moving time, **When** recording is attempted,
   **Then** the attempt is refused and the refusal identifies the moving time as the problem.
3. **Given** session details with an activity type that is neither running nor cycling, **When**
   recording is attempted, **Then** the attempt is refused and the refusal identifies the
   unsupported activity type.
4. **Given** session details with no start time, **When** recording is attempted, **Then** the
   attempt is refused and the refusal identifies the missing start time.
5. **Given** session details with a missing or blank external identifier, **When** recording is
   attempted, **Then** the attempt is refused and the refusal identifies the identifier.
6. **Given** session details carrying a heart-rate sample outside the plausible range, **When**
   recording is attempted, **Then** the attempt is refused and the refusal identifies the
   offending sample.
7. **Given** session details carrying heart-rate samples that are not in ascending time order,
   **When** recording is attempted, **Then** the attempt is refused and the refusal identifies the
   ordering as the problem.
8. **Given** session details carrying a heart-rate series that is present but empty, **When**
   recording is attempted, **Then** the attempt is refused — a session either has heart-rate data
   or has none.
9. **Given** a valid session with heart-rate data and a maximum heart rate of zero or less,
   **When** its training load is requested, **Then** the request is refused and the refusal
   identifies the maximum heart rate.
10. **Given** any refused attempt, **When** the refusal occurs, **Then** no partially-formed
    session record exists to be read or used.

---

### Edge Cases

- A session with a moving time of exactly zero, or a negative moving time.
- A session that starts before midnight and ends after it in the athlete's local time.
- A session that starts during a daylight-saving transition in the athlete's local timezone.
- A session whose type is neither running nor cycling (for example a swim, a walk, or a strength
  session), including a type that is empty or unrecognised.
- A session with no heart-rate data at all.
- A session carrying an empty heart-rate series — distinct from having no heart-rate data.
- A session with exactly one heart-rate sample, which under the hold-until-next rule contributes no
  measured time and therefore a measured load of 0.
- Heart-rate samples that are out of time order, duplicated at the same instant, or spaced
  irregularly.
- Heart-rate samples that fall outside the session's own time window.
- A heart-rate series with long gaps, or one that covers only part of the session — the series may
  account for less time than the session's moving time.
- Heart-rate samples that are physiologically implausible (for example 0 bpm or 300 bpm).
- A heart-rate sample exactly on a zone boundary, below 50% of maximum heart rate, or above the
  stated maximum heart rate.
- A maximum heart rate that is zero, negative, or absent.
- A session with an implausibly long moving time (for example multiple days).
- A session whose start time lies in the future relative to the moment of recording.
- Two different sessions submitted with the same external identifier.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST represent a single completed training session as a self-contained
  record consisting of an external identifier, a start time, a moving time, an activity type, and
  optional heart-rate data.
- **FR-002**: The system MUST retain an external identifier for each session as an opaque,
  provider-neutral value, and MUST NOT interpret, parse, or attach meaning to its contents.
- **FR-003**: The system MUST retain each session's start time together with its offset from UTC,
  such that both the absolute instant and the athlete's local start time remain recoverable.
- **FR-004**: The system MUST retain each session's duration as its **moving time** — the time the
  athlete spent actually moving, excluding stops — expressed as a positive span of time. Total
  elapsed time is not part of this feature and MUST NOT be required to record a session.
- **FR-005**: The system MUST classify each session as either running or cycling, and MUST NOT
  accept any other activity type.
- **FR-006**: The system MUST treat heart-rate data as optional, and MUST make the absence of
  heart-rate data distinguishable from a recorded measurement.
- **FR-007**: The system MUST represent a session's heart-rate data, when present, as an ordered,
  non-empty series of samples, each carrying the time at which it was measured and a heart rate in
  beats per minute.
- **FR-008**: The system MUST expose, for every valid session, a single non-negative numeric
  training-load value expressed in TRIMP points, representing how demanding that session was.
- **FR-009**: The system MUST compute the training load of a session that has heart-rate data using
  **Edwards TRIMP**: the sum, across five heart-rate zones, of the minutes spent in each zone
  multiplied by that zone's weight. Zones are defined as a percentage of the athlete's maximum
  heart rate, with the lower bound inclusive and the upper bound exclusive:

  | Zone | Heart rate as % of maximum | Weight |
  |------|----------------------------|--------|
  | —    | below 50%                  | 0      |
  | 1    | 50% – 60%                  | 1      |
  | 2    | 60% – 70%                  | 2      |
  | 3    | 70% – 80%                  | 3      |
  | 4    | 80% – 90%                  | 4      |
  | 5    | 90% and above              | 5      |

  Time at or above 100% of maximum heart rate counts in zone 5. Time below 50% contributes nothing.

- **FR-010**: The system MUST determine the time spent in each zone by holding each heart-rate
  sample's value constant from that sample's time until the time of the next sample. The final
  sample contributes no time. Time before the first sample and after the last sample contributes
  nothing.
- **FR-011**: The system MUST compute training load deterministically from the session's own
  details together with the athlete's maximum heart rate: the same session details and the same
  maximum heart rate MUST always yield the same value, independent of the current date and time, of
  any other session, and of any external service.
- **FR-012**: The system MUST express training load on a single scale determined by heart-rate
  intensity alone, such that running and cycling sessions with identical heart-rate series receive
  identical loads.
- **FR-013**: The system MUST compute the training load of a session that has no heart-rate data as
  its moving time in minutes multiplied by a default intensity weight of 2, expressed in the same
  TRIMP points as FR-009.
- **FR-014**: The system MUST mark every training-load value as either **measured** (computed per
  FR-009 from heart-rate data) or **estimated** (computed per FR-013 without it), and consumers of
  a load value MUST be able to tell the two apart regardless of the numeric value.
- **FR-015**: The athlete's maximum heart rate MUST be supplied to the training-load computation as
  an explicit input. A session record MUST NOT own, store, or default it, because it describes the
  athlete rather than the session and may change independently of any session.
- **FR-016**: The system MUST refuse to compute a measured training load when the supplied maximum
  heart rate is zero or negative, and the refusal MUST identify the maximum heart rate.
- **FR-017**: The system MUST refuse to create a session record whose moving time is zero or
  negative.
- **FR-018**: The system MUST refuse to create a session record whose activity type is not running
  or cycling.
- **FR-019**: The system MUST refuse to create a session record that has no start time.
- **FR-020**: The system MUST refuse to create a session record whose external identifier is
  missing or blank.
- **FR-021**: The system MUST refuse to create a session record whose heart-rate data is present
  but empty, whose heart-rate samples are not in ascending time order, or which carries any sample
  outside the plausible range of 20–250 beats per minute; the refusal MUST identify the offending
  sample or ordering.
- **FR-022**: Every refusal MUST state which rule was violated, and MUST NOT be silently discarded,
  substituted with a default value, or reduced to a generic failure.
- **FR-023**: When creation is refused, the system MUST NOT leave any partially-formed session
  record available for use.
- **FR-024**: A session record, once created, MUST NOT be modified; a correction MUST be expressed
  as a new record.
- **FR-025**: The training-session model MUST NOT reference, name, or encode concepts belonging to
  Strava or any other external activity provider, including provider-specific field names, data
  shapes, identifier formats, or enumerations.

### Key Entities *(include if data involved)*

- **Training Activity**: A single completed training session. Carries its external identifier, its
  start time with offset, its moving time, its activity type, and optionally its heart-rate series.
  Exposes its training load given the athlete's maximum heart rate. Cannot exist in an invalid
  state, and cannot be changed once created.
- **Activity Type**: The kind of session. For this feature the permitted values are running and
  cycling; anything else is rejected.
- **Heart Rate Series**: The optional measured heart-rate record for one session — an ordered,
  non-empty set of samples, each a time and a beats-per-minute value. Present or absent as a whole.
- **Training Load**: A non-negative number of TRIMP points expressing how demanding one session
  was, together with whether it was measured from heart-rate data or estimated from moving time.
  The two parts are inseparable: a load value is never meaningful without knowing which it is.
- **Heart Rate Zone**: One of five intensity bands defined as a percentage of the athlete's maximum
  heart rate, each carrying the weight that converts time spent in it into TRIMP points.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of validly-described sessions can be recorded and read back with every detail
  unchanged, including the athlete's local start time, offset, and heart-rate sample order.
- **SC-002**: 100% of attempts to record a session that violates a stated rule are refused, and
  every refusal names the violated rule; no invalid session record is ever obtainable.
- **SC-003**: Repeated computation of a session's training load from the same inputs produces an
  identical value every time, with zero variation across repetitions, dates, or session ordering.
- **SC-004**: For every pair of sessions with identical heart-rate intensity differing only in
  duration, the longer session's load is greater — no inversions.
- **SC-005**: For every pair of sessions of equal duration differing only in heart-rate intensity,
  the higher-intensity session's load is greater or equal, and strictly greater whenever the two
  fall in different zones — no inversions.
- **SC-006**: A reviewer can reproduce any session's training-load value by hand from the zone
  table in FR-009, or the estimate formula in FR-013, plus the session's own details and the
  athlete's maximum heart rate — without reading any code.
- **SC-007**: 100% of training-load values report whether they were measured or estimated, and no
  consumer can obtain a load value without that marking.
- **SC-008**: A session recorded at any UTC offset retains a start time from which the athlete's
  local calendar day is recoverable unchanged — including a session started during a
  daylight-saving transition, where the same wall-clock time at two different offsets must remain
  two distinguishable instants on the same local day.
- **SC-009**: A running session and a cycling session with identical heart-rate series produce
  identical load values, in 100% of such pairs.
- **SC-010**: A reviewer inspecting the training-session model finds no mention of Strava or any
  other named external provider.

## Assumptions

- The product serves a single athlete; there is no notion of multiple users, sharing, or
  permissions in this feature.
- Detecting or resolving duplicate external identifiers belongs to storage and synchronisation,
  not to this feature; here the identifier is simply retained.
- Moving time is supplied by whatever records the session. Deriving moving time from raw movement
  data is not this feature's responsibility, and is a constraint placed on the future import
  feature.
- The athlete's maximum heart rate is established elsewhere and supplied as an input. Measuring,
  estimating, or storing it is outside this feature.
- The zone boundaries and weights in FR-009 are the standard Edwards TRIMP five-zone table. They
  are fixed values in this feature, not athlete-configurable.
- The default intensity weight of 2 in FR-013 corresponds to zone 2, moderate aerobic effort, and
  is deliberately the same for running and cycling so that the estimate sits on the same scale as
  a measured load. It is a single reviewable constant chosen as a reasonable default, not a
  physiologically derived value, and is expected to be revisited once real data has been examined.
- The hold-until-next rule in FR-010 means a gap in the heart-rate series is attributed to the last
  sample before it. This is a deliberate default chosen for determinism and hand-verifiability over
  a gap threshold, which would introduce another unexplained constant.
- A session's heart-rate series may account for less time than its moving time. The measured load
  reflects only the time the samples cover; it is not scaled up to the full moving time.
- Heart-rate sample times are **not** validated against the session's moving time, and samples
  extending beyond it are accepted. Moving time excludes stops while a heart-rate monitor keeps
  recording through them, so a series routinely spans more time than the moving time it belongs to.
  Refusing such a session would reject ordinary, correct data.
- No upper bound is placed on moving time. Any threshold would be arbitrary — ultra-endurance
  events genuinely run for a day or more — and an implausibly long session is a data-quality
  question for whatever imports it, not a rule the domain can decide.
- A session has a start time but no end time, so a session running past local midnight is not
  observable in this feature and needs no rule here. It becomes relevant when sessions are assigned
  to calendar days for daily aggregation, which is out of scope.
- Heart-rate samples between 20 and 250 beats per minute are treated as plausible; anything outside
  that range is treated as corrupt data and the session is refused rather than the sample being
  silently dropped, per Constitution Principle VI. This range is a deliberate default and may be
  revisited once real data shapes have been examined.
- Distance, elevation, pace, and power are not required to record a session or to compute its
  training load in the MVP, and are therefore not part of this feature.
- Running and cycling are the only activity types the MVP supports; supporting more types later is
  expected to extend this feature rather than reshape it.
- A start time in the future is accepted; guarding against upstream clock skew is not this
  feature's responsibility. This is asserted by a test so that no future change quietly adds a
  guard the specification does not call for.
- The terms used in this specification (session, activity type, training load) are the product's
  own vocabulary and are deliberately independent of any provider's terminology.

## Out of Scope

- Fetching sessions from Strava or any other external source, including authentication, token
  handling, pagination, rate limits, and incremental synchronisation.
- Deriving moving time from raw position or movement data.
- Measuring, estimating, or storing the athlete's maximum heart rate.
- Storing sessions or retrieving them from storage.
- Aggregating sessions into daily, weekly, or any other multi-session totals.
- Fitness (CTL), Fatigue (ATL), Form (TSB), trend detection, and anomaly detection.
- Any user interface for viewing sessions or loads.
