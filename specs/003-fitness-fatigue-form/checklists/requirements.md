# Specification Quality Checklist: Fitness, Fatigue, and Form

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

- Items marked incomplete require spec updates before `/speckit-clarify` or `/speckit-plan`.

### Validation iteration 1 — 2026-09-17

Three `[NEEDS CLARIFICATION]` markers remained, all on the load model itself. They were deliberate
rather than oversights: section 9 of the project plan requires the formulas and time constants to be
fixed in the specification so the implementing agent does not invent them, and Constitution
Principle VII forbids the agent inventing requirements. Each had more than one defensible answer
with materially different figures, and none had a default this project could adopt without a
decision. Every other checklist item passed.

### Validation iteration 2 — 2026-09-17

All three were answered by the project owner and folded into the spec:

1. **FR-005 — exponential form.** The true exponential `α = 1 − e^(−1/N)` was chosen over the
   `1 ÷ N` approximation. This is the one place in the domain where exact hand-reproduction is given
   up: the smoothing factors are irrational, so FR-029 states a tolerance of 0.0001 points in place
   of the exactness feature 001 and 002 require of their point totals, and FR-029a was added to keep
   the factors' own precision from consuming that tolerance.
2. **FR-012 — seeding.** Both metrics seed at 0 before the history's first day. No caller-supplied
   starting state, which keeps the request to a history plus a range.
3. **FR-014 — warm-up.** 42 days, one Fitness time constant, counted from and including the
   history's first day. SC-007 and the User Story 3 scenarios were made concrete against that
   boundary (D..D+41 not yet reliable, D+42 onward reliable).

All checklist items now pass. The spec is ready for `/speckit-plan`; a separate `/speckit-clarify`
pass is not needed, as the questions that pass would have raised were resolved here.

### Validation iteration 3 — 2026-09-17 (post-plan amendments)

`/speckit-plan` found two gaps that this checklist's items did not catch, because both were
*consistency* problems visible only when designing against the spec rather than reading it:

1. **FR-019's last sentence was wrong about Form's basis.** Read literally it reported `Mixed` for a
   day whose every contributing session was measured, purely because the athlete had rested for a
   week and the 7-day window was empty. Resolved as **FR-019a** (a basis of none contributes nothing)
   and **FR-019b** (which records the consequence: Form's basis is then necessarily identical to
   Fitness's, because the 7-day window always sits inside the 42-day one). A sixth acceptance scenario
   was added to User Story 3.
2. **No requirement resolved a history ending before the range's last day**, though the Edge Cases
   listed it. Resolved as **FR-022a**: refuse, naming the shortfall. SC-011 was extended to include
   it.

All 16 checklist items still pass. The spec now carries 34 functional requirements.

**Worth noting for the process retrospective**: "Requirements are testable and unambiguous" passed in
iterations 1 and 2 and was wrong about FR-019. Reading a requirement is not the same as designing
against it, which is the argument for `/speckit-plan` being a separate phase rather than a formality
on the way to tasks.
