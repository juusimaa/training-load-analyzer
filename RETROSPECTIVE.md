# Retrospective: Spec-Driven Development with an AI Agent

**Project**: Training Load Analyzer · **Written**: 2026-09-18 · Closes §16 (Day 14) and §20 of
[training-load-analyzer-plan.md](training-load-analyzer-plan.md).

This is the deliverable the project was actually for. The plan says so in §20: the exercise is not
evaluated by the number of features implemented, but by what can be said afterwards about
Spec-Driven Development and strict TDD under AI assistance. Six features shipped; that is the
evidence, not the result.

Everything below is sourced from the repository — commits, specs, compliance reviews, bug records.
Where the record cannot answer a question, it says so rather than guessing.

---

## What the record shows

| | |
|---|---|
| Planned duration | 14 days |
| Actual duration | **3 calendar days** (2026-09-16 → 2026-09-18) |
| Features | 6 of 6, all merged via PR |
| Tasks | 560 across six task lists, 558 complete |
| Tests | 409 passing (204 domain, 106 infrastructure, 99 web), 0 failing |
| Production code | 4,472 lines |
| Test code | 7,529 lines |
| Specification artifacts | **12,294 lines** |
| Post-merge defects found by a human using the app | 2 |

The specification-to-code ratio and the last row are the two findings that most shaped this
document. They are discussed under questions 7 and 6.

---

## The seven questions

### 1. Does Spec Kit help the agent make more consistent changes?

**Yes, and the mechanism is identifiable.** It is not that the agent reads the spec and behaves
better. It is that the spec creates *addressable names* — FR-012, C83, SC-005, R16 — and those names
turn a vague instruction into something greppable, testable, and reviewable.

Concrete evidence: feature 005's compliance review could assert Principle II not by argument but by
comparing the Domain project's git tree hash against the one recorded at task T002, and finding it
byte-identical (`f77a0e1`). Feature 006 did the same and added [scripts/compliance-006.sh](scripts/compliance-006.sh)
so the check is reproducible rather than claimed. Neither check is possible without an artifact that
said, in advance and in writing, what must not change.

The limit is worth stating: consistency holds *within* an artifact set and has to be arranged
*between* them. Section 7 of the project plan sketched five projects and three were built — the
right outcome under Principle III, but it only stayed the right outcome because a human wrote §22
([bb44aaa](https://github.com/juusimaa/training-load-analyzer/commit/bb44aaa)) to record which of
the five were actually decided and what would trigger revisiting, "so the five-project sketch in
section 7 is not taken at face value when a feature is scaffolded." A document the agent reads is a
document the agent will act on, including the speculative parts. Marking what is a sketch is not
optional.

### 2. How well does the AI follow strict TDD?

**Well, under scaffolding — and it erodes in one specific, predictable way.**

The scaffolding that worked was writing tasks as RED/GREEN pairs tied to named scenarios, then using
**commit order as the evidence**. Tasks T064 (001) and T066 (002) both require reviewing the git
history to confirm each test commit precedes the production commit that makes it pass, with an
explicit note that coverage percentage is *not* a substitute. Feature 001's history shows this
working literally: 55 commits alternating `test: RED for …` / `feat: …`.

The erosion is documented in [specs/006-dashboard/compliance-review.md](specs/006-dashboard/compliance-review.md),
which names three lapses and does not tidy them away. All three were the same failure:

> every lapse was a GREEN step doing more than the failing test asked for.

T024 and T031 were written after the code satisfying them, because earlier GREEN steps over-reached
and absorbed behaviour belonging to later tasks. That is not TDD however the coverage reads
afterwards, and the review says so. It happened in a feature whose own task list warned about it.

**The most valuable technique the project found was proving tests discriminate.** Feature 006
deliberately broke production code six times to confirm the corresponding test noticed. One did not:

> T061 — planted `const decimal Significant = 0.05m` — **passed at first. The test was worthless.**
> Its regex ended in `(?![\w])`, which rejected a C# decimal literal's `m` suffix.

A green test that cannot fail is invisible to every other measure of quality. Feature 001 found the
same class of problem at specification time: finding F1 noted that two guards both threw
`ArgumentException` with the same `ParamName`, so a RED test asserting only those could go green
against the wrong guard — "a RED step that cannot fail for a unique reason is not really RED."

This is the finding to carry forward. Not "did we write tests first" but "can this test fail."

### 3. Where does a human need to intervene?

The record shows four distinct points, in increasing order of how hard they are to automate.

**a) Deciding what the specification will not say.** Every open architectural decision was escalated
to and answered by the developer: §22.1–22.3 in the plan, the two FR-019 gaps in feature 003
([3cc800c](https://github.com/juusimaa/training-load-analyzer/commit/3cc800c) — "Both were answered
by the project owner"), and R4–R7 in feature 006. The constitution's Principle VII demands exactly
this, and it held.

**b) Catching the gap that gets settled silently.** Feature 006's FR-012 required state to live "on
the athlete's device." The design used a server-side singleton and no artifact said so. The plan's
own Principle VII gate passed it. `/speckit-analyze` caught it afterwards as CRITICAL. The commit
that fixed it ([9aa788d](https://github.com/juusimaa/training-load-analyzer/commit/9aa788d)) draws
the right conclusion:

> The useful record is not that it was fixed but that the gate did not catch it.

**c) Using the software.** Both post-merge defects were found by a human running the app against a
real account, not by 409 tests. See question 6.

**d) Reviewing the code.** This has **not happened**, and it is a required clause of the
constitution's Definition of Done. Feature 006's compliance review states plainly that it does not
substitute for human review. That gap is open at the time of writing.

### 4. Does specifying behaviour before implementation improve the result?

**Yes, and the clearest proof is the tests that exist to stop code being written.**

Feature 001's analyze pass produced four tests whose purpose is to prevent a rule the specification
never asked for: that a padded external identifier round-trips byte-for-byte (F7, guarding against a
`.Trim()` "added for tidiness"); that heart-rate samples extending beyond moving time are accepted
rather than refused; that the year-9999 boundary surfaces the framework's own exception rather than
an invented domain refusal (002 T068). An agent working from a ticket writes none of these. They
exist only because something wrote down where the behaviour stops.

The second effect is subtler: **declined abstractions get recorded with revisit triggers rather than
argued about twice**. Feature 006 declined eleven candidate types and four packages, each with the
condition that would justify it. §22.2 declines an Application project with three named triggers.
This is the antidote to the agent's documented tendency toward speculative layering — not a rule
saying "don't," but a written record of what would change the answer.

### 5. What task size works best?

**The project ran a natural experiment and the answer is uncomfortable.**

Feature 001 committed per RED/GREEN step: ~55 commits for 69 tasks. Features 003–006 committed per
user story: 4–6 commits for 65–146 tasks.

Fine granularity made Principle I *auditable* — the history itself is the proof, and that is what
T064 and T066 check. Coarse granularity was faster and produced an unreadable audit trail: a commit
titled `feat: fitness, fatigue, and form for any daily load history (T001-T020, User Story 1)`
cannot be checked for test-before-code order at all.

Note where the three TDD lapses appear: in feature 006, the coarse-commit end of the project. The
compliance review found them only because someone went looking afterwards. In feature 001, the
history would have shown them immediately.

**Recommendation**: task size should be per-scenario regardless, but *commit* granularity should stay
fine for anything where the order is the evidence. The speed gained by batching is paid back at
review time, and it is paid back with interest when the thing being reviewed is process compliance
rather than output.

### 6. Does TDD with AI produce better domain design, or merely more tests?

**Better design — but with a blind spot the test count actively conceals.**

The design case is real. `TrainingLoadAggregator`, `MetricsChart`, `SyncMessage` and
`DashboardViewBuilder` are pure functions tested without a renderer or a database. No mocking
library was ever introduced — the constitution forbade it by default and no genuine need arose in
three days of work across four layers. The domain project has **zero** package and project
references and was not modified at all during features 005 and 006. That is unusually clean, and
test-first pressure is the plausible cause: code that is hard to instantiate in a test doesn't get
written that way twice.

Now the blind spot. **Two defects reached a human using the application, past 409 green tests:**

1. **[fix: accept a fractional utc_offset from Strava](https://github.com/juusimaa/training-load-analyzer/commit/402abc8)** —
   a real account returned `utc_offset: 10800.0` where the fixtures, built from the documented
   schema, always produced a bare integer. The sync crashed outright. No test could have caught it,
   because every fixture in the suite was written by the same process that wrote the parser.
2. **[fix: give the metrics chart the stylesheet that paints it](https://github.com/juusimaa/training-load-analyzer/commit/4c22009)** —
   the chart's polylines carried `class="line line-fitness"` and no `stroke`, relying on CSS rules
   that were never written. The bUnit tests assert on the emitted DOM, and the DOM was correct. The
   chart was invisible. The assessment notes the stylesheet was still 37 lines of untouched Blazor
   template.

These are the same defect twice: **the suite verifies the boundary it also defines.** Fixtures test
the parser against the shape the parser expects; DOM assertions test the markup against the markup.
Neither reaches the two things that were actually real — Strava's output and a browser's rendering.

The three machine-dependent defects the compliance review catalogues (invariant-culture SVG
coordinates, `TimeProvider.GetLocalNow()`, `ToLocalTime()`) are a third instance: all three were
correct on this laptop and wrong elsewhere. The `LANG=fi_FI.UTF-8` run in
[README.md](README.md) exists so the first cannot return.

**Answer**: TDD with an AI agent produces genuinely better *internal* design and essentially no
protection at the edges, while producing a test count that reads like protection at the edges.

### 7. How much of Spec Kit is useful structure, and how much is ceremony?

**12,294 lines of specification against 12,001 lines of code.** Roughly 1:1. That ratio has to be
defended item by item, not in aggregate.

**Earned its place:**

- **research.md** — 110 recorded decisions across six features, each with reasoning and, where
  applicable, a revisit trigger. This is the single most reusable artifact. It is where "why isn't
  there an Application project" is answered without re-litigation.
- **`/speckit-analyze`** — caught a CRITICAL that the plan's own compliance gate had passed, plus
  two HIGH and four MEDIUM in feature 006 alone, and eight findings in feature 001. Cross-artifact
  checking finds a class of error that reading any single document cannot.
- **The constitution** — Principle I is the reason the TDD lapses were written down instead of
  smoothed over, and Principle III is why the compliance reviews enumerate declined dependencies.
- **Compliance reviews** — feature 005's found and fixed a genuine architectural violation
  (`StravaAuthorization` sitting in `Strava/` while referencing `Persistence/`), and feature 006's is
  the most useful document in the repository.

**Ceremony, on this evidence:**

- **`/speckit-clarify` never ran.** No spec has a Clarifications section. Gaps were instead caught
  during `/plan` and `/analyze` — later, but caught. For a solo project where the spec author and the
  decision-maker are the same person, a dedicated clarification round appears to be redundant with
  planning.
- **The user-story independence model** doesn't fit analytical domains. Feature 002's task list says
  so directly: "The template's model assumes stories are mutually independent. These are not, and the
  spec says so." Several features restated this. Useful structure should not need to be argued with
  in every feature.
- **quickstart.md** largely duplicated material already in the tasks and the README.
- **Task renumbering churn.** Feature 001's analyze findings required rewriting a 48-task list
  wholesale "since four insertions would otherwise have shifted 48 ids and their cross-references."
  Dense numeric identifiers make traceability greppable and make insertion expensive.

---

## What this exercise did not test

Stated plainly so the conclusions above are not over-read:

- **Greenfield, solo, three days.** Nothing here says anything about Spec-Driven Development in an
  existing codebase, across a team, or over a timescale where specifications go stale.
- **No human code review has happened yet.** Every "clean design" claim rests on the agent's own
  compliance reviews and on 409 tests. That is exactly the evidence question 6 shows to be weakest
  at the edges.
- **Two verification tasks are unrun** — T125 (SC-005's ten-second incremental sync bound) and T126
  (the quickstart end-to-end on a real account). Both need real Strava credentials.
- **The load model was never validated against reality.** Edwards TRIMP was chosen, specified, and
  implemented consistently. Whether its output is *useful training guidance* is untested and outside
  the MVP.
- **Strava MCP was available as a development-time aid and appears to have been little used.** The
  fixtures were built from the documented schema. The `utc_offset` defect is what that costs.

---

## What to change next time

1. **Prove tests discriminate, as a routine step.** Break the production code; confirm the test goes
   red. Feature 006 did this six times and found one worthless test. This costs minutes and is the
   only check that catches a test which cannot fail.
2. **Commit per RED/GREEN step whenever process compliance is the thing being evaluated.** The
   history is the audit trail; batching destroys it, and the TDD lapses appeared at the batched end
   of the project.
3. **Build at least one fixture from real captured data per integration.** Anonymized, per the
   constitution — but shaped by reality rather than by documentation.
4. **Add one check per feature that the software is actually usable.** Not a broader unit suite. The
   chart defect needed someone to look at the page; that is a different kind of check and no amount
   of DOM assertion substitutes for it.
5. **Run `/speckit-analyze` before implementation, always.** It caught what the planning gate missed
   in the one feature where those two things disagreed.
6. **Skip `/speckit-clarify` on solo work**, and fold its questions into planning, which is where
   they surfaced anyway.

---

## Remaining before the project is closed

- [ ] T125 — time an incremental sync against SC-005's ten-second bound
- [ ] T126 — run [specs/006-dashboard/quickstart.md](specs/006-dashboard/quickstart.md) end to end on
      a real account
- [ ] Human code review, per the constitution's Definition of Done
- [ ] Close out §22.2 in the project plan — its revisit trigger was Feature 5, which shipped without
      an Application project
