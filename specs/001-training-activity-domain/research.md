# Phase 0 Research: Training Activity Domain

**Feature**: 001-training-activity-domain | **Date**: 2026-09-16

The project constitution already fixes the broad technology choices (C#, modern .NET, xUnit).
This document resolves the decisions it leaves open, plus the design questions that the
specification's requirements force but do not themselves answer.

---

## R1: Target framework

- **Decision**: `net10.0`, C# 14. Both projects target it; no multi-targeting.
- **Rationale**: SDK 10.0.100 is installed on the development machine (9.0.304 is also present).
  The constitution says "modern .NET"; there is no reason for this feature to target anything
  older, and nothing in it depends on a framework version.
- **Alternatives considered**: `net9.0` — no benefit, and would pin the whole solution lower later.
  Multi-targeting — pure speculation under Principle III; nothing consumes this library externally.

## R2: Test framework

- **Decision**: xUnit v3 (`xunit.v3`, 4.x), with `xunit.runner.visualstudio` and
  `Microsoft.NET.Test.Sdk` retained.
- **Rationale**: The constitution mandates xUnit. v3 is the current line — 4.0.0 shipped
  2026-08-14, 4.0.1 is on NuGet — and has Microsoft Testing Platform support built in. Keeping the
  VSTest runner packages alongside it is what xUnit itself recommends until every development
  environment has moved to MTP, which matters here because the work happens in both VS Code and the
  terminal.
- **Alternatives considered**: xUnit v2 — still works, but starting a new project on the previous
  major line means a migration for no gain.

## R3: Assertion library

- **Decision**: built-in xUnit assertions (`Assert.*`). No assertion package.
- **Rationale**: The constitution permits "FluentAssertions **or** built-in xUnit assertions", so
  this is an open choice, and FluentAssertions now carries a licensing question. From v8 it moved
  from Apache 2.0 to the Xceed Community License: free for open-source and non-commercial use, but
  commercial use requires a paid licence at $130 per developer per year. Version 7 remains Apache
  2.0 indefinitely. This project is a personal learning exercise, so v8 would likely qualify as
  non-commercial — but "likely qualify" is a poor reason to take on a dependency the feature does
  not need. What this feature asserts is small and concrete: a decimal load value, a provenance
  flag, an exception type, and a message naming the violated rule. `Assert.Equal`, `Assert.Throws`,
  and `Assert.Contains` cover all of it. Principle III applies to test dependencies as much as to
  production abstractions.
- **Alternatives considered**:
  - *FluentAssertions 7.x* — Apache 2.0 and stable, but frozen at a version that will not gain
    features, and it pulls in a dependency to buy readability this feature doesn't lack.
  - *AwesomeAssertions* — a community fork of FluentAssertions 7 under Apache 2.0, drop-in
    compatible. The right answer **if** fluent syntax is wanted later; adopting it now would be
    speculative.
  - *Shouldly* — comparable fluent library, permissively licensed. Same objection.
- **Revisit when**: assertions in later features (collections of daily/weekly loads, time series
  comparisons) start becoming hard to read with `Assert.*`. AwesomeAssertions is then the default
  choice, and swapping it in is mechanical.

## R4: Mocking library

- **Decision**: none.
- **Rationale**: Principle IV requires a genuine need. This feature has no collaborators to
  isolate: every unit under test is a value or a pure calculation over values supplied directly.
  Introducing Moq or NSubstitute here would be the exact pattern the constitution calls a defect.

## R5: Project layout

- **Decision**: two projects only —

  ```text
  TrainingLoadAnalyzer.sln
  src/TrainingLoadAnalyzer.Domain/
  tests/TrainingLoadAnalyzer.Domain.Tests/
  ```

- **Rationale**: The preliminary project plan sketches nine projects (Domain, Application,
  Infrastructure, Api, Web, plus four test projects), but explicitly says that sketch "should not
  be treated as a mandatory Clean Architecture template". This feature is pure domain: no
  persistence, no HTTP, no external provider. Creating `Application`, `Infrastructure`, `Api`, or
  `Web` now would create five empty projects with no current need — speculative generalization
  under Principle III. The solution file is created now so later features can add projects to it.
- **Alternatives considered**: the full nine-project layout up front — rejected as speculative.
  A single project with tests inside it — rejected because the Domain assembly must be able to
  declare zero package references, which is how Principle II is enforced mechanically (see R6).

## R6: Enforcing domain independence (Principle II, FR-025)

- **Decision**: `TrainingLoadAnalyzer.Domain` carries **zero** NuGet package references and zero
  project references. This is asserted by inspection of the `.csproj`, not by a test.
- **Rationale**: A domain project whose `.csproj` has no dependencies cannot reference Strava or any
  other provider — the constraint is enforced by the build rather than by discipline. SC-010 ("a
  reviewer finds no mention of Strava") is then a review step, not a test, which is honest: it is a
  property of the source text, not of runtime behaviour.
- **Alternatives considered**: an architecture-test package (NetArchTest, ArchUnitNET) asserting no
  forbidden namespaces — a dependency and a layer of machinery to guard a constraint that an empty
  `<ItemGroup>` already makes impossible. Revisit when `Infrastructure` exists and the boundary
  becomes one that code could actually cross.

## R7: How validation failures are reported (FR-022, FR-023)

- **Decision**: throw from the constructor. A single exception type per violated rule family, with
  a message naming the offending field. No `Result<T>`, no `TryCreate`, no validation-error
  collection.
- **Rationale**: FR-023 requires that no partially-formed record survives a refusal; a throwing
  constructor gives that for free, and is the idiomatic C# way to make an invalid state
  unrepresentable. FR-022 requires the refusal to state which rule was violated, which an exception
  type plus message satisfies. A `Result<T>` type would be a new abstraction with one call site and
  no proven need — Principle III. Nothing in this feature has to report several violations at once.
- **Alternatives considered**:
  - *`Result<T>` / discriminated-union return* — more functional and avoids exceptions for expected
    input errors, but it is scaffolding built before there is a caller that benefits. The import
    feature, which will process batches and must not abort on one bad activity, is the plausible
    trigger.
  - *Collecting all violations* — nothing currently consumes more than the first.
- **Revisit when**: the Strava import feature needs to reject one activity in a batch and carry on.

## R8: Time representation (FR-003, FR-004, FR-007)

- **Decision**:
  - Start time: `DateTimeOffset`.
  - Moving time: `TimeSpan`.
  - Heart-rate sample time: `TimeSpan` measured **from the session's start**, not an absolute
    instant.
- **Rationale**: `DateTimeOffset` is the only BCL type that satisfies FR-003 directly — it keeps the
  instant and the offset, so both UTC and the athlete's local time are recoverable, and SC-008
  (local calendar day across a DST transition) follows from reading `.Offset`. `DateTime` would
  force an out-of-band offset field and invite a `DateTimeKind` bug.

  Sample times are relative because FR-010's hold-until-next rule only ever uses *differences*
  between consecutive samples. Relative times make the load calculation independent of dates and
  timezones entirely, which is what makes SC-003 (determinism) and SC-006 (hand-reproducibility)
  easy to demonstrate — a test fixture reads "150 bpm at minute 0, 175 at minute 10" rather than
  carrying timestamps.
- **Alternatives considered**: absolute `DateTimeOffset` per sample — matches how an external
  provider would supply the data, but drags timezone reasoning into a calculation that has no
  business knowing about it, and makes every test fixture verbose. Conversion belongs in the future
  import feature. NodaTime — a dependency, and R6 rules out any dependency in Domain.

## R9: Numeric type for training load

- **Decision**: `decimal` for TRIMP points, and compute zone time in whole `TimeSpan` arithmetic
  converted to decimal minutes.
- **Rationale**: SC-006 requires that a reviewer reproduce a load value by hand. `decimal` produces
  exactly the number the reviewer computes; `double` produces 79.99999999999999 for the worked
  example in some orderings and forces every assertion to carry a tolerance, which quietly weakens
  the tests. Performance is irrelevant at this scale.
- **Alternatives considered**: `double` — standard for physiological calculations, rejected for
  exactness of hand-verification. `int` — loses fractional minutes, and sample spacing is not
  guaranteed to be whole minutes.

## R10: Zone boundary comparison (FR-009)

- **Decision**: classify a sample by integer cross-multiplication rather than by computing a
  percentage. A sample of `hr` bpm against maximum `max` falls at or above a boundary of `p`
  percent when `hr * 100 >= p * max`.
- **Rationale**: FR-009 makes boundaries inclusive-below/exclusive-above, so a sample landing
  exactly on a boundary must land in the upper zone deterministically. Computing `hr / max * 100` in
  floating point makes exact-boundary cases depend on representation error; integer
  cross-multiplication makes them exact. Both values are small integers, so overflow is not a
  concern. Worked check against User Story 2 scenario 2: `max = 190`, `hr = 150` →
  `15000 >= 70 × 190 = 13300` and `15000 < 80 × 190 = 15200` → zone 3, weight 3. `hr = 175` →
  `17500 >= 90 × 190 = 17100` → zone 5, weight 5.
- **Alternatives considered**: `decimal` percentage arithmetic — also exact, but expresses the rule
  less directly and still needs care about the comparison direction.

## R11: Type shape and equality (FR-024)

- **Decision**:
  - `TrainingActivity`: `sealed class`, all state readonly, no equality override.
  - `HeartRateSample`, `TrainingLoad`, `HeartRateSeries`, `MaximumHeartRate`: value types or
    records with structural equality.
- **Rationale**: FR-024 requires immutability, which readonly state gives regardless of the kind of
  type. `TrainingActivity` deliberately does *not* get record equality: two sessions are the same
  session when their external identifiers match (FR-002), not when every field matches, and a
  record's all-fields equality would silently imply the wrong rule. Leaving reference equality in
  place means no equality semantics are claimed until a feature needs them. The value objects do
  want structural equality — tests compare load values directly, and SC-009 asserts that two loads
  *are equal*.
- **Alternatives considered**: `record TrainingActivity` — convenient `with`-expressions and
  `ToString`, but `with` is mutation-by-copy, which is exactly what FR-024 exists to discourage, and
  the equality semantics are wrong. Implementing identity equality on the external identifier now —
  no current caller needs it; the deduplication that would need it lives in the storage feature.

## R12: Where the load calculation lives

- **Decision**: an instance method on `TrainingActivity` taking the athlete's maximum heart rate as
  its argument. No calculator class, no interface, no dependency injection.
- **Rationale**: The specification phrases it this way — the Training Activity entity "exposes its
  training load given the athlete's maximum heart rate" — and FR-015 requires the maximum heart rate
  to be an input rather than owned state, which an argument expresses precisely. A
  `TrainingLoadCalculator` service with a single implementation and no substitution need is the
  case Principle III names as a defect. There is nothing to inject: the calculation is a pure
  function of the activity's own data and one argument.
- **Alternatives considered**: a separate `TrainingLoadCalculator` class — rejected as an
  abstraction without a current need. An `ITrainingLoadCalculator` interface — rejected more firmly;
  a single implementation with no substitution need is precisely the anti-pattern the constitution
  targets.
- **Revisit when**: a second load method has to coexist with Edwards TRIMP (the specification
  currently mandates exactly one), or when a caller must choose a method at runtime.

## R13: Representing measured versus estimated load (FR-014)

- **Decision**: `TrainingLoad` is a single value object carrying both the points and a provenance
  enum (`Measured` / `Estimated`). There is no way to obtain the number without the provenance.
- **Rationale**: FR-014 requires consumers to be able to tell the two apart "regardless of the
  numeric value", and SC-007 requires that no consumer can obtain a load value without the marking.
  Returning a bare `decimal` plus a separate flag would let a caller drop the flag; bundling them
  makes that impossible by construction. The 40-minute no-heart-rate session and the worked
  heart-rate session in User Story 2 both produce 80 points, which is a deliberate reminder in the
  specification that the number alone is ambiguous.
- **Alternatives considered**: two distinct types (`MeasuredLoad` / `EstimatedLoad`) — stronger
  still, but it forces every future consumer to handle two types before there is any consumer that
  treats them differently. Revisit if aggregation in Feature 2 turns out to need them separated.

---

## Resolved unknowns

No `NEEDS CLARIFICATION` items remain. The specification entered Phase 0 with none, and every
decision above is either fixed by the constitution or resolved here with a recorded rationale and a
revisit trigger.

## Sources

- [Fluent Assertions Library v8 Abandons Apache Licensing — InfoQ](https://www.infoq.com/news/2025/01/fluent-assertions-v8-license/)
- [Frequently Asked Questions About Fluent Assertions — Xceed](https://xceed.com/fluent-assertions-faq/)
- [Another open source project shifts to restrictive license — devclass](https://devclass.com/2025/01/16/another-open-source-project-shifts-to-restrictive-license-fluent-assertions-following-xceed-partnership/)
- [Core Framework v3 4.0.0 — xUnit.net](https://xunit.net/releases/v3/4.0.0)
- [Microsoft Testing Platform (xUnit.net v3) — xUnit.net](https://xunit.net/docs/getting-started/v3/microsoft-testing-platform)
