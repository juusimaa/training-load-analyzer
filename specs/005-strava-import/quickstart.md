# Quickstart: Strava Import

**Feature**: 005-strava-import | **Date**: 2026-09-17

How to build and confirm this feature does what [spec.md](./spec.md) says. This is a build-and-validate
guide — the implementation is driven by `/speckit-tasks` and `/speckit-implement`, one RED/GREEN cycle
at a time.

**This feature is different from 001–004 and the differences matter.** It creates projects, adds
packages, opens databases, and talks to a third party. The habits that served four features of pure
domain work — no scaffolding, no dependencies, everything deterministic — do not apply, and the guard
rails that replace them are in [Checking the constitution held](#checking-the-constitution-held).

## Prerequisites

- .NET SDK 10.0.100 or later — verify with `dotnet --version`
- Features 001–004 merged and green. `dotnet test` should report **204 passing** before a line of this
  feature is written.
- For the unit and integration tests: **nothing else**. No network, no database file, no Strava
  account. That is FR-042 and SC-011, and it is a requirement rather than a convenience.
- For the optional manual check against real Strava only: an application registered at
  <https://www.strava.com/settings/api>, giving a client id and client secret.

## Scaffolding

Unlike features 002–004, this one **does** scaffold. Once, at the start:

```bash
dotnet new classlib -o src/TrainingLoadAnalyzer.Infrastructure -f net10.0
dotnet new xunit3 -o tests/TrainingLoadAnalyzer.Infrastructure.Tests -f net10.0

dotnet sln add src/TrainingLoadAnalyzer.Infrastructure
dotnet sln add tests/TrainingLoadAnalyzer.Infrastructure.Tests

dotnet add src/TrainingLoadAnalyzer.Infrastructure \
  reference src/TrainingLoadAnalyzer.Domain
dotnet add tests/TrainingLoadAnalyzer.Infrastructure.Tests \
  reference src/TrainingLoadAnalyzer.Infrastructure

dotnet add src/TrainingLoadAnalyzer.Infrastructure \
  package Microsoft.EntityFrameworkCore.Sqlite --version 10.0.12
dotnet add src/TrainingLoadAnalyzer.Infrastructure \
  package Microsoft.EntityFrameworkCore.Design --version 10.0.12

dotnet new tool-manifest
dotnet tool install dotnet-ef --version 10.0.12
```

**Pin 10.0.12, not 10.0.0.** EF Core 10.0.0 pulls a transitive `SQLitePCLRaw.lib.e_sqlite3` 2.1.11 and
restore raises `NU1903` for a high-severity advisory. 10.0.12 depends on 2.1.12 and restores clean.

**The `xunit3` template must be reconciled with this repository's conventions.** Match
`tests/TrainingLoadAnalyzer.Domain.Tests/TrainingLoadAnalyzer.Domain.Tests.csproj`: the
`xunit.v3.mtp-v2` package, `OutputType=Exe`, the `Xunit` implicit using, and the `xunit.runner.json`
content item. `global.json` already selects the Microsoft.Testing.Platform runner for the solution.

**Do not add** `Microsoft.EntityFrameworkCore.InMemory` (research R8), a mocking library (R9), or a
resilience package (R17). If a task asks you to, the design drifted — check it against
[research.md](./research.md) first.

**The Domain project must end this feature exactly as it started it**: zero `PackageReference`, zero
`ProjectReference`. That is checkable, and it is checked below.

## Everyday loop

```bash
dotnet test                                                 # everything: 001-005
dotnet test --project tests/TrainingLoadAnalyzer.Domain.Tests        # must stay at 204 green
dotnet test --project tests/TrainingLoadAnalyzer.Infrastructure.Tests
dotnet build -warnaserror                                   # before calling a cycle done
```

Features 001–004 must stay green throughout. This feature is purely additive to them; a red domain test
means something was changed that should not have been (FR-041).

## The two test doubles, and why they are not mocks

Everything in this feature is testable without a network or a file, using two hand-written pieces
totalling well under a hundred lines.

**A stubbed HTTP handler** (research R9). Strava is reached only through `HttpClient`, so a
`HttpMessageHandler` subclass returning queued responses controls the whole integration — status codes,
**headers**, and bodies. Headers matter more here than bodies: `X-ReadRateLimit-Usage` is what drives
the rate-limit behaviour, and no mocking library expresses that more clearly than a switch statement.

**Real SQLite, in memory** (research R8):

```csharp
var connection = new SqliteConnection("DataSource=:memory:");
connection.Open();                      // keep it open for the whole test class
var options = new DbContextOptionsBuilder<ImportDbContext>()
    .UseSqlite(connection)              // the CONNECTION, never the connection string
    .Options;
```

Three traps, all verified by running them:

- Pass the connection **string** instead and EF Core opens and closes per operation, so the schema is
  created and the database then evaporates: `SQLite Error 1: 'no such table: Activities'`.
- Close and reopen the same connection object and the data is gone. Pooling does not preserve a
  `:memory:` database.
- Two `:memory:` connections are two separate databases. That is the isolation you want — one
  connection per test class, safe in parallel.

**Do not reach for the EF Core InMemory provider.** Side by side, it *succeeds* at
`OrderBy(a => a.StartedAt)` and `Where(a => a.StartedAt > x)`, both of which throw on real SQLite. A
suite built on it would green-light queries that fail in production.

## The one thing SQLite will not do

```text
OrderBy(x => x.StartedAt)       -> NotSupportedException: SQLite does not support expressions
                                   of type 'DateTimeOffset' in ORDER BY clauses
Where(x => x.StartedAt > cut)   -> InvalidOperationException: could not be translated
```

`DateTimeOffset` round-trips through SQLite **exactly**, offset included — that was verified across
`+03:00`, `+05:45`, `-04:00` and `+00:00`, at one-tick precision. Fidelity is not the problem;
querying is. Every query this feature makes is a range over start time, so `ActivityRow` stores
`StartedAtUtcTicks` and `StartedAtOffsetMinutes` instead ([data-model.md](./data-model.md), research
R5).

Reconstruct defensively — the second form below overflows at the extremes of the range:

```csharp
new DateTimeOffset(new DateTime(utcTicks, DateTimeKind.Utc))
    .ToOffset(TimeSpan.FromMinutes(offsetMinutes));       // correct
new DateTimeOffset(utcTicks + offset.Ticks, offset);      // overflows
```

## Validating the feature

Each user story is independently runnable, in priority order.

**User Story 1 — connect.** Drive `ExchangeAsync` against a stubbed token response and read the stored
connection; restart the context and read it again; expire the access token on the fake clock and
confirm a sync renews it; have the stub reject the renewal and confirm `ReconnectionRequired` rather
than a generic failure. The rotation test is the one that matters most: return a *different* refresh
token from the refresh response and assert the stored one changed (research R13).

**User Story 2 — import.** A stubbed history of runs, rides, and swims; assert the counts, the skip
reasons, and the mapped fields. Check the offset case explicitly: an activity with `utc_offset` of
`10800` must come back as `+03:00`, so it lands on the right local day. Check that `moving_time` was
used and `elapsed_time` was not.

**User Story 3 — incremental.** Import, add one activity to the stub, sync, and assert both what was
stored **and what was requested** — the second assertion is the requirement. Then: a late upload dated
inside the look-back window; a deletion inside it; a deletion outside it that survives; a sport type
corrected to `EBikeRide`.

**User Story 4 — limits and interruption.** Have the stub return 429, and separately fail mid-page.
Assert that what was stored is kept, that the outcome names the reason, that `RetryAfter` is the next
quarter-hour, and — the important one — that **nothing was removed** (C61).

### Fixtures

Purpose-built and anonymized (FR-043). Strava MCP may be used while writing them, to learn what real
payloads look like and to find edge cases; the payloads themselves do not enter the repository.

Start from the field list in [data-model.md](./data-model.md) rather than from a captured response.
Note that `utc_offset` and `has_heartrate` appear only in Strava's example payloads and in no published
schema, so they must be treated as optional even though real responses always carry them.

## Checking against real Strava (optional, manual)

This feature builds everything from the authorization code onward; catching the redirect belongs to
Feature 6 (research R11). So the manual check has one paste step:

```bash
export STRAVA_CLIENT_ID=...          # never committed - .gitignore already covers .env
export STRAVA_CLIENT_SECRET=...
```

Visit the URL `BuildAuthorizeUrl` produces with `redirect_uri=http://localhost/` — `localhost` and
`127.0.0.1` are white-listed by Strava — approve, and copy the `code` parameter out of the browser's
address bar. The page will fail to load; that is expected, there is nothing listening. Pass the code to
`ExchangeAsync`.

**The code is single-use and short-lived.** A second attempt with the same code fails, which is correct
behaviour and not a bug in the exchange.

**Watch the budget while doing this.** Reads are limited to 100 per 15 minutes and 1,000 per day, and a
first import of a multi-year history costs roughly 140 requests (research R24) — so it will stop once
at the fifteen-minute limit and resume. That is the design working, not a failure.

## Checking the constitution held

Run these before calling the feature done. Each maps to a principle that this feature, unlike its
predecessors, could plausibly break.

```bash
# Principle II / FR-018 / C69 - the domain gained no dependency
grep -E 'PackageReference|ProjectReference' src/TrainingLoadAnalyzer.Domain/*.csproj ; echo "expect: no output"

# Principle II / C69 - no Strava vocabulary anywhere in the domain
grep -ril -E 'strava|oauth|sport_type|access_token' src/TrainingLoadAnalyzer.Domain/ ; echo "expect: no output"

# FR-041 - features 001-004 untouched and still green
git diff --stat main -- src/TrainingLoadAnalyzer.Domain tests/TrainingLoadAnalyzer.Domain.Tests
dotnet test --project tests/TrainingLoadAnalyzer.Domain.Tests    # expect: 204 passed

# FR-005 / C51 / C66 - no credential reachable from a result or a log
grep -rn -E 'Console\.|Log.*[Tt]oken|ToString.*[Tt]oken' src/TrainingLoadAnalyzer.Infrastructure/

# FR-017f / C71 - samples are filtered in the mapper, never by relaxing the domain
git diff main -- src/TrainingLoadAnalyzer.Domain/HeartRateSeries.cs ; echo "expect: no output"

# FR-021 / C58 - no load computed during import
grep -rn 'CalculateTrainingLoad\|TrimpPoints' src/TrainingLoadAnalyzer.Infrastructure/ ; echo "expect: no output"

# FR-042 / SC-011 - the test suite touches no network and no file
grep -rn 'DataSource=[^:]' tests/TrainingLoadAnalyzer.Infrastructure.Tests/ ; echo "expect: no output"

# Principle III / research R8, R9, R17 - none of the declined dependencies crept in
grep -rn -E 'InMemory|Moq|NSubstitute|FakeItEasy|Polly|WireMock' \
  src/TrainingLoadAnalyzer.Infrastructure tests/TrainingLoadAnalyzer.Infrastructure.Tests
```

The last one is worth keeping. Four features of "no dependencies" discipline end here, and the risk is
not that a package is added deliberately — it is that one is added in passing, to solve a problem
research already solved without it.

## Three decisions worth knowing before you start

[research.md](./research.md) R2, R14 and R21 were put to the developer during planning and answered on
2026-09-17. Two changed the specification, and the answers are easy to undo by accident while coding:

- **R2 — no `IActivitySource` in the domain.** If you find yourself adding an interface to
  `TrainingLoadAnalyzer.Domain`, stop. The project boundary is what satisfies Principle II here; the
  port arrives in Feature 6, shaped by its first consumer outside Infrastructure.
- **R14 — a narrower grant is refused, not accepted.** `FR-002a`. A connection that cannot see private
  activities is not a degraded connection, it is a refused one, because the resulting history would be
  wrong in a way nothing downstream could detect.
- **R21 — implausible heart-rate samples are discarded and counted.** `FR-017f`, `FR-017g`. Discard in
  the **mapper**, on the raw stream arrays, before `HeartRateSeries` is constructed. The domain's
  invariants do not move; Infrastructure simply never hands it a sample it would refuse. A series is
  unusable only when fewer than two survive.

Worth a test of its own: assert that a ride whose stream opens with zero-bpm samples still ends up with
**measured** load. That single test is what keeps the measured window from quietly becoming pointless.
