# Quickstart: Fitness, Fatigue, and Form

**Feature**: 003-fitness-fatigue-form | **Date**: 2026-09-17

How to build and confirm this feature does what [spec.md](./spec.md) says. This is a build-and-validate
guide — the implementation is driven by `/speckit-tasks` and `/speckit-implement`, one RED/GREEN cycle
at a time.

## Prerequisites

- .NET SDK 10.0.100 or later — verify with `dotnet --version`
- Features 001 and 002 merged and green: `DailyTrainingLoad`, `LoadBasis`, and `DateRange` must exist.
- Nothing else. No database, no API credentials, no network access.

## Scaffolding

**None.** Like feature 002, this feature creates no project, adds no package, and changes no `.csproj`.
Two new types land in the existing `src/TrainingLoadAnalyzer.Domain/`, and the new tests in the
existing `tests/TrainingLoadAnalyzer.Domain.Tests/`.

If a task asks you to add a project or a package reference, that is a signal the design drifted —
check it against [research.md](./research.md) R1 before doing it.

## Everyday loop

```bash
dotnet test                                       # all tests: 001, 002, and 003 together
dotnet test --filter FullyQualifiedName~Metrics   # one area while cycling
dotnet build -warnaserror                         # before calling a cycle done
```

Features 001 and 002 must stay green throughout. This feature is purely additive; a red 001 or 002
test means something was changed that should not have been.

## The two numbers everything else is built on

Both verified on the installed SDK, not taken from memory:

```text
α_fitness = 1 − e^(−1/42) = 0.023528313347756735
α_fatigue = 1 − e^(−1/7)  = 0.1331221002498184
```

A single day of load 100 applied to a zero seed therefore gives Fitness `2.352831335` and Fatigue
`13.31221002`. If those two numbers are right, FR-005, FR-012, and FR-029a are all working; if either
is wrong, nothing downstream is worth checking yet.

## Validating the feature

The specification's acceptance scenarios are the validation. Each becomes a test; the feature is done
when they all pass. These are the ones worth checking by hand, because they are where a plausible
implementation goes wrong quietly. All figures below were computed on the installed SDK.

1. **The `1 ÷ N` trap** (FR-005, C21). After 42 days of a constant load of 100 from a zero seed,
   Fitness is **63.212056**. The `1 ÷ 42` approximation gives **63.654405** — a gap of 0.44, more than
   four thousand times the 0.0001 tolerance. This is the single most likely wrong implementation, it
   looks entirely reasonable in a diff, and a test asserting a hand-rounded `63.2` would not catch it.
   Assert with `tolerance: 0.0001`.

2. **The zero seed** (FR-012, C21). Day one of the history advances from exactly 0, so a first day of
   load 100 yields `2.352831335`, not 100 and not 50. An implementation that seeds from the first
   day's own load — a plausible-looking "warm start" — gives 100 here.

3. **Rest days decay, they do not skip** (FR-009, C22). Load 100 on day one then nothing on day two:
   Fitness `2.2974731819`, Fatigue `11.5400606675`. An implementation that iterates only the days
   that had training leaves both unchanged at day one's values.

4. **Fatigue falls faster than Fitness** (US1 scenario 3, SC-005). From a series settled at a load of
   100 (200 days), seven rest days take Fitness from `99.1451` to `83.9245` — down 16% — and Fatigue
   from `100.0000` to `36.7879` — down 63%. Form goes from `−0.8549` to `+47.1365`. If Form does not
   turn positive here, the two time constants are the wrong way round.

5. **A hard block pushes Form negative** (US1 scenario 2). From that same settled series, seven days
   at a load of 200 give Fitness `114.6281`, Fatigue `163.2121`, Form `−48.5839`.

6. **Sustained training settles Form at zero** (US1 scenario 1, SC-004). 365 days of an unchanging
   load of 100 gives Fitness `99.9832`, Fatigue `100.0000`, Form `−0.0168` — both metrics within 0.02%
   of the load, comfortably inside SC-004's 1%.

7. **The warm-up boundary** (FR-014, C23). For a history beginning 2026-01-01: 2026-02-11 is the 42nd
   day and is **not yet reliable**; 2026-02-12 is the 43rd and **is** reliable. Off-by-one here is the
   easiest mistake in the feature and no figure changes when it is wrong, so only the flag reveals it.

8. **History before the range is used but not returned** (FR-011, FR-024, C19). A history from
   2026-01-01 with a range of 2026-03-01 to 2026-03-31 must return **31** entries whose first day
   already carries two months of accumulated training — not 90 entries, and not 31 entries starting
   from a zero seed on 2026-03-01.

9. **The basis window forgets** (FR-019, SC-009, C25). A single estimated day 8 days ago must leave
   `FatigueBasis` untouched while `FitnessBasis` is still `Mixed`; the same day 43 days ago must leave
   both untouched. An implementation that combines the basis over the whole history marks everything
   `Mixed` forever after the athlete's first ride without a heart-rate strap.

10. **A zero-point session is not a rest day** (FR-018, FR-020, C24). A day with `ActivityCount 0` and
    `Basis None` contributes nothing to any window. A day whose sessions totalled exactly 0 points —
    `ActivityCount 1`, `Basis Measured` — does contribute, so a window containing only that day reads
    `Measured`, not `None`. Both produce identical Fitness and Fatigue.

11. **Refusals are told apart** (FR-022, FR-022a, FR-023, FR-026, C28). Five refusals share the `ParamName`
    `"history"`. Write each test against the message, not just the exception type, or a test for "the
    history has a gap" will pass against "the history starts too late".

12. **An all-zero history is not a refusal** (FR-025, C29). Every day at 0 points returns the full
    series with both metrics decaying toward zero and every basis `None`.

## Assertion style

Different from feature 002, and deliberately so ([research.md](./research.md) R7):

```csharp
// Exact — these members are not floating point
Assert.Equal(new DateOnly(2026, 3, 1), day.Day);
Assert.False(day.IsReliable);
Assert.Equal(LoadBasis.Mixed, day.FitnessBasis);

// Tolerance — FR-029 states one, so do not compare these exactly
Assert.Equal(63.212056, day.Fitness, tolerance: 0.0001);

// Do NOT do this: record-struct equality compares double exactly
// Assert.Equal(new DailyTrainingMetrics(...), actual[0]);
```

`Assert.Equal(double, double, tolerance:)` is available in the installed `xunit.v3.assert` 4.0.1 —
verified, not assumed.

## Checking the constitution held

```bash
# Principle II / FR-030 / SC-013 / C30 — still no dependencies, still no provider named
grep -c "PackageReference\|ProjectReference" src/TrainingLoadAnalyzer.Domain/TrainingLoadAnalyzer.Domain.csproj   # expect 0
grep -ril "strava" src/ | grep -v obj/                                                                            # expect no output

# Principle III — no interface introduced for a single implementation
grep -rn "public interface" src/ --include='*.cs'                                                                 # expect no output

# Principle IV — no mocking library crept in
grep -ril "moq\|nsubstitute\|fakeiteasy" tests/ --include='*.csproj'                                               # expect no output

# FR-005 / C21 — the 1/N approximation is nowhere in the source
grep -rn "/ 42\|/ 7\.0\|/42\.0" src/ --include='*.cs'          # expect only the Math.Exp arguments

# FR-007 / FR-027 / C26 — no clock is read
grep -rn "DateTime.Now\|DateTime.Today\|DateTimeOffset.Now\|DateOnly.FromDateTime(DateTime.N" src/ --include='*.cs'  # expect no output

# R2 — double is confined to the metrics; loads are still decimal
grep -rn "double" src/TrainingLoadAnalyzer.Domain/ --include='*.cs' | grep -v "Metrics"   # expect no output
```

## Two rules that came from amendments

Both were specification gaps found during planning and answered by the developer before implementation
(research R10, R11). They are easy to get wrong precisely because the original spec did not state
them:

- **An empty Fatigue window does not make Form mixed** (FR-019a). Fitness window `Measured`, Fatigue
  window `None` — trained three weeks ago, rested since — gives `FormBasis` `Measured`. Since
  Fatigue's 7 days always sit inside Fitness's 42, `FormBasis` equals `FitnessBasis` on every day
  (FR-019b), which is why it is a derived property and not a stored field. A test that finds them
  differing has found a bug in the derivation, not an interesting edge case.
- **A history ending before the range is refused** (FR-022a), not truncated and not padded with rest
  days. It is one of the six refusals in item 11 above.
