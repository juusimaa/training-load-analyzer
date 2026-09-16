# Specification Quality Checklist: Training Activity Domain

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-09-16
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

- Items marked incomplete require spec updates before `/speckit-clarify` or `/speckit-plan`
- **Status: 16 of 16 items passing as of validation iteration 3.** Ready for `/speckit-plan`.

### Validation iteration 1 — 2026-09-16

Three markers open: duration semantics (FR-004), heart-rate measures (FR-007), training-load
method (FR-009). 13 of 16 checklist items passing.

### Validation iteration 2 — 2026-09-16

Author resolved all three: moving time; full heart-rate sample series; heart-rate-based TRIMP.

Two consequences required new requirements rather than being defaultable — the athlete's heart-rate
parameters are not a property of a session, and choosing a heart-rate-based method made heart-rate
data load-bearing while it remained optional. Both were raised as new open markers rather than
resolved silently. 13 of 16 items passing.

### Validation iteration 3 — 2026-09-16

Author resolved both remaining markers:

| Question | Answer | Encoded in |
|----------|--------|------------|
| TRIMP variant | Edwards TRIMP, five zones weighted 1–5 | FR-009 (zone table), FR-010, FR-011, FR-016 |
| No heart-rate data | Duration-based estimate, marked as estimated | FR-013, FR-014, SC-007 |

All 16 checklist items now pass. Requirements renumbered to FR-001 – FR-025 (was FR-001 – FR-022);
no downstream artifacts existed yet, so no references were broken.

Three mechanical details were needed to make Edwards TRIMP unambiguous, and were resolved as
documented defaults rather than new questions, per the command's guidance on informed guesses.
Each is recorded in Assumptions and is a single reviewable value:

1. **Zone boundary precision** (FR-009) — lower bound inclusive, upper exclusive; below 50% of
   maximum contributes nothing; at or above 100% counts in zone 5.
2. **Time attribution from discrete samples** (FR-010) — each sample's value is held until the next
   sample; the final sample contributes no time. Chosen over a gap threshold, which would have
   introduced another unexplained constant. Consequence: a single-sample series yields a measured
   load of 0, and a series with gaps attributes the gap to the preceding sample. Both are listed
   as edge cases.
3. **Estimate weight** (FR-013) — moving-time minutes × 2, i.e. zone 2 / moderate aerobic, applied
   identically to running and cycling so the estimate shares the measured scale.

**Reviewer attention is warranted on item 3** in particular: it is a chosen constant, not a
physiologically derived one, and it silently sets how much a heart-rate-less session contributes to
CTL/ATL downstream.

**Note on Content Quality item 1**: the specification names Strava in FR-025 and in the Out of Scope
section. This is a deliberate architectural constraint inherited from Constitution Principle II,
not an implementation detail, and it is stated as a prohibition on the domain rather than as a
design instruction.
