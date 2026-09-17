# Quickstart: Training Load Aggregation

**Feature**: 002-training-load-aggregation | **Date**: 2026-09-17

How to build and confirm this feature does what [spec.md](./spec.md) says. This is a
build-and-validate guide — the implementation is driven by `/speckit-tasks` and
`/speckit-implement`, one RED/GREEN cycle at a time.

## Prerequisites

- .NET SDK 10.0.100 or later — verify with `dotnet --version`
- Feature 001 merged and green: `TrainingActivity`, `TrainingLoad`, and `LoadProvenance` must exist.
- Nothing else. No database, no API credentials, no network access.

## Scaffolding

**None.** Unlike feature 001, this feature creates no project, adds no package, and changes no
`.csproj`. Every new type lands in the existing `src/TrainingLoadAnalyzer.Domain/`, and every new
test in the existing `tests/TrainingLoadAnalyzer.Domain.Tests/`.

If a task asks you to add a project or a package reference, that is a signal the design drifted —
check it against [research.md](./research.md) R1 before doing it.

## Everyday loop

```bash
dotnet test                                          # all tests, 001 and 002 together
dotnet test --filter FullyQualifiedName~Aggregation  # one area while cycling
dotnet build -warnaserror                            # before calling a cycle done
```

Feature 001's tests must stay green throughout. This feature is purely additive; a red 001 test
means something was changed that should not have been.

## Validating the feature

The specification's acceptance scenarios are the validation. Each becomes a test; the feature is
done when they all pass. These are the ones worth checking by hand, because they are where a
plausible implementation goes wrong quietly:

1. **The gap-free series** (US1 scenario 2, C8). One session on 2026-03-02 over the range
   2026-03-01 to 2026-03-05 must return **five** entries, not one. The commonest wrong
   implementation groups the activities and returns only the days that had training.

2. **The offset-local day** (US1, FR-004, C11). A session at `2026-03-02T00:30:00+02:00` belongs to
   **2026-03-02**, even though its UTC instant falls on 2026-03-01. Verified on the installed SDK:
   `DateTimeOffset.Date` is offset-local. A test that only ever uses `+00:00` sessions will pass
   against a UTC implementation and prove nothing.

3. **The Monday boundary** (US2 scenario 2, SC-008). Sunday 2026-03-01 is ISO `2026-W09`; Monday
   2026-03-02 is `2026-W10`. Two sessions a day apart must land in different weeks.

4. **The extended edge week** (US2 scenario 5, FR-013, C9/C10). Range Wednesday 2026-03-04 to
   Thursday 2026-03-05, with a 60-point session on Monday 2026-03-02: the weekly result is **one**
   entry for 2026-03-02 → 2026-03-08 **including** those 60 points, while the daily result contains
   only 03-04 and 03-05 and **excludes** them. This is the single most consequential decision in
   the feature and the easiest to implement inconsistently.

5. **The 53-week year** (US2 scenario 6). 2026 is a 53-week ISO year: 2027-01-03 belongs to
   `2026-W53`, and 2027-01-04 opens `2027-W01`. A range crossing that boundary must produce
   consecutive weeks with no gap and no duplicate. Also confirm 2025-12-29 is `2026-W01`.

6. **Rest day versus zero day** (US3 scenario 4, FR-016, C12). A day with no sessions is
   `(0 points, count 0, None)`. A day whose sessions all scored 0 points is `(0 points, count n,
   Measured)`. If these two are indistinguishable, `ActivityCount` or `Basis` is not being set.

7. **One estimate taints the week** (US3 scenario 5, FR-015). A week with one estimated session
   among several measured ones is `Mixed`, not `Measured`.

## Checking the constitution held

```bash
# Principle II / FR-025 / SC-010 / C18 — still no dependencies, still no provider named
grep -c "PackageReference\|ProjectReference" src/TrainingLoadAnalyzer.Domain/TrainingLoadAnalyzer.Domain.csproj   # expect 0
grep -ril "strava" src/ | grep -v obj/                                                                            # expect no output

# Principle IV — no mocking library crept in
grep -ril "moq\|nsubstitute\|fakeiteasy" tests/ --include='*.csproj'                                                # expect no output

# Principle III — no interface was introduced for a single implementation
grep -rn "public interface" src/ --include='*.cs'                                                                   # expect no output

# FR-004 / C11 — the UTC path is never taken for day assignment
grep -rn "UtcDateTime\|ToUniversalTime\|TimeZoneInfo" src/ --include='*.cs'                                         # expect no output
```

## Known boundary

A range ending inside the final ISO week of year 9999 cannot have its closing Sunday computed —
`DateOnly.MaxValue` is a Friday, and `AddDays` throws `ArgumentOutOfRangeException`. The
specification places no upper bound on a range and states no rule for this, so none is invented
here; see [research.md](./research.md) R13. It is not reachable from any real training date.
