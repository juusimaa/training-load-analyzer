# Specification Quality Checklist: Strava Import

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-09-17
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs)
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders
- [x] All mandatory sections completed

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain
- [x] Requirements are testable and unambiguous
- [x] Success criteria are measurable
- [x] Success criteria are technology-agnostic (no implementation details)
- [x] All acceptance scenarios are defined
- [x] Edge cases are identified
- [x] Scope is clearly bounded
- [x] Dependencies and assumptions identified

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria
- [x] User scenarios cover primary flows
- [x] Feature meets measurable outcomes defined in Success Criteria
- [x] No implementation details leak into specification

## Notes

- **All three open clarifications were resolved by the developer on 2026-09-17** and written back into
  the specification; no markers remain.
  - **FR-010, FR-010a, FR-010b** — the sport-type table is now fixed: `Run`/`TrailRun`/`VirtualRun`
    map to running, `Ride`/`GravelRide`/`MountainBikeRide`/`VirtualRide` to cycling, `EBikeRide` and
    everything else ignored.
  - **FR-017 through FR-017e** — a heart-rate series is retrieved only for activities Strava reports as
    having one, within a 180-day measured window, and only when not already held; a series once stored
    is never discarded.
  - **FR-031 through FR-031e, FR-032** — reconciliation happens only over history read to completion:
    routinely over the look-back window, over the whole history on a full resynchronization, and never
    at all after a sync that stopped early.
- Three requirements were added beyond the clarifications as direct consequences of them, and are
  flagged here so they are reviewed rather than absorbed silently: **FR-017e** (an outstanding series
  holds the resume point back, so retries need no separate queue), **FR-031c** (nothing is ever removed
  after an incomplete read), and **FR-031e** (a sport type corrected out of scope removes its session).
- **On "no implementation details"**: "Strava" and its sport-type names appear throughout by necessity —
  the provider's vocabulary is the subject of the feature, and Principle VII requires the mapping to be
  pinned in the specification rather than chosen during implementation. The Assumptions section also
  cites Strava's read-request allowance, because it is the reason the 180-day window exists. The spec
  names no endpoint, response field, library, storage engine, or project in the solution; those belong
  to the plan.
- Content Quality and Feature Readiness items passed on the first validation iteration. The second
  iteration, after the clarifications were applied, passed every item.
- **Ready for `/speckit-plan`.** `/speckit-clarify` has nothing left to ask.

## Re-validation after planning (2026-09-17)

Planning amended the specification twice, so the checklist was re-run against the amended text. All 16
items still pass; the spec grew from 55 to 58 functional requirements.

- **FR-002a** — access granted more narrowly than requested is refused, naming what was withheld.
  Designing the connection flow surfaced that Strava lets an athlete approve some requested scopes and
  decline others, which the spec had not covered. Silently accepting would have under-reported the
  athlete's training undetectably ([research.md](../research.md) R14).
- **FR-017f, FR-017g** — implausible heart-rate samples are discarded, counted, and reported; a series
  is unusable only when fewer than two samples survive. The original Assumption forbidding any repair
  would have sent nearly every real session to estimated load, cancelling the 180-day measured window
  the developer had deliberately chosen (R21). The Assumption was rewritten to scope "does not repair"
  to moving time and sport type and to state the heart-rate rule explicitly.
- Also added during planning: `EMountainBikeRide`, `Handcycle` and `Velomobile` are now named in
  FR-010's ignored list, so each exclusion reads as a decision rather than an oversight. This changed
  no behaviour — all four were already excluded by the catch-all.
- One item to note for the completion review rather than a checklist failure: the specification now
  names Strava sport types, scope semantics, and a 180-day window. Each is the provider's vocabulary or
  a value Principle VII requires be pinned in writing, not a leaked implementation choice. The spec
  still names no endpoint, library, storage engine, or project.
