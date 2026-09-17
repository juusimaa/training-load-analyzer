# Quickstart: Load Trends

**Feature**: 004-load-trends | **Date**: 2026-09-17

How to build and confirm this feature does what [spec.md](./spec.md) says. This is a build-and-validate
guide — the implementation is driven by `/speckit-tasks` and `/speckit-implement`, one RED/GREEN cycle
at a time.

## Prerequisites

- .NET SDK 10.0.100 or later — verify with `dotnet --version`
- Features 001, 002, and 003 merged and green: `WeeklyTrainingLoad`, `IsoWeek`, `LoadBasis`, and
  `DateRange` must exist. `dotnet test` should report 143 passing before a line of this feature is
  written.
- Nothing else. No database, no API credentials, no network access.

## Scaffolding

**None.** Like features 002 and 003, this feature creates no project, adds no package, and changes no
`.csproj`. Two new types land in the existing `src/TrainingLoadAnalyzer.Domain/`, and the new tests in
the existing `tests/TrainingLoadAnalyzer.Domain.Tests/`.

If a task asks you to add a project or a package reference, that is a signal the design drifted —
check it against [research.md](./research.md) R1 before doing it.

## Everyday loop

```bash
dotnet test                                     # all tests: 001-004 together
dotnet test --filter FullyQualifiedName~Trend   # one area while cycling
dotnet build -warnaserror                       # before calling a cycle done
```

Features 001, 002, and 003 must stay green throughout. This feature is purely additive; a red test in
any of them means something was changed that should not have been.

## The rule everything else is built on

Six steps, evaluated on every read of `Classification` ([data-model.md](./data-model.md)):

```text
1.  not IsComplete            -> Indeterminate
2.  |AbsoluteChange| < 50     -> Steady
3.  RelativeChange is null    -> skip step 4; the floor already decided
4.  |RelativeChange| < 0.15   -> Steady
5.  AbsoluteChange > 0        -> SignificantIncrease
6.  otherwise                 -> SignificantDecrease
```

The order is the behaviour, not a detail. Step 1 before anything else is what makes FR-017
unreachable-by-construction; step 2 before step 4 is what makes FR-013 and FR-014 fall out instead of
needing a branch of their own.

**Verified against all sixteen cases** — every acceptance scenario in the specification, the relative
discriminator that none of them covers, and the four threshold boundaries — on 2026-09-17, using
decimal arithmetic. The tables below are that run's output, not expectations typed from memory.

## Two numbers that need care

**`decimal`, not `double`.** `610.4m - 505.7m` is exactly `104.7`; the same subtraction in `double`
gives `104.69999999999999`, which fails FR-033 and guarantee C32. Three of four realistic TRIMP pairs
fail that way ([research R2](./research.md#r2-numeric-type-for-the-two-changes)). If a `double`
appears anywhere in this feature, it is a defect.

**FR-011 needs two tests, not one.** The requirement says a change of "exactly 0.15 with exactly 50
points" is significant, but that pair is not exactly representable — it needs a previous week of
`50 / 0.15 = 333.333…`. Pin the two thresholds separately:

| previous | current | tests | expected |
|---------:|--------:|-------|----------|
| 400 | 460 | relative exactly `0.15` | `SignificantIncrease` |
| 400 | 459 | just under | `Steady` |
| 200 | 250 | absolute exactly `50` | `SignificantIncrease` |
| 200 | 249 | just under | `Steady` |

## Validating the feature

The specification's acceptance scenarios are the validation. Each becomes a test; the feature is done
when all of them pass and nothing else was built. Expected classifications, confirmed by calculation:

| Scenario | previous | current | absolute | relative | expected |
|----------|---------:|--------:|---------:|---------:|----------|
| US1.1 | 500 | 600 | `+100` | `+0.2` | `SignificantIncrease` |
| US1.2 | 600 | 450 | `-150` | `-0.25` | `SignificantDecrease` |
| US1.3 | 500 | 500 | `0` | `0` | `Steady` |
| US2.2 | 500 | 520 | `+20` | `+0.04` | `Steady` |
| US2.3 | 20 | 26 | `+6` | `+0.3` | `Steady` — under the floor |
| FR-010 | 1000 | 1060 | `+60` | `+0.06` | `Steady` — under the relative threshold |
| US2.4 | 600 | 400 | `-200` | `-0.333…` | `SignificantDecrease` |
| US2.5 | 500 | 0 | `-500` | `-1` | `SignificantDecrease` |
| US3.1 | 0 | 400 | `+400` | *(null)* | `SignificantIncrease` |
| US3.2 | 0 | 0 | `0` | *(null)* | `Steady` |
| US3.3 | 500 | 200 | `-300` | `-0.6` | `Indeterminate` — partial week |
| FR-014 | 0 | 30 | `+30` | *(null)* | `Steady` — gentle return |

Note US2.3 and FR-010 in particular — they are the pair that justifies having two thresholds instead
of one, and neither alone does it. An implementation applying only the relative threshold passes
FR-010 and fails US2.3; one applying only the absolute floor does the reverse. FR-010's case is
deliberately not an acceptance scenario in the specification: no scenario there covers that
direction, which is why it is listed here under the requirement itself.

US3.1 is the third case a naive implementation gets wrong, for an unrelated reason: it is the
ordering test. A rule that consults the relative threshold before the floor reports it as `Steady`
and so never flags a return to training.

### Building a history for a test

The calculator takes weekly totals directly, so no activities are needed:

```csharp
var w = IsoWeek.For(new DateOnly(2026, 3, 2));   // Monday of 2026-W10
var history = new[]
{
    new WeeklyTrainingLoad(w, 500m, 4, LoadBasis.Measured),
    new WeeklyTrainingLoad(IsoWeek.For(w.Monday.AddDays(7)), 600m, 5, LoadBasis.Measured),
};
```

Remember that the week **before** the range must be in the history, or the call is refused by FR-022.
A range covering only the second week above is the smallest valid request.

### Checking completeness

`IsComplete` comes from the range alone, never from the totals:

| range | week | complete? |
|-------|------|-----------|
| Mon 2026-03-02 → Sun 2026-03-08 | 2026-W10 | yes |
| Mon 2026-03-02 → Wed 2026-03-04 | 2026-W10 | no — cut at the end |
| Wed 2026-03-04 → Sun 2026-03-08 | 2026-W10 | no — cut at the start |

A range running Monday to Sunday makes every week complete, which is the natural way to ask and the
way to avoid `Indeterminate` entirely. Why the rule is conservative, and the case where it knowingly
withholds a valid judgement, is in [research R8](./research.md#r8-completeness-is-a-property-of-the-range-not-of-the-data).

## Assertion style

Unlike feature 003, **whole-value equality is the right style again**:

```csharp
Assert.Equal(
    new WeeklyLoadTrend(week, 600m, 500m, IsComplete: true, LoadBasis.Measured),
    actual);
```

Every member is `decimal`, `bool`, or an enum, all of which compare exactly, so the record struct's
generated equality is sound here. Feature 003 had to avoid this because `double` members compare
exactly when they should not. Asserting the whole value also catches a member nobody thought to
check — which is how feature 002 found two defects late (its T061, T062).

The derived members need no separate assertion in most tests: if `Points` and `PreviousPoints` are
right, `AbsoluteChange` cannot be wrong. Assert `Classification` directly where the threshold rule is
what is under test.

**Do not assert against the threshold constants.** They are private precisely so this is impossible;
a test that read them would assert only that the code agrees with itself
([research R7](./research.md#r7-where-the-thresholds-live)). Use literals or, better, the point totals
in the tables above.

## Checking the constitution held

```bash
# Principle II / FR-034 / C45 — still no dependencies, still no provider named
grep -rn "PackageReference\|ProjectReference" src/TrainingLoadAnalyzer.Domain/*.csproj
grep -rni "strava" src/TrainingLoadAnalyzer.Domain/

# Principle III — no interface introduced for a single implementation
grep -rn "interface I" src/TrainingLoadAnalyzer.Domain/

# R2 / C32 — no double anywhere in this feature's files
grep -n "double" src/TrainingLoadAnalyzer.Domain/WeeklyLoadTrend.cs \
                 src/TrainingLoadAnalyzer.Domain/TrainingLoadTrendCalculator.cs

# FR-005 / FR-031 / C43 — no clock is read
grep -rn "DateTime.Now\|DateTime.UtcNow\|DateTimeOffset.Now\|Today" src/TrainingLoadAnalyzer.Domain/

# FR-029 / C31 — one entry point, no second detection pass
grep -n "public static" src/TrainingLoadAnalyzer.Domain/TrainingLoadTrendCalculator.cs
```

Each should return nothing, except the last, which must show exactly one `public static` method.

## The rule that came from an amendment

**FR-022b** — a history that stops before the range's last week is refused, naming the shortfall — was
a gap found while planning, not while specifying. It was put to the developer and answered on
2026-09-17 ([research R11](./research.md#r11--resolved--a-history-that-stops-before-the-ranges-last-week)).

Worth a deliberate test rather than treating it as symmetric boilerplate with FR-022: the tempting
implementation zero-fills the missing weeks, and the first thing this feature would then do is report
a significant decrease for a week that has no data behind it at all.
