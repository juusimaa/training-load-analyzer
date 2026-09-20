# Feature Specification: Load Trends

**Feature Branch**: `004-load-trends`

**Created**: 2026-09-17

**Status**: Complete — implemented and signed off 2026-09-20

**Input**: User description: "Feature 4 — Load Trends. Detect and describe how a person's training load is changing from one ISO week to the next, so that meaningful increases and drops become visible rather than having to be read off raw weekly totals. Scope, per section 15 of training-load-analyzer-plan.md: week-over-week load change (absolute and relative), significant increases, significant decreases. This feature builds on what Features 002 and 003 already produced and must not duplicate them. Available today in TrainingLoadAnalyzer.Domain: WeeklyTrainingLoad(IsoWeek Week, decimal Points, int ActivityCount, LoadBasis Basis) from TrainingLoadAggregator.AggregateWeekly, DailyTrainingLoad, DailyTrainingMetrics (Fitness/Fatigue/Form with per-figure LoadBasis) from TrainingMetricsCalculator, IsoWeek, DateRange, LoadBasis (None/Measured/Estimated/Mixed) and LoadProvenance. Points the specification should settle explicitly, because they decide behaviour and must not be invented during implementation: what counts as a 'significant' increase or decrease, and whether that threshold is relative, absolute, or both; how a week following a rest week or a zero-load week is handled, where a relative change is undefined or infinite; how a partial or in-progress week at the end of the requested range is treated; how gaps are handled when a week has no recorded activity at all — is it a zero week or an absent one; how the LoadBasis of the weeks involved carries into the trend, since a trend computed from Estimated or Mixed weeks is weaker evidence than one from Measured weeks, consistent with how Feature 003 attaches reliability to every figure; whether the feature reports a per-week change series, a classification per week, or both. This is pure domain logic: no Strava, no persistence, no UI. Follow the constitution — strict TDD, domain independent of infrastructure, no speculative generalization."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - See how this week compares to last week (Priority: P1)

An athlete looking at a run of weekly totals — 480, 505, 610, 300 — can see the numbers but has to
do the arithmetic in their head to know what changed. They want each week to carry its own
comparison against the week before it: how many points up or down, and what share of the previous
week that represents. A jump from 505 to 610 is +105 points and +20.8%; the drop to 300 is −310
points and −50.8%. The same two facts, stated once per week, for every week in the range they asked
about.

**Why this priority**: This is the whole substance of the feature. Everything else in this
specification either qualifies these two numbers or labels them. Without them there is no trend, and
with them alone the feature already delivers item 11 of the MVP — the change itself is visible
without any judgement being applied to it.

**Independent Test**: Supply a continuous run of weekly totals and a requested range, and read the
absolute and relative change on each week of the resulting series. Fully testable against
hand-computed arithmetic with no thresholds, no classification, and no reliability rules involved.

**Acceptance Scenarios**:

1. **Given** weeks W1=500 and W2=600 and a range covering W2, **When** trends are calculated, **Then**
   W2 reports an absolute change of +100 points and a relative change of +0.2.
2. **Given** weeks W1=600 and W2=450 and a range covering W2, **When** trends are calculated, **Then**
   W2 reports an absolute change of −150 points and a relative change of −0.25.
3. **Given** weeks W1=500 and W2=500 and a range covering W2, **When** trends are calculated, **Then**
   W2 reports an absolute change of 0 points and a relative change of 0.
4. **Given** a range spanning four consecutive weeks, **When** trends are calculated, **Then** exactly
   four entries are produced, one per ISO week, in ascending week order, each comparing itself to the
   week immediately before it.
5. **Given** a requested range whose first week has no preceding week in the supplied history, **When**
   trends are calculated, **Then** the request is refused and the refusal identifies the missing week.
6. **Given** a requested range spanning four weeks and a history that stops after the second, **When**
   trends are calculated, **Then** the request is refused and the refusal identifies the shortfall
   rather than returning two entries.

---

### User Story 2 - Have the changes that matter called out (Priority: P2)

The same athlete does not want to scan twelve percentages looking for the ones worth reacting to.
Ordinary week-to-week variation — a session moved, a slightly longer ride — should be labelled as
steady and fade into the background, while a genuine ramp-up or a genuine collapse in training
should be labelled as such. A jump from 500 to 610 points should be called out as a significant
increase. A wobble from 500 to 520 should not. A week of 20 points rising to 26 should not be called
out either, even though it is +30%, because six points is not a change in anybody's training.

**Why this priority**: This is what turns a column of percentages into an observation, and it is the
second half of MVP item 11 — "detect significant increases or decreases in training load". It
depends on User Story 1 and is therefore built after it, but it is the part an athlete actually
reads.

**Independent Test**: Supply weekly totals engineered to sit either side of the significance
thresholds and read the classification on each resulting entry. Testable purely against the
threshold rules, independently of how the underlying changes were computed.

**Acceptance Scenarios**:

1. **Given** weeks W1=500 and W2=600, **When** trends are calculated, **Then** W2 is classified as a
   significant increase.
2. **Given** weeks W1=500 and W2=520, **When** trends are calculated, **Then** W2 is classified as
   steady, because +4% does not reach the relative threshold.
3. **Given** weeks W1=20 and W2=26, **When** trends are calculated, **Then** W2 is classified as
   steady, because +6 points does not reach the absolute floor even though +30% clears the relative
   threshold.
4. **Given** weeks W1=600 and W2=400, **When** trends are calculated, **Then** W2 is classified as a
   significant decrease.
5. **Given** weeks W1=500 and W2=0, **When** trends are calculated, **Then** W2 is classified as a
   significant decrease with a relative change of −1.

---

### User Story 3 - Know when a trend is not worth trusting (Priority: P3)

Not every comparison deserves the same confidence. Coming back from a completely idle week, the
percentage increase is not a number at all — there is nothing to divide by — yet the return to
training is exactly the kind of jump worth flagging. A week the athlete is only three days into will
always look like a catastrophic drop, because four of its days have not happened. And a week whose
load was estimated from moving time rather than measured from heart rate supports a weaker
conclusion than one built from real data. The athlete wants each trend to carry that context rather
than to be silently misled by it.

**Why this priority**: It prevents the feature from producing confident nonsense, and it keeps
Feature 3's discipline — every figure states how far it can be trusted — rather than dropping it one
feature later. It is last because Stories 1 and 2 are already useful on a well-behaved history, and
this story hardens them against the ill-behaved one.

**Independent Test**: Supply histories containing a zero week, a range boundary that cuts a week
short, and weeks of differing bases, and read the relative change, the completeness, the
classification, and the basis on each resulting entry.

**Acceptance Scenarios**:

1. **Given** weeks W1=0 and W2=400, **When** trends are calculated, **Then** W2 reports an absolute
   change of +400 points, no relative change at all, and is classified as a significant increase.
2. **Given** weeks W1=0 and W2=0, **When** trends are calculated, **Then** W2 reports an absolute
   change of 0, no relative change at all, and is classified as steady.
3. **Given** a requested range ending on a Wednesday, **When** trends are calculated, **Then** the
   final week is reported as partial and classified as indeterminate, while still reporting its
   absolute and relative change.
4. **Given** a measured week followed by an estimated week, **When** trends are calculated, **Then**
   the resulting trend reports a basis of mixed.
5. **Given** a week carrying no training at all followed by a measured week, **When** trends are
   calculated, **Then** the resulting trend reports a basis of measured, because nothing about the
   comparison was estimated.

---

### Edge Cases

- **A week missing from the supplied history.** Refused rather than repaired. A skipped week would
  silently compare a week to the wrong predecessor (FR-024).
- **The first week of the requested range.** It has a predecessor like any other week, and that
  predecessor must be supplied even though it lies outside the range (FR-022).
- **A history that stops before the range's last week.** Refused, naming the shortfall. Zero-filling
  the missing weeks would manufacture rest weeks the athlete never took, each of which would then be
  reported as a significant decrease (FR-022b).
- **A single-week range.** Produces exactly one entry, comparing that week to the supplied week
  before it.
- **A range starting mid-week.** Its first week is partial on the same terms as a truncated final
  week (FR-017).
- **A range beginning and ending inside the same ISO week.** Produces one entry, partial at both
  ends.
- **Previous week of zero, current week of zero.** No relative change exists; the absolute change of
  0 is steady (FR-013).
- **Previous week of zero, current week below the absolute floor.** A 30-point return to training
  after an idle week is steady, not a significant increase — the floor applies whether or not a
  relative change exists (FR-014).
- **A change exactly at a threshold.** Significant. Both tests are "at least", never "more than"
  (FR-011).
- **A week whose sessions totalled exactly zero points.** Distinct from a week with no training: it
  contributes its basis, while an untrained week contributes nothing (FR-020).
- **A history extending far beyond the requested range at either end.** Accepted; entries are
  produced only for weeks within the range (FR-023).
- **An entirely zero history.** Produces a full series of steady entries with no relative changes,
  not an empty result and not a refusal.

## Requirements *(mandatory)*

### Functional Requirements

#### What is compared

- **FR-001**: The system MUST compare each ISO-8601 week's total training load against the total
  training load of the ISO week immediately preceding it, and MUST NOT compare against any other
  week, any average of several weeks, or any rolling window.
- **FR-002**: The system MUST report, for each compared week, an **absolute change** equal to that
  week's points minus the preceding week's points, in the same training-load points the weekly totals
  are expressed in. A positive figure means more load than the week before.
- **FR-003**: The system MUST report, for each compared week, a **relative change** equal to the
  absolute change divided by the preceding week's points, expressed as a proportion rather than a
  percentage — a relative change of `0.2` means twenty percent more load than the week before. Any
  conversion to a percentage for display belongs to the user interface.
- **FR-004**: The system MUST NOT report a relative change when the preceding week's points are
  zero, because no such proportion exists. The absence MUST be readable as an absence, and MUST NOT
  be represented as zero, as an infinity, as a sentinel value, or by omitting the entry.
- **FR-005**: The system MUST calculate both changes purely from the supplied weekly load history
  and the requested range, without reading from storage, contacting any external service, or
  consulting the current time.
- **FR-006**: The system MUST base every comparison on the weekly totals as the aggregation feature
  produced them, and MUST NOT recompute, re-derive, or adjust a weekly total in the course of
  comparing it.

#### What counts as significant

- **FR-007**: The system MUST classify each compared week as exactly one of **significant increase**,
  **significant decrease**, **steady**, or **indeterminate**.
- **FR-008**: The system MUST classify a week as a significant increase when its change is upward and
  clears both the relative threshold and the absolute floor, and as a significant decrease when its
  change is downward and clears both.
- **FR-009**: The system MUST classify as steady every compared week that is not indeterminate and
  does not clear both thresholds, including a week whose change is exactly zero.
- **FR-010**: The relative threshold MUST be a change in magnitude of at least **0.15** — fifteen
  percent of the preceding week — and the absolute floor MUST be a change in magnitude of at least
  **50 points**. Both MUST be cleared for a change to be significant, so that a large proportion of a
  tiny number and a small proportion of a large number are both left as steady. These values are
  fixed by this specification and MUST NOT be inferred, defaulted, or chosen during implementation.
- **FR-011**: Both threshold tests MUST treat a change exactly at the threshold as clearing it, so a
  change of exactly 0.15 with exactly 50 points is significant.
- **FR-012**: The system MUST apply both threshold tests to the unrounded changes of FR-002 and
  FR-003, so that a change cannot be rounded across a threshold.
- **FR-013**: When no relative change exists (FR-004), the system MUST decide significance on the
  absolute floor alone, because the relative test cannot be applied. The absolute floor MUST still be
  cleared.
- **FR-014**: A week following an untrained week therefore MUST be classified as a significant
  increase when it carries at least 50 points, and as steady when it carries fewer — resuming
  training is a real increase, but resuming it gently is not a significant one.

#### Weeks that cannot be fairly compared

- **FR-015**: The system MUST report, for each compared week, whether that week is **complete** or
  **partial** within the requested range.
- **FR-016**: A week MUST be reported as complete when all seven of its days fall inside the
  requested range, and as partial otherwise. Completeness MUST be determined from the requested range
  alone, so that the same history and range always yield the same answer regardless of when the
  calculation is run.
- **FR-017**: The system MUST classify every partial week as indeterminate, and MUST NOT classify one
  as a significant increase, a significant decrease, or steady. A week the range cuts short is being
  compared against a week of seven days, and that comparison is not like for like; labelling it a
  significant decrease would report a drop the athlete did not take.
- **FR-018**: The system MUST still report the absolute and relative change of a partial week. The
  change is a fact about the load actually recorded, and it is qualified rather than withheld — on
  the same terms as a warm-up day's figures in the fitness-and-form feature.
- **FR-019**: A partial week MUST be compared against the preceding week's full seven-day total, and
  the system MUST NOT scale, extrapolate, or pro-rate either week to make the comparison even.
  Inventing load the athlete has not done would make the figure worse, not better.

#### How far a trend can be trusted

- **FR-020**: The system MUST report, for each compared week, the basis of that comparison as exactly
  one of **measured**, **estimated**, **mixed**, or **none**, using the same four states and the same
  meanings as a weekly total's basis.
- **FR-021**: The system MUST derive a comparison's basis by combining the bases of the two weeks it
  compares: measured when every contributing week was measured, estimated when every contributing
  week was estimated, mixed when both kinds contributed, and none when neither week carried any
  training. A basis of **none** MUST contribute nothing to the combination, so a measured week
  following an untrained week reports a measured comparison — nothing about it was estimated. This is
  the same rule by which a day's basis is combined in the fitness-and-form feature.

#### Refusals and inputs

- **FR-022**: The system MUST refuse a request whose supplied weekly history does not include the ISO
  week immediately preceding the first week of the requested range, and the refusal MUST identify the
  missing week. The alternative — producing a first entry with no comparison in it — would put an
  entry in the series that means something different from every other entry.
- **FR-022b**: The system MUST refuse a request whose supplied weekly history ends before the last
  ISO week touching the requested range, and the refusal MUST identify the shortfall. Treating the
  missing trailing weeks as rest would invent training history the athlete never supplied — a
  fabricated zero week is indistinguishable from a real one and would be classified as a significant
  decrease — and returning fewer entries than the range asked for would break FR-028. This mirrors
  FR-022 at the other end of the history.
- **FR-023**: The system MUST accept a history extending beyond the requested range at either end,
  using the week before the range to compare against while producing entries only for weeks touching
  the range.
- **FR-024**: The system MUST refuse a supplied history that is not continuous and gap-free in
  ascending ISO-week order — a missing week, a repeated week, or weeks out of order MUST be refused
  rather than repaired, because a skipped week silently compares a week against the wrong
  predecessor.
- **FR-025**: A week in which the athlete did not train MUST be present in the supplied history as a
  zero-point week with a basis of none, and MUST NOT be absent from it. An absent week is a gap and
  is refused under FR-024; this is what makes "no training that week" and "no data for that week"
  impossible to confuse.
- **FR-026**: The system MUST refuse a requested range whose end date precedes its start date, or
  which is missing either bound, on the same terms as an aggregation range.
- **FR-027**: Every refusal MUST state which rule was violated, and MUST NOT be silently discarded,
  substituted with an empty result, or reduced to a generic failure.

#### The series produced

- **FR-028**: The system MUST produce exactly one entry per ISO week touching the requested range, in
  ascending week order, with no gaps and no duplicates, so the trend can be read straight down
  alongside the weekly totals it came from.
- **FR-029**: The system MUST report the change and the classification together on every entry, as
  one series, rather than producing a separate list of significant weeks. A caller wanting only the
  significant increases filters the series; the feature MUST NOT provide a second result shape, a
  second entry point, or a detection pass of its own to do it for them.
- **FR-030**: Each entry MUST carry enough to be read without consulting the weekly totals
  separately: the week itself, its own points, the preceding week's points, both changes, the
  classification, the completeness, and the basis.

#### Arithmetic and independence

- **FR-031**: The system MUST calculate deterministically: the same history over the same range MUST
  always yield identical results, independent of the current date and of any external service.
- **FR-032**: The system MUST carry full precision through both changes and MUST NOT round them. Any
  rounding for display belongs to the user interface.
- **FR-033**: The system MUST produce an absolute change a reviewer can reproduce exactly by
  subtracting the two weekly totals, with no tolerance required, because the subtraction is exact.
- **FR-034**: The trend model MUST NOT reference, name, or encode concepts belonging to Strava or any
  other external activity provider, including provider-specific field names, data shapes, identifier
  formats, or enumerations.

### Key Entities *(include if data involved)*

- **Weekly Load History**: The continuous, gap-free run of weekly training-load totals the comparison
  consumes, each carrying its points and its basis. Supplied by the aggregation feature; not produced
  here. Must include the week immediately preceding the requested range's first week, and must carry
  untrained weeks as zero-point weeks rather than omitting them.
- **Weekly Load Trend**: One ISO week's comparison against the week before it — the week itself, its
  points, the preceding week's points, the absolute change, the relative change where one exists, the
  classification, whether the week is complete within the range, and the basis. Exists for every week
  touching the requested range, including weeks with no training.
- **Absolute Change**: How many training-load points the week moved by. Always defined, always exact.
- **Relative Change**: What share of the preceding week that movement represents, as a proportion.
  Absent — not zero — when the preceding week carried no load.
- **Trend Classification**: The judgement attached to a comparison: a significant increase, a
  significant decrease, steady, or indeterminate. Derived from the two changes and the week's
  completeness on every occasion it is read, never accumulated or stored independently of them.
- **Completeness**: Whether the requested range contains all seven days of the week. A property of
  the range, not of the athlete's training, and not a judgement about the data.
- **Basis**: How far the comparison can be trusted, given where the load in the two weeks it compares
  came from. Not a judgement about the training, only about the evidence behind the numbers.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An athlete reading a run of twelve weekly totals can tell, without doing any
  arithmetic, which weeks their training rose or fell in and by how much, in both points and
  proportion.
- **SC-002**: Every week touching a requested range yields exactly one trend entry, so a twelve-week
  range always produces twelve entries regardless of how many of those weeks the athlete trained in.
- **SC-003**: Any absolute change in the series can be reproduced exactly by subtracting the two
  weekly totals it compares, with no tolerance allowed.
- **SC-004**: Any relative change in the series can be reproduced from the same two totals, and no
  entry reports a relative change of zero where the preceding week carried no load.
- **SC-005**: A week in which training rose or fell by less than fifteen percent, or by fewer than
  fifty points, is never presented as significant — so ordinary week-to-week variation does not
  compete for attention with a real ramp-up.
- **SC-006**: A week whose range is cut short is never presented as a significant decrease, so no
  athlete is told their training collapsed in a week that has not finished.
- **SC-007**: Every trend entry states its basis, so a comparison resting on estimated load can be
  told apart from one resting on measured load without inspecting the weeks behind it.
- **SC-008**: A request whose history is missing the preceding week, stops before the range's last
  week, contains a gap, repeats a week, or runs out of order is refused with a message naming the
  rule broken, and no partial series is returned alongside it.
- **SC-009**: The same history over the same range produces byte-identical results on every run and
  on any date, so a trend can be cited in a review and re-derived later.
- **SC-010**: The trend model can be exercised in full without a Strava account, a network
  connection, a database, or a user interface.

## Assumptions

- **Thresholds are fixed, not configurable.** FR-010 pins 0.15 and 50 points as constants of the
  specification. No per-athlete or per-caller threshold is offered, because nothing in the MVP needs
  one and a settable threshold would be speculative generalization. Should the dashboard later need
  to vary them, that is a specification amendment, not a parameter added during implementation.
- **The 15% relative threshold is a deliberate departure from the familiar "10% rule".** Ten percent
  is a guideline for how fast to progress, not a test for what is worth reporting; at ten percent a
  large share of ordinary weeks would be flagged and the classification would stop carrying
  information. Fifteen percent, paired with the 50-point floor, is set so that a flagged week is one
  an athlete would recognise as a real change of gear.
- **Fifty points is calibrated to this project's load scale.** Load is Edwards TRIMP points, in which
  an hour of steady moderate work scores in the low hundreds and a training week commonly totals
  several hundred. Fifty points is therefore roughly a short easy session — below it, a change is not
  a change in anybody's training.
- **The caller supplies the weekly history; this feature does not aggregate it.** Producing weekly
  totals from activities belongs to the aggregation feature and is not repeated here. The requirement
  that untrained weeks appear as zero-point weeks (FR-025) is already satisfied by what that feature
  produces, since it walks the range rather than grouping the activities.
- **Completeness is defined against the requested range, not against the calendar or the clock.**
  This keeps the calculation deterministic and clock-free (FR-005, FR-031). A caller wanting the
  current in-progress week marked partial asks for a range ending today, and gets exactly that.
- **Trends are computed from weekly load, not from fitness, fatigue, or form.** The three metrics of
  Feature 3 already describe the athlete's state day by day; this feature describes the movement of
  the training itself. Applying trend detection to the metrics series is out of scope and is not
  implied by anything here.
- **No trend is computed at the daily level.** MVP item 11 and section 15 of the project plan both
  frame the trend as week over week. Daily trend detection is not in scope.
- **No explanatory prose is generated.** The classification is a label, not a sentence. Turning a run
  of trends into readable commentary, if it happens at all, belongs to the dashboard feature.
- **Depends on Feature 2 for weekly totals and their bases, and shares Feature 3's basis-combination
  rule.** Neither feature is modified by this one.
