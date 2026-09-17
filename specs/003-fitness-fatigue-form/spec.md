# Feature Specification: Fitness, Fatigue, and Form

**Feature Branch**: `003-fitness-fatigue-form`

**Created**: 2026-09-17

**Status**: Draft

**Input**: User description: "Feature 3 — Fitness / Fatigue / Form. From the daily training-load series produced by Feature 2, compute the three standard training-stress metrics over a date range: Fitness (CTL, chronic training load), Fatigue (ATL, acute training load), and Form (TSB, training stress balance). The result is a day-by-day historical series so the evolution of the three metrics over time can be displayed, not just today's values. Pure domain logic in TrainingLoadAnalyzer.Domain, built on the existing DailyTrainingLoad / TrainingLoadAggregator / DateRange types; no Strava, no persistence, no UI. The exponentially-weighted formulas, the time constants, the seeding/warm-up behaviour at the start of the series and how rest days and days with no data are treated must all be pinned down in the spec before any implementation, and the LoadBasis (measured/estimated/mixed) provenance already carried by daily totals must be carried through into the resulting metrics. Scope excludes trend/spike detection, which is Feature 4."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Read today's fitness, fatigue, and form (Priority: P1)

An athlete who has been training for months wants three numbers that summarise where they stand
today: how much training their body has absorbed over the long run (Fitness), how much recent work
is still weighing on them (Fatigue), and whether the balance between the two leaves them fresh or
buried (Form). One hard week should push Fatigue up sharply while Fitness barely moves, and Form
should turn negative. A week of rest should let Fatigue fall away quickly while Fitness declines
only slowly, and Form should climb.

**Why this priority**: These three numbers are the product's reason for existing — items 7, 8, and
9 of the MVP. Without them the athlete has raw daily totals and nothing that interprets them. This
is the smallest slice that delivers the interpretation, and it can be read on its own without any
chart, trend, or history view.

**Independent Test**: Feed a hand-built daily load series with known values through the calculation
and check the final day's three figures against numbers worked out by hand from the stated
formulas; include a series ending in a hard block and a series ending in a rest week.

**Acceptance Scenarios**:

1. **Given** a daily load series in which every day carries the same load repeated for long enough
   for both metrics to settle, **When** the metrics are calculated, **Then** Fitness and Fatigue
   both converge toward that daily load and Form converges toward 0 — sustained, unchanging
   training eventually stops producing either freshness or fatigue.
2. **Given** a settled series followed by several consecutive days of load well above the settled
   level, **When** the metrics are calculated, **Then** Fatigue rises further than Fitness over
   those days and Form is negative on the last of them.
3. **Given** a settled series followed by seven consecutive days of zero load, **When** the metrics
   are calculated, **Then** Fatigue falls further than Fitness over that week and Form is positive
   on the last day.
4. **Given** any day in the series, **When** its Form is read, **Then** it equals that day's Fitness
   minus that day's Fatigue exactly, with no separate or independently accumulated value.
5. **Given** the same daily load series, **When** the metrics are calculated repeatedly, **Then**
   identical figures are produced every time, with no dependence on the current date or on any
   external service.

---

### User Story 2 - Follow how the three metrics evolved (Priority: P2)

The athlete wants to see the shape of a training block, not a snapshot: how Fitness climbed through
a build period, where Fatigue spiked, and whether Form recovered before the event. They need a
figure for every single day across a chosen stretch of time, including rest days and days they did
nothing at all, so the three curves are continuous and can be plotted or scanned day by day.

**Why this priority**: It turns three numbers into a story the athlete can act on, and it is what
MVP item 10 (display how these metrics evolve over time) and the eventual dashboard chart consume.
It is worth less on its own than Story 1 only because the single most recent value is the one an
athlete checks daily.

**Independent Test**: Calculate the metrics over a range spanning several weeks and confirm there is
exactly one entry per calendar day, in order, with no gaps — including across a stretch with no
training at all — and that each day's figures follow from the day before it by the stated formulas.

**Acceptance Scenarios**:

1. **Given** a requested range of 2026-03-01 to 2026-03-31, **When** the metrics are calculated,
   **Then** exactly 31 entries are produced, one per calendar day in ascending order, each carrying
   a Fitness, a Fatigue, and a Form figure.
2. **Given** a range in which the athlete trained on no day at all, **When** the metrics are
   calculated, **Then** an entry is still produced for every day, each showing metrics that decay
   toward zero rather than being absent, and no day is omitted.
3. **Given** a day carrying no training, **When** that day's metrics are calculated, **Then** it is
   treated as a day of zero load and the metrics decay across it — a rest day is a real input to the
   calculation, not a gap to be skipped.
4. **Given** any two consecutive days in the result, **When** the later day's figures are checked
   against the earlier day's figures and the later day's load, **Then** they follow from the stated
   formulas exactly, so any day in the series can be reproduced by hand from the day before it.
5. **Given** a requested range covering a single day, **When** the metrics are calculated, **Then**
   one entry is produced and it is not an error.

---

### User Story 3 - Know how far to trust the three numbers (Priority: P3)

Fitness and Fatigue are built from weeks of daily loads, and some of those loads were measured from
heart rate while others were only estimated from duration. When the athlete reads a Form figure
before deciding whether to race, they need to know whether it rests on measured work, on guesses, or
on a mixture — and whether the series has run long enough for the figures to mean anything at all.
A Fitness figure computed from the first three days of a freshly imported history is not yet a
Fitness figure, and must not be presented as one.

**Why this priority**: It qualifies the numbers from Stories 1 and 2 rather than producing new ones.
Without it the athlete cannot tell a warm-up artefact from a real value, and the eventual dashboard
would show a meaningless rising curve during the first weeks of data as though it were genuine
progress.

**Independent Test**: Calculate the metrics over a series built from measured loads only, from
estimated loads only, and from a mixture, and confirm each day reports the corresponding basis;
separately, calculate over a series shorter than the warm-up period and confirm every day in it is
marked as not yet reliable.

**Acceptance Scenarios**:

1. **Given** a day whose contributing daily loads were all measured, **When** its metrics are
   calculated, **Then** they are reported as measured.
2. **Given** a day among whose contributing daily loads at least one was estimated and at least one
   was measured, **When** its metrics are calculated, **Then** they are reported as mixed.
3. **Given** a history beginning on 2026-01-01 and a day on or before 2026-02-11 — the 42nd day —
   **When** its metrics are calculated, **Then** they are marked as not yet reliable, and that
   marking is readable alongside the figures rather than having to be inferred from the dates.
4. **Given** that same history and the day 2026-02-12 — the 43rd day — **When** its metrics are
   calculated, **Then** they are marked as reliable.
5. **Given** a day on which no load has yet contributed anything, **When** its metrics are
   calculated, **Then** their basis is reported as none, distinguishable from a day whose metrics
   rest on real zero-load days.

---

### Edge Cases

- A requested range covering exactly one day.
- A requested range whose end date precedes its start date, or which is missing either bound.
- A history supplied with no days in it at all.
- A history that does not reach as far back as the requested range's first day.
- A history that stops before the requested range's last day, leaving days at the end with no load
  to consume.
- A supplied history with a gap in it — a missing calendar day between two present ones.
- A history whose days are supplied out of order, or containing the same calendar day twice.
- A range and a history that do not overlap at all.
- The very first day of the whole history, which has no preceding day to decay from.
- A history shorter than the Fatigue time constant, shorter than the Fitness time constant, and
  shorter than the stated warm-up period.
- A single enormous load — a multi-day ultra — sitting in an otherwise quiet series.
- A long history of many years of daily values, where repeated decay could accumulate rounding
  error.
- Every day carrying exactly zero load, which must remain distinguishable from having no data.
- A history whose daily totals came entirely from estimated loads, so no figure in the result rests
  on measurement.

## Requirements *(mandatory)*

### Functional Requirements

#### What is calculated

- **FR-001**: The system MUST calculate, for each calendar day, a **Fitness** figure representing the
  athlete's chronic training load: an exponentially weighted average of daily training load with a
  long time constant, so that it responds slowly to any single day.
- **FR-002**: The system MUST calculate, for each calendar day, a **Fatigue** figure representing the
  athlete's acute training load: an exponentially weighted average of the same daily training load
  with a short time constant, so that it responds quickly to recent days.
- **FR-003**: The system MUST calculate, for each calendar day, a **Form** figure equal to that day's
  Fitness minus that day's Fatigue. Form MUST be derived from the other two on every occasion it is
  read and MUST NOT be accumulated, stored, or decayed independently, so the three can never
  disagree.
- **FR-004**: The system MUST use a Fitness time constant of **42 days** and a Fatigue time constant
  of **7 days**. These values are fixed by this specification and MUST NOT be inferred, defaulted, or
  chosen during implementation.
- **FR-005**: The system MUST advance both metrics day by day using the following rule, applied once
  per calendar day in ascending date order:

  > `today = yesterday + (today's load − yesterday) × α`, where `α = 1 − e^(−1/N)`

  with N being the metric's time constant in days: 42 for Fitness, 7 for Fatigue. The two smoothing
  factors are therefore `α_fitness = 1 − e^(−1/42) ≈ 0.0235283133` and `α_fatigue = 1 − e^(−1/7) ≈
  0.1331221002`. This is the true exponential form; the approximation `α = 1 ÷ N` MUST NOT be used in
  its place.
- **FR-006**: The system MUST include the day's own training load in that day's Fitness and Fatigue
  figures — each day's figures reflect training done up to and including that day.
- **FR-007**: The system MUST calculate all three metrics purely from the supplied daily load
  history and requested range, without reading from storage, contacting any external service, or
  consulting the current time.

#### The series produced

- **FR-008**: The system MUST produce exactly one entry per calendar day in the requested range, in
  ascending date order, with no gaps and no duplicates, so the three metrics form continuous curves
  that can be plotted directly.
- **FR-009**: The system MUST treat a day carrying no training as a day of load 0 and MUST advance
  both metrics across it, so that a rest day causes decay rather than being skipped. A day with no
  training and a day with a recorded zero-point session MUST produce identical Fitness and Fatigue
  figures, differing only in their reported basis (FR-016).
- **FR-010**: The system MUST calculate each day's figures from the day immediately preceding it, so
  that no day in the series can be produced without every day before it having been produced first.

#### Where the series starts

- **FR-011**: The system MUST require the supplied daily load history to begin on or before the first
  day of the requested range, and MUST calculate forward from the history's first day so that the
  requested range's first day already carries the accumulated effect of the days preceding it.
- **FR-012**: The system MUST seed both Fitness and Fatigue at **0** on the notional day before the
  supplied history begins, so the history's first day is calculated as `0 + (that day's load − 0) ×
  α`. The system MUST NOT accept, require, or infer any other starting value, and MUST NOT derive a
  seed from the history's own early days.
- **FR-013**: The system MUST mark every day lying within a **warm-up period** of the history's first
  day as not yet reliable, and every day beyond it as reliable.
- **FR-014**: The warm-up period MUST be **42 days** — one Fitness time constant — counted from the
  first day of the supplied history and including it. The history's first day and the 41 days
  following it are therefore not yet reliable, and the 43rd day onward is reliable.
- **FR-015**: The system MUST make the reliability marking readable on every day of the series
  alongside that day's figures, and MUST NOT withhold, blank, or refuse the figures of a warm-up day
  — the figures are produced, and qualified.

#### How far the figures can be trusted

- **FR-016**: The system MUST report, for each day's metrics, the basis of those metrics as exactly
  one of **measured**, **estimated**, **mixed**, or **none**, using the same four states and the same
  meanings as a daily total's basis.
- **FR-017**: The system MUST derive a day's basis from the bases of the daily totals that
  contributed load to that day's metrics: measured when every contributing day was measured,
  estimated when every contributing day was estimated, mixed when both kinds contributed, and none
  when no day has yet contributed any load.
- **FR-018**: A day whose daily total carried no training MUST NOT contribute to the basis, since it
  contributed no load and therefore nothing that could have been measured or estimated. A day whose
  sessions totalled exactly zero points MUST contribute its basis, because it carries a real
  measurement or a real estimate.
- **FR-019**: The system MUST determine the basis over a bounded window of preceding days rather
  than over the whole history, so that one estimated day early in a long history does not mark every
  later day as mixed forever. The window MUST be the metric's own time constant: the last 42 days
  for Fitness and the last 7 days for Fatigue, each including the day itself. Form's basis MUST
  combine the two, being measured only when both are measured and mixed when they disagree or when
  either is mixed.
- **FR-020**: The system MUST report a day's basis as none when no day within the relevant window
  carried any training, distinguishable from a basis derived from real zero-point sessions.

#### Refusals and inputs

- **FR-021**: The system MUST refuse a requested range whose end date precedes its start date, or
  which is missing either bound, on the same terms as an aggregation range.
- **FR-022**: The system MUST refuse a request whose supplied history does not reach back to the
  first day of the requested range, and the refusal MUST identify the shortfall — silently seeding
  mid-range would produce figures that look genuine and are not.
- **FR-023**: The system MUST refuse a supplied history that is not continuous and gap-free in
  ascending date order — a missing day, a repeated day, or days out of order MUST be refused rather
  than repaired, because a silently skipped day changes every figure after it.
- **FR-024**: The system MUST accept a history that extends beyond the requested range at either end
  and MUST use the days before the range to build up the metrics, while producing entries only for
  days within the range.
- **FR-025**: The system MUST accept a history in which every day carries zero load and produce a
  full series of figures decaying toward zero, rather than an empty result or a refusal.
- **FR-026**: Every refusal MUST state which rule was violated, and MUST NOT be silently discarded,
  substituted with an empty result, or reduced to a generic failure.

#### Arithmetic and independence

- **FR-027**: The system MUST calculate deterministically: the same history over the same range MUST
  always yield identical figures, independent of the current date and of any external service.
- **FR-028**: The system MUST carry full precision through the day-by-day calculation and MUST NOT
  round intermediate figures, so that rounding error cannot accumulate across a long history. Any
  rounding for display belongs to the user interface.
- **FR-029**: The system MUST produce figures a reviewer can reproduce, from the stated formula and
  the previous day's figures, to within **0.0001 points**. Because the smoothing factors of FR-005
  are irrational, exact reproduction is not achievable and MUST NOT be required; a stated tolerance
  takes its place. The daily loads entering the calculation are still consumed exactly as the
  aggregation feature produced them, with no rounding applied on the way in.
- **FR-029a**: The system MUST apply the smoothing factors of FR-005 at a precision of at least ten
  significant figures, whether computed or stated as constants, so that the tolerance of FR-029 is
  not consumed by the factors themselves.
- **FR-030**: The metrics model MUST NOT reference, name, or encode concepts belonging to Strava or
  any other external activity provider, including provider-specific field names, data shapes,
  identifier formats, or enumerations.

### Key Entities *(include if data involved)*

- **Daily Load History**: The continuous, gap-free run of daily training-load totals the calculation
  consumes, each carrying its points and its basis. Supplied by the aggregation feature; not
  produced here. Must begin on or before the requested range's first day.
- **Daily Training Metrics**: One calendar day's position — the day itself, its Fitness, its Fatigue,
  its Form, whether the figures are yet reliable, and the basis of each. Exists for every day in the
  requested range, including days with no training.
- **Fitness**: How much training the athlete has absorbed over the long run, as an exponentially
  weighted average of daily load over a 42-day constant. Slow to build and slow to lose.
- **Fatigue**: How much recent training is still weighing on the athlete, over a 7-day constant.
  Quick to rise and quick to fall.
- **Form**: The balance between the two — Fitness minus Fatigue. Positive means fresher than the
  recent training would suggest, negative means carrying accumulated work. Never stored separately
  from the two figures it comes from.
- **Reliability**: Whether a day lies far enough past the start of the supplied history for the
  seeding assumption to have decayed out of the figures. Not a judgement about the training, only
  about how much history stands behind the number.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For any requested range, the result contains exactly one entry per calendar day in
  that range, in ascending order, with no gaps and no duplicates — verified across ranges of one
  day, one week, and several months.
- **SC-002**: A reviewer can reproduce any day's Fitness and Fatigue from the previous day's figures
  and that day's load, using only the formula stated in FR-005, to within 0.0001 points, for 100% of
  days checked.
- **SC-003**: Form equals Fitness minus Fatigue on 100% of days produced, with zero discrepancy.
- **SC-004**: Given an unchanging daily load sustained for 365 days, Fitness and Fatigue both settle
  to within 1% of that load, and Form to within 1% of that load of zero.
- **SC-005**: Given a settled series followed by seven days of zero load, Fatigue falls by a larger
  proportion than Fitness over those seven days in 100% of such cases.
- **SC-006**: 100% of days with no training produce figures, and none are omitted from the series.
- **SC-007**: For a history beginning on day D, 100% of days from D to D+41 inclusive are marked not
  yet reliable and 100% of days from D+42 onward are marked reliable, with no day on either side of
  that boundary marked otherwise.
- **SC-008**: 100% of days report a basis for each of the three figures, and no consumer can obtain a
  figure without its basis and its reliability marking.
- **SC-009**: A day's basis reflects only the daily totals inside that metric's own window, verified
  by showing that an estimated day drops out of the basis once it falls outside the window.
- **SC-010**: Repeated calculation over the same history and range produces identical figures every
  time, with zero variation across repetitions or across the date on which the calculation is run.
- **SC-011**: 100% of requests violating a stated rule — an invalid range, a history that starts too
  late, a history with a gap, a repeated day, or days out of order — are refused, and every refusal
  names the violated rule; no partial or approximate result is returned in place of a refusal.
- **SC-012**: Over a 10-year history of daily values, the difference between the calculated figures
  and figures computed at higher precision stays within the tolerance stated in FR-029, showing no
  accumulated drift.
- **SC-013**: A reviewer inspecting the metrics model finds no mention of Strava or any other named
  external provider.

## Assumptions

- The daily load history is produced by the Training Load Aggregation feature (002), which already
  guarantees a continuous, gap-free daily series with a basis and a session count per day. This
  feature consumes that series and does not re-derive it from sessions, so it needs no maximum heart
  rate, no sessions, and no knowledge of how a load was calculated.
- Fitness, Fatigue, and Form are this product's names for what the training-science literature calls
  CTL, ATL, and TSB. The athlete-facing names are used throughout because they are what the
  dashboard will show; the acronyms are recorded here once and then dropped.
- The 42-day and 7-day time constants (FR-004) are the values established by the impulse-response
  training model these metrics come from, and are what every comparable product uses. Making them
  configurable is deliberately not done: a configurable constant would need a specified valid range,
  a default, and a rule for what happens when it changes mid-history, none of which the MVP needs.
- The true exponential smoothing factor `1 − e^(−1/N)` is used (FR-005) rather than the common
  `1 ÷ N` approximation. The two differ by about 1% per step at N=42 and about 7% at N=7, which
  compounds into visibly different curves, and the exponential is the form the underlying model is
  actually defined in. The accepted cost is that the factors are irrational, so the day-by-day
  figures cannot be reproduced exactly the way feature 001's and 002's point totals can. FR-029
  replaces exactness with a stated tolerance for this reason, and it is the one place in the domain
  where a tolerance is accepted — the daily loads feeding this calculation remain exact.
- Form is derived rather than accumulated (FR-003) so the invariant Form = Fitness − Fatigue holds by
  construction and cannot be broken by a later change to either input.
- Both metrics are seeded at 0 (FR-012) rather than from caller-supplied starting values. Nothing in
  the MVP has stored values to supply — the Strava import feature brings in raw activities, not
  previously computed metrics — so accepting a starting state would add two inputs to every request,
  plus rules for their absence and validity, to serve a caller that does not exist. The accepted cost
  is that the first weeks of any history read artificially low, which is precisely what the warm-up
  marking exists to declare. Seeding from the history's own early days was rejected as it would
  produce figures no reviewer could derive without knowing a second, invented averaging rule.
- The warm-up period is one Fitness time constant (FR-014): after 42 days roughly 63% of the
  zero-seeding error has decayed out. Two or three constants would decay more of it, but would leave
  a newly imported year of training showing three to four months as provisional. The marking is a
  qualifier rather than a guarantee: a day marked reliable can still sit somewhat below its true
  value if the history began only just over 42 days earlier, and the honest way to remove that error
  is to supply more history, not to lengthen the marking.
- A day with no training is load 0 rather than absent data (FR-009). The two are genuinely different
  in principle — an athlete who did not train and an athlete whose watch failed are not in the same
  state — but this product has no way to tell them apart, since an activity that was never recorded
  leaves no trace. Treating an unrecorded day as rest is the assumption the available data forces,
  and the basis of none makes it visible where it matters.
- The metrics are calculated forward from the start of the supplied history rather than from the
  start of the requested range (FR-011, FR-024), because a 42-day constant means a figure only means
  anything once weeks of prior training stand behind it. Asking the caller for history before the
  range is the price of that.
- A gap in the supplied history is refused rather than filled with zeroes (FR-023). Filling would be
  easy and would silently change every later figure, and this feature cannot tell a genuinely absent
  day from an aggregation defect. The aggregation feature already produces gap-free series, so a gap
  here indicates a caller error worth surfacing.
- The basis is computed over a bounded window (FR-019) rather than over the whole contributing
  history. Mathematically every past day contributes something to an exponentially weighted average
  forever, so an unbounded rule would mark every day after the athlete's first estimated session as
  mixed for the rest of time, which would tell the athlete nothing. Using each metric's own time
  constant as the window keeps the qualifier aligned with the period the figure actually reflects.
- The reliability marking (FR-013) describes only how much history stands behind a figure. It is not
  a data-quality score, is not combined with the basis, and does not change the figures themselves.
- The three metrics are unitless in the sense that they are expressed on the same points scale as the
  daily loads they come from, so a Fitness of 60 is directly comparable against a daily load of 60.
- These figures are descriptive, not prescriptive. This specification defines no thresholds for what
  a "good" or "risky" Form value is, and no training recommendations follow from it — the project
  plan places medical and health recommendations out of scope.
- The product serves a single athlete, so all supplied daily loads belong to one person and no
  grouping by athlete is needed.

## Out of Scope

- Week-over-week change, trend detection, load spike and drop detection, and any explanatory
  analysis text — these are Feature 4 and consume this feature's output.
- Ramp rate, monotony, strain, and any other derived training-stress metric beyond the three
  specified here.
- Predicting or projecting Fitness, Fatigue, or Form forward past the last day of supplied history,
  and any "what if I train X tomorrow" calculation.
- Recommending training, rest, tapers, or race timing from a Form value, and any threshold marking a
  value as good, risky, or overreaching.
- Weekly or monthly summaries of the three metrics; the series is daily.
- Fetching or persisting daily loads or calculated metrics, and any caching of previously computed
  figures.
- Any API endpoint or user interface exposing these figures, including the dashboard chart that will
  eventually plot them.
- Per-activity-type metrics — separate running and cycling fitness — and the impact of one
  individual session on the metrics.
