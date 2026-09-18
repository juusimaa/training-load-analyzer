# Constitution Compliance Review: Dashboard

**Feature**: 006-dashboard | **Date**: 2026-09-18 | **Task**: T129

Reviewed against [constitution.md](../../.specify/memory/constitution.md) v1.0.0 after
implementation. 406 tests green, `dotnet build -warnaserror` clean.

| Principle | Verdict | Evidence |
|---|---|---|
| **I. Strict TDD** | ⚠️ **Largely held, with recorded lapses** | See below — this is the honest section |
| **II. Domain Independence from Strava** | ✅ | `Domain` and `Infrastructure` end the feature **byte-identical** — tree hashes `f77a0e1…` and `fbc29cd…` match what T002 recorded, and `git diff main` is empty for both. `Features/Dashboard` imports only `Domain` and `Persistence`; `Features/Sync` only `Sync`; `Endpoints` only `Strava` and `Sync`. Checked by `scripts/compliance-006.sh` |
| **III. Simplicity Before Abstraction** | ✅ | Eleven candidate types declined with triggers in data-model.md. Four packages declined: charting library, mocking library, EF InMemory, browser automation — all checked by script. `TrainingLoadAnalyzer.Application` revisited (R2) and still not created. Two packages added, both test-only, plus the host itself |
| **IV. Testability** | ✅ | No mocking library. The two test doubles are feature 005's, **linked not copied**. Most of the feature is a pure function (`DashboardViewBuilder`, `MetricsChart`, `SyncMessage`) tested without a renderer or a database; bUnit covers only what has no non-rendering formulation |
| **V. Isolation of External Integrations** | ✅ | OAuth, tokens, paging and rate limits stayed in `Infrastructure`, unmodified. The host adds two endpoints that *call* `StravaAuthorization` and hold none of that logic. The one place this came under pressure — R13's concurrency guard — was resolved without reaching across the boundary |
| **VI. Observability & Deliberate Error Handling** | ✅ | Ten failure paths, each logged or named (T128). The reader catches **named** exception types, never `Exception`, and never `OperationCanceledException`. `SyncMessage` is total by construction. No logging infrastructure introduced beyond the framework's `ILogger` |
| **VII. Specification Adherence** | ⚠️ **One item missed during planning, since closed** | Four specification problems were escalated and answered (R4–R7). A fifth — FR-012's browser storage — was settled silently in the design and only caught by `/speckit-analyze` afterwards (R23). Recorded in plan.md's amendments table rather than quietly fixed |

## Principle I, honestly

**Most of the feature arrived RED first**, and six discriminating checks were verified by
deliberately breaking the production code to confirm they noticed:

| Check | Broken how | Result |
|---|---|---|
| T031 | reader switched to `GetUtcNow` | failed — `18.9.2026` for the expected `19.9.2026` |
| T061 | planted `const decimal Significant = 0.05m` | **passed at first — the test was worthless.** Its regex ended in `(?![\w])`, which rejects a C# decimal literal's `m` suffix. Corrected, re-planted, now fails |
| T065 | `InvariantCulture` removed from `MetricsChart` | failed |
| T091 | exchange moved ahead of the state check | failed — the stub recorded a forged code reaching Strava's token endpoint |
| T103 | one-at-a-time guard removed | **deadlocked** two concurrent walks, which is its own confirmation |
| T105 | coordinator registered scoped instead of singleton | failed |

**Three lapses, none tidied away:**

1. **T015 passed on first run.** The Blazor template ships `Home.razor` at `@page "/"`, so the route
   already existed. Deleting the template page made it genuinely RED before the GREEN step. Caught
   and corrected; no production code was written against a green test.
2. **T024 and T031 were written after the code that satisfies them.** T021's and T029's GREEN steps
   over-reached — the empty-history guard and the local-day derivation belonged to the tasks that
   were meant to force them. Both tests are real (T031 was proved to discriminate), but the order
   was wrong, and that is not TDD however the coverage reads afterwards.
3. **`RecentActivity.cs` existed at the end of Phase 3**, because `DashboardView.Recent` needs the
   type to compile. It is a data shape with no behaviour, never populated until T079, but the phase
   checkpoint's "no recent-activity code" was not literally true.

**The pattern worth naming**: every lapse was a GREEN step doing more than the failing test asked
for. That is the specific way Principle I erodes in UI work, and it happened three times in a feature
whose own task list warned about it.

## Three defects of the same shape

The feature produced three machine-dependent defects, all found here rather than in review:

1. **SVG coordinates** built without the invariant culture render `points="0,45,3 1,5,12,25"` on a
   Finnish machine — wrong geometry, no exception, correct on an en-US agent (research R9).
2. **`TimeProvider.GetLocalNow()`** follows the build machine's zone unless `LocalTimeZone` is also
   pinned, which would move every date assertion on CI (research R22).
3. **`ToLocalTime()`** in the rate-limit message read the machine's zone; the test asserting "07:15"
   passed only because this laptop is UTC+03:00. Now converted by the coordinator, where the zone is
   known.

All three are invisible on one machine and wrong on another. The suite is run under
`LANG=fi_FI.UTF-8` (T121) precisely so the first cannot come back.

## Not verified

Two tasks require a real Strava account and could not be run in this environment:

- **T125** — SC-005's ten-second bound on an incremental sync. The path is exercised against the stub
  handler, but the timing is unmeasured.
- **T126** — the quickstart end to end on a real account: a real consent screen, a real rate limit,
  two browser tabs.

Neither is a defect; both are unverified. They are the developer's to run.

## Human review

Constitution §Development Workflow requires that the AI-generated code has been reviewed by a human
before the feature is considered complete. **That has not happened yet**, and this review does not
substitute for it.
