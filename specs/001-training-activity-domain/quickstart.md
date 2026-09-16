# Quickstart: Training Activity Domain

**Feature**: 001-training-activity-domain | **Date**: 2026-09-16

How to create the solution, run the tests, and confirm the feature does what
[spec.md](./spec.md) says. This is a build-and-validate guide — the implementation itself is driven
by `/speckit-tasks` and `/speckit-implement`, one RED/GREEN cycle at a time.

## Prerequisites

- .NET SDK 10.0.100 or later — verify with `dotnet --version`
- Nothing else. No database, no API credentials, no network access.

## One-time scaffolding

Run from the repository root. This is structure only; no production type is created here, because
under Principle I every type arrives via a failing test first.

```bash
dotnet new sln --name TrainingLoadAnalyzer

dotnet new classlib --name TrainingLoadAnalyzer.Domain --output src/TrainingLoadAnalyzer.Domain --framework net10.0
dotnet new xunit3 --name TrainingLoadAnalyzer.Domain.Tests --output tests/TrainingLoadAnalyzer.Domain.Tests --framework net10.0

dotnet sln add src/TrainingLoadAnalyzer.Domain/TrainingLoadAnalyzer.Domain.csproj
dotnet sln add tests/TrainingLoadAnalyzer.Domain.Tests/TrainingLoadAnalyzer.Domain.Tests.csproj

dotnet add tests/TrainingLoadAnalyzer.Domain.Tests/TrainingLoadAnalyzer.Domain.Tests.csproj \
  reference src/TrainingLoadAnalyzer.Domain/TrainingLoadAnalyzer.Domain.csproj

rm src/TrainingLoadAnalyzer.Domain/Class1.cs
```

If `dotnet new xunit3` is not available on the installed SDK, run `dotnet new install xunit.v3.templates`
first, or create a plain `xunit` project and replace its `xunit` package reference with `xunit.v3`.

Then confirm the empty solution builds and the test project is discoverable:

```bash
dotnet build
dotnet test
```

Expected: build succeeds, `dotnet test` reports zero tests. That zero is the correct starting point
— the first real test is the first RED step.

## Everyday loop

```bash
dotnet test                                    # all tests
dotnet test --filter FullyQualifiedName~Load   # one area while cycling
dotnet build -warnaserror                      # before calling a cycle done
```

## Validating the feature

The specification's acceptance scenarios are the validation. Each becomes a test; the feature is
done when they all pass. The four worth checking by hand, because they are where a plausible
implementation goes wrong quietly:

**1. The worked TRIMP example** (User Story 2, scenario 2) — maximum heart rate 190, samples of
150 bpm at minute 0, 175 bpm at minute 10, 160 bpm at minute 20.

Expected: `80` points, `Measured`. By hand: 150/190 is 78.9% → zone 3, weight 3, held 10 minutes →
30. 175/190 is 92.1% → zone 5, weight 5, held 10 minutes → 50. The final sample contributes
nothing. If you get a value near but not equal to 80, the implementation is using `double`
somewhere it should use `decimal` (see [research.md](./research.md) R9).

**2. The estimate** (User Story 2, scenario 9) — no heart-rate data, 40 minutes of moving time.

Expected: `80` points, `Estimated`. The number deliberately collides with case 1: if a test passes
while asserting only on `Points`, it is not actually testing FR-014. Assert the provenance.

**3. Boundary classification** (FR-009) — maximum heart rate 200, a sample at exactly 140 bpm.

Expected: zone 3, weight 3. 140 is exactly 70%, and the lower bound is inclusive. A floating-point
implementation can land this in zone 2; the integer cross-multiplication in
[research.md](./research.md) R10 cannot.

**4. Cross-modality** (SC-009) — one running activity and one cycling activity with identical
series and identical maximum heart rate.

Expected: the two `TrainingLoad` values are equal. If they are not, `ActivityType` has leaked into
the calculation, violating FR-012.

## Checking the constitution held

Three checks that no test covers, to run before calling the feature complete:

```bash
# Principle II / FR-025 / SC-010 — the domain has no dependencies and names no provider
cat src/TrainingLoadAnalyzer.Domain/TrainingLoadAnalyzer.Domain.csproj   # expect no PackageReference, no ProjectReference
grep -ril "strava" src/ tests/                                           # expect no output

# Principle IV — no mocking library crept in
grep -rl -E "Moq|NSubstitute|FakeItEasy" tests/                          # expect no output
```

And the one that cannot be automated: read the git history for the feature and confirm each test
commit precedes the production code that makes it pass. Coverage numbers prove nothing here
(Principle I); commit order is the evidence that the process was actually followed.
