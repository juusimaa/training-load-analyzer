---

description: "Task list for feature 005 — Strava Import"
---

# Tasks: Strava Import

**Input**: Design documents from `/specs/005-strava-import/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/infrastructure-api.md](./contracts/infrastructure-api.md),
[quickstart.md](./quickstart.md)

**Tests**: MANDATORY. The template treats test tasks as optional; Constitution Principle I (Strict
TDD, NON-NEGOTIABLE) overrides that. Every production member below arrives via a failing test, with
one deliberate and explicitly-marked exception — Phase 1.

**Organization**: Tasks are grouped by user story. Each task is one step of a
RED → GREEN → REFACTOR → VERIFY cycle, tied to a numbered acceptance scenario in
[spec.md](./spec.md).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel — different files, no dependency on an incomplete task
- **[Story]**: US1 / US2 / US3 / US4, mapping to the user stories in spec.md
- **RED**: write a test that fails, and confirm it fails *for the stated reason*
- **GREEN**: the minimum production code that makes it pass — nothing more
- **CONFIRM**: a test expected to pass with no new production code. It pins behaviour the design
  already implies. If it fails, that is a real defect, not a cue to write more code
- **VERIFY**: run the whole suite and check the cycle's discipline held

## ⚠️ The Principle I risk in this feature, and what to do about it

Features 001–004 tested pure functions: the first RED step could always be written immediately.
**This one cannot.** Before a single assertion can run, a project must exist, a package must be
restored, and two test doubles must be written. That is a standing invitation to build the plumbing,
get a sync working end to end, and write tests afterwards — which Principle I says is not TDD
regardless of the coverage that results.

Two rules keep it honest, and the plan's Constitution Check records them as this feature's named
mitigation:

1. **Phase 1 is the only non-TDD phase.** It creates empty projects and adds packages. It creates
   **no production type** — not a `DbContext`, not a client, not an entity. It is marked as an
   exception here so it cannot be quietly extended into one.
2. **Every task from T008 onward starts RED.** If a GREEN task seems to need a type that no RED step
   asked for, that is design drift — stop and re-read [research.md](./research.md).

## Path Conventions

Two projects already exist and are **not modified by this feature**:
`src/TrainingLoadAnalyzer.Domain/` and `tests/TrainingLoadAnalyzer.Domain.Tests/`.

Two are created in Phase 1: `src/TrainingLoadAnalyzer.Infrastructure/` and
`tests/TrainingLoadAnalyzer.Infrastructure.Tests/`, laid out per [plan.md](./plan.md) —
`Strava/`, `Persistence/`, `Sync/` in the first; `Fakes/` plus one file per user story in the second.

## Fixture activities

Every test below draws from this set. They are **purpose-built and anonymized** (FR-043): the
identifiers are invented, and no field came from a real account. Strava MCP may be used to check that
the *shapes* are realistic; its output does not enter the repository.

The clock is fixed at **2026-09-17T10:07:33Z** in every test (research R10), so the 180-day measured
window of FR-017a opens at **2026-03-21**.

| # | `id` | `sport_type` | `start_date` | `utc_offset` | `moving_time` | `has_heartrate` | Expected |
|---|------|--------------|--------------|--------------|---------------|-----------------|----------|
| A1 | `11000000001` | `Run` | `2026-09-10T04:30:00Z` | `10800` | `3120` | `true` | Running, **measured** — also pins FR-015 and FR-016 |
| A2 | `11000000002` | `Ride` | `2026-09-12T05:00:00Z` | `10800` | `5400` | `true` | Cycling, **measured** |
| A3 | `11000000003` | `Swim` | `2026-09-11T05:00:00Z` | `10800` | `2400` | `true` | **Skipped** — `SportOutOfScope` |
| A4 | `11000000004` | `EBikeRide` | `2026-09-13T05:00:00Z` | `10800` | `4800` | `true` | **Skipped** — `SportOutOfScope` (FR-010) |
| A5 | `11000000005` | `TrailRun` | `2026-09-05T05:00:00Z` | `10800` | `4200` | `false` | Running, estimated |
| A6 | `11000000006` | `GravelRide` | `2026-09-06T05:00:00Z` | `10800` | `7200` | `false` | Cycling, estimated |
| A7 | `11000000007` | `Run` | `2026-09-01T05:00:00Z` | `10800` | `0` | `false` | **Skipped** — `UnusableByDomain` (FR-011) |
| A8 | `11000000008` | `Run` | `2026-08-28T05:00:00Z` | `10800` | `1800` | `false` | Running, estimated — `manual: true` (FR-013) |
| A9 | `11000000009` | `Ride` | `2024-01-15T09:00:00Z` | `7200` | `5400` | `true` | Cycling, estimated — **outside the window**, no stream request |
| A10 | `11000000010` | `Run` | `2026-09-14T05:00:00Z` | `10800` | `2700` | `true` | Running, estimated, **`HeartRateOutstanding`** — streams 404 |

A1 additionally carries `elapsed_time: 4080`, so any test asserting 52 minutes of moving time fails
against an implementation that reached for elapsed time (FR-016, spec scenario US2.4). Its
`utc_offset` of `10800` is `+03:00`, which puts its `04:30Z` start at **07:30 local** — spec scenario
US2.3, and the reason it lands on the correct local day.

### Heart-rate stream fixtures

| # | `time` | `heartrate` | Expected |
|---|--------|-------------|----------|
| S1 | `[0, 60, 120, 180]` | `[0, 0, 142, 150]` | 2 samples discarded, 2 survive → **measured**, `DiscardedSamples = 2` (FR-017f, FR-017g) |
| S2 | `[0, 60, 120]` | `[0, 0, 0]` | 0 survive → **unusable**, estimated load (FR-017f) |
| S3 | `[0, 300, 600]` | `[95, 142, 171]` | nothing discarded → measured, `DiscardedSamples` absent |
| S4 | *(the key is absent)* | — | Strava returned no heart-rate stream → estimated (research R20) |

**S1 is the fixture that matters most.** It is the strap-warm-up case FR-017f was written for
([research.md](./research.md) R21): without the discarding rule it makes the whole session estimated,
and the 180-day measured window spends a request per activity to achieve nothing. A test asserting
that A1 + S1 yields **measured** load is what keeps that decision alive in the code.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: create the two projects and restore the two packages. **This is the only phase in this
feature that is not test-first**, and it creates no production type — see the Principle I note above.

- [X] T001 Establish the baseline: `dotnet test` reports **204 passing, 0 failing** — features 001–004. Any red here is a pre-existing problem to fix before adding to it
- [X] T002 Record the starting state of the domain, which must not change: `grep -E 'PackageReference|ProjectReference' src/TrainingLoadAnalyzer.Domain/TrainingLoadAnalyzer.Domain.csproj` produces **no output**, and `git rev-parse HEAD:src/TrainingLoadAnalyzer.Domain` gives a tree hash to compare against in T134
- [X] T003 Create `src/TrainingLoadAnalyzer.Infrastructure/` with `dotnet new classlib -f net10.0`, add it to `TrainingLoadAnalyzer.sln`, and add a project reference to `src/TrainingLoadAnalyzer.Domain/`. The reference runs **one way only** — Domain must never reference Infrastructure (FR-018, C69)
- [X] T004 Create `tests/TrainingLoadAnalyzer.Infrastructure.Tests/` with `dotnet new xunit3 -f net10.0`, add it to the solution, and reference `src/TrainingLoadAnalyzer.Infrastructure/`
- [X] T005 Reconcile the generated test `.csproj` with this repository's conventions by matching `tests/TrainingLoadAnalyzer.Domain.Tests/TrainingLoadAnalyzer.Domain.Tests.csproj`: the `xunit.v3.mtp-v2` package at 4.0.1, `OutputType=Exe`, the `Xunit` implicit `Using`, and a copied `xunit.runner.json` content item. `global.json` already selects the Microsoft.Testing.Platform runner solution-wide
- [X] T006 Add `Microsoft.EntityFrameworkCore.Sqlite` **10.0.12** and `Microsoft.EntityFrameworkCore.Design` **10.0.12** (`PrivateAssets="all"`) to `src/TrainingLoadAnalyzer.Infrastructure/`. **Pin 10.0.12, not 10.0.0** — 10.0.0 pulls a transitive `SQLitePCLRaw.lib.e_sqlite3` 2.1.11 and restore raises `NU1903` for a high-severity advisory (research R7). Confirm `dotnet restore` is warning-free
- [X] T007 Create a local tool manifest and install `dotnet-ef` 10.0.12 (`dotnet new tool-manifest`, `dotnet tool install dotnet-ef --version 10.0.12`). Local rather than global so the version is pinned in the repository (research R7)

**Checkpoint**: `dotnet build` succeeds, `dotnet test` still reports 204 passing, and the two new
projects contain **no production type at all**. If either contains a `DbContext`, a client, or an
entity at this point, Phase 1 was over-extended.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: the two test doubles. This is **test code**, so Principle I does not require a failing
test first — but nothing here may reference a production type that does not yet exist.

- [X] T008 [P] Write `tests/TrainingLoadAnalyzer.Infrastructure.Tests/Fakes/StubHttpMessageHandler.cs` — an `HttpMessageHandler` subclass holding a queue of `(predicate, HttpResponseMessage)` pairs, recording every request it received so a test can assert on **what was asked for**, not only on what came back. It must be able to return arbitrary status codes and arbitrary **headers**; the rate-limit behaviour of Phase 6 lives entirely in headers. No mocking library (research R9, Principle IV)
- [X] T009 [P] Write `tests/TrainingLoadAnalyzer.Infrastructure.Tests/Fakes/SqliteFixture.cs` — generic over `TContext : DbContext`, opening a `SqliteConnection("DataSource=:memory:")` and **keeping it open** for the fixture's lifetime, passing the **connection object** to `UseSqlite`. Three traps, all verified in research R8: passing the connection *string* makes EF Core open and close per operation so the schema evaporates (`SQLite Error 1: 'no such table'`); closing and reopening the same connection destroys the data; two `:memory:` connections are two separate databases — which is the isolation wanted, so one fixture per test class
- [X] T010 [P] Write `tests/TrainingLoadAnalyzer.Infrastructure.Tests/Fakes/FixedClock.cs` — a `TimeProvider` pinned to `2026-09-17T10:07:33Z`, with a method to advance it. Three requirements depend on "now" and cannot be tested without controlling it: FR-017a's measured window, FR-004's expiry check, and FR-035's retry time (research R10)

**Checkpoint**: three test-only files exist, the suite still reports 204 passing, and no production
type has been written. Every task from here on starts RED.

---

## Phase 3: User Story 1 - Connect a Strava account (Priority: P1) 🎯 MVP

**Goal**: an athlete authorizes once, and the analyzer holds a durable, self-renewing permission to
read their activities — refusing clearly when that permission is withheld, mismatched, or revoked.

**Independent Test**: drive the authorization exchange and renewal against the stub handler and read
the stored connection afterwards. Fully testable with no activity ever imported.

### The authorize URL

- [X] T011 [US1] RED: assert FR-001, FR-002 and C47 — `BuildAuthorizeUrl` produces a URL at `https://www.strava.com/oauth/authorize` carrying `client_id`, the given `redirect_uri`, `response_type=code`, the given `state`, and a `scope` that **includes `activity:read_all`**. Confirm it fails to compile because `StravaAuthorization` does not exist — file: `tests/TrainingLoadAnalyzer.Infrastructure.Tests/ConnectionTests.cs`
- [X] T012 [US1] GREEN: create `src/TrainingLoadAnalyzer.Infrastructure/Strava/StravaCredentials.cs` as `public sealed record StravaCredentials(string ClientId, string ClientSecret)` and `src/TrainingLoadAnalyzer.Infrastructure/Strava/StravaAuthorization.cs` with `BuildAuthorizeUrl` only. Nothing else — no exchange, no refresh; T013 is what forces the first of those
- [X] T013 [P] [US1] CONFIRM: assert C46 and C70 — the URL contains no password field of any kind, and the requested scope contains **no `:write` scope**. This analyzer never writes to Strava, and the scope is where that is enforced rather than merely intended — file: `tests/TrainingLoadAnalyzer.Infrastructure.Tests/ConnectionTests.cs`

### Exchanging the code

- [X] T014 [US1] RED: assert scenario US1.1 — given a stub returning a token response with `access_token`, `refresh_token`, `expires_at`, `athlete.id` and a `scope` of `read,activity:read_all`, `ExchangeAsync("code")` returns a connection carrying that athlete id and those credentials. Confirm it fails to compile because `ExchangeAsync` does not exist — file: `tests/TrainingLoadAnalyzer.Infrastructure.Tests/ConnectionTests.cs`
- [X] T015 [US1] GREEN: create `src/TrainingLoadAnalyzer.Infrastructure/Strava/StravaTokens.cs` for the response shape and add `ExchangeAsync` to `StravaAuthorization`, posting to `https://www.strava.com/oauth/token` with `grant_type=authorization_code`. **`expires_at` is epoch seconds**, not a duration — convert it once, here (data-model). Deserialization must ignore unrecognised members so a field Strava adds later cannot fail the exchange (FR-020)
- [X] T016 [US1] CONFIRM: assert FR-020 — a token response carrying three fields this code has never heard of is deserialized without error — file: `tests/TrainingLoadAnalyzer.Infrastructure.Tests/ConnectionTests.cs`

### Storing it, and surviving a restart

- [X] T017 [US1] RED: assert scenario US1.2 and FR-003 — after `ExchangeAsync`, a **new** `ImportDbContext` over the same connection reads back the stored connection. Confirm it fails to compile because `ImportDbContext` and `StravaConnection` do not exist — file: `tests/TrainingLoadAnalyzer.Infrastructure.Tests/ConnectionTests.cs`
- [X] T018 [US1] GREEN: create `src/TrainingLoadAnalyzer.Infrastructure/Persistence/StravaConnection.cs` with exactly the columns in [data-model.md](./data-model.md) — `AthleteId`, `AccessToken`, `RefreshToken`, `ExpiresAtUtcTicks`, `GrantedScopes`, `ConnectedAtUtcTicks` — and `src/TrainingLoadAnalyzer.Infrastructure/Persistence/ImportDbContext.cs` with a single `DbSet<StravaConnection>`. **No activities table yet**; T041 is what forces it. `ClientId` and `ClientSecret` are **not** columns — they belong to the application registration, not to the athlete, and a copied database file must not leak them (research R12)
- [X] T019 [US1] GREEN: create the first migration with `dotnet ef migrations add InitialConnection`, and have the fixture apply migrations rather than `EnsureCreated` — the two do not mix, and starting with `EnsureCreated` is a documented one-way door (research R7)
- [X] T020 [US1] CONFIRM: assert FR-008's precondition — at most one connection row can exist. Attempting to store a second is refused by the schema, not merely by a check in code — file: `tests/TrainingLoadAnalyzer.Infrastructure.Tests/ConnectionTests.cs`

### Renewal, and the refresh token that rotates

- [X] T021 [US1] RED: assert scenario US1.3 and FR-004 — with the fixed clock advanced past `ExpiresAtUtcTicks`, a renewal is performed automatically and the new access token is stored. Confirm it fails because `RefreshAsync` does not exist — file: `tests/TrainingLoadAnalyzer.Infrastructure.Tests/ConnectionTests.cs`
- [X] T022 [US1] GREEN: add `RefreshAsync` to `StravaAuthorization`, posting `grant_type=refresh_token` to the same endpoint, and persist the result
- [X] T023 [US1] RED — **the test that prevents a permanent lockout**: assert C48 and research R13. Have the stub return a refresh response whose `refresh_token` differs from the one that was sent, then assert the **stored refresh token changed**. Confirm it fails against an implementation that persisted only the access token. Strava rotates the refresh token on every successful token request and invalidates the old one immediately; keeping the original works in testing and then locks the athlete out for good, presenting as FR-006's reconnection-required failure — file: `tests/TrainingLoadAnalyzer.Infrastructure.Tests/ConnectionTests.cs`
- [X] T024 [US1] GREEN: persist **both** tokens from every token response, in `StravaAuthorization`, before the access token is used for anything
- [X] T025 [P] [US1] CONFIRM: the same rotation rule holds on `ExchangeAsync`, not only on refresh — file: `tests/TrainingLoadAnalyzer.Infrastructure.Tests/ConnectionTests.cs`

### When the permission is not there

- [X] T026 [US1] RED: assert scenario US1.4, FR-006 and C49 — a stub rejecting the refresh with HTTP 400 causes `RefreshAsync` to throw `ReconnectionRequiredException`, and the stored connection is **left in place** rather than deleted. Confirm it fails because the exception type does not exist. This must be distinguishable from every other failure: reported as a generic error it looks like a bug, and reported as success it looks like an athlete who has not trained — file: `tests/TrainingLoadAnalyzer.Infrastructure.Tests/ConnectionTests.cs`
- [X] T027 [US1] GREEN: add `src/TrainingLoadAnalyzer.Infrastructure/Strava/ReconnectionRequiredException.cs` and throw it from `RefreshAsync` on a rejected renewal. Its message must name what the athlete has to do, and must **not** contain a token (FR-005, C51)
- [X] T028 [US1] RED: assert **FR-002a**, scenario US1.7 and C52 — a token response whose granted `scope` is `read,activity:read` (private-activity access withheld) causes `ExchangeAsync` to refuse, naming the access that was not granted, and **no connection row is written**. Confirm it fails because the scope is currently ignored. This is the amendment from [research.md](./research.md) R14: accepted silently, the import reports success while omitting every private activity, and no figure downstream could ever detect it — file: `tests/TrainingLoadAnalyzer.Infrastructure.Tests/ConnectionTests.cs`
- [X] T029 [US1] GREEN: compare the granted scope against what was requested in `StravaAuthorization.ExchangeAsync`, and refuse when private-activity access is missing. The refusal must be **distinguishable from a rejected credential** (FR-002a) — a different exception type, not a different message on the same one
- [X] T030 [P] [US1] CONFIRM: a grant that *does* include `activity:read_all` is accepted and records its scopes on the connection, so the two paths are pinned against each other — file: `tests/TrainingLoadAnalyzer.Infrastructure.Tests/ConnectionTests.cs`
- [X] T031 [US1] RED: assert scenario US1.6, FR-008 and C50 — with activities stored for athlete `900001`, an exchange returning athlete `900002` is refused and the refusal names the mismatch. Confirm it fails because nothing compares the athlete — file: `tests/TrainingLoadAnalyzer.Infrastructure.Tests/ConnectionTests.cs`
- [X] T032 [US1] GREEN: compare `athlete.id` against the stored connection in `ExchangeAsync` and refuse a mismatch. Two athletes' training must never combine into one history

### Disconnecting

- [X] T033 [US1] RED: assert scenario US1.5 and FR-007 — `DisconnectAsync` discards the stored credentials, leaving no connection row behind. Confirm it fails because `DisconnectAsync` does not exist. The other half of US1.5 — that a sync then refuses — is asserted in Phase 4 at T088, because `StravaActivitySync` does not exist yet and a task must not depend forward on one that does — file: `tests/TrainingLoadAnalyzer.Infrastructure.Tests/ConnectionTests.cs`
- [X] T034 [US1] GREEN: add `DisconnectAsync`, revoking at Strava via `POST https://www.strava.com/oauth/revoke` and then deleting the connection row. Use `/oauth/revoke` rather than the older `/oauth/deauthorize`: as of June 2026 it is the recommended endpoint and it becomes the only supported one in June 2027 (research R11)
- [X] T035 [US1] CONFIRM — *deferred to Phase 4 and completed there, once the activities table existed*: assert FR-007's boundary — disconnecting leaves the **stored activities** untouched. Whether to discard them is the athlete's separate, explicit choice, not a side effect — file: `tests/TrainingLoadAnalyzer.Infrastructure.Tests/ConnectionTests.cs`

### Credentials do not leak

- [X] T036 [P] [US1] CONFIRM — **discriminating check** for FR-005 and C51: assert that neither `ReconnectionRequiredException.ToString()` nor `StravaConnection.ToString()` contains the access or refresh token. A record's compiler-generated `ToString` prints every property, so a `StravaConnection` declared as a `record` would leak both into any log line that interpolates it. This test is what makes that a compile-time-visible choice rather than an accident — file: `tests/TrainingLoadAnalyzer.Infrastructure.Tests/ConnectionTests.cs`
- [X] T037 [P] [US1] CONFIRM: assert FR-005 — `grep -rn 'Console\.\|Debug\.WriteLine' src/TrainingLoadAnalyzer.Infrastructure/` produces no output. Recorded as a test-adjacent check in the same file, re-run in T138
- [X] T038 [US1] REFACTOR: with every connection test green, review `StravaAuthorization` for the one thing this story can get structurally wrong — a path that stores an access token *before* its refresh token. Make the two a single assignment so the ordering cannot drift
- [X] T039 [US1] VERIFY: run `dotnet test` — all green, including all 204 domain tests. Confirm `src/TrainingLoadAnalyzer.Infrastructure/Persistence/` contains **no activity type yet**: this story stored a connection and nothing else
- [X] T040 [US1] VERIFY: confirm the two refusal paths are genuinely distinct types — a withheld scope (FR-002a) and a rejected credential (FR-006) must not be the same exception with different text, because Feature 6 will present them differently

**Checkpoint**: User Story 1 is complete. An athlete can connect once, the connection survives a
restart and renews itself, and every way the permission can be wrong — withheld, mismatched, revoked
— is refused by its own named path.

---

## Phase 4: User Story 2 - Bring in the training history (Priority: P2)

**Goal**: every run and ride Strava holds becomes a stored session the existing domain features can
compute against, and everything the analyzer cannot use is accounted for by identifier and reason.

**Independent Test**: run an import against a stub holding the fixture history above and read the
stored sessions — the right ones present, mapped field for field, the rest accounted for. No
incremental logic and no rate limiting involved.

### The row, and the round trip

- [X] T041 [US2] RED: assert FR-022, FR-024 and C53 — storing A1 as a `TrainingActivity` and reading it back yields a session equal member for member, asserted with `DateTimeOffset.EqualsExact` for the start and `SequenceEqual` for the samples. Confirm it fails to compile because `ActivityRow` and `ActivityStore` do not exist — file: `tests/TrainingLoadAnalyzer.Infrastructure.Tests/PersistenceRoundTripTests.cs`
- [X] T042 [US2] GREEN: create `src/TrainingLoadAnalyzer.Infrastructure/Persistence/ActivityRow.cs` with exactly the columns in [data-model.md](./data-model.md), `init` properties, and `From(...)` / `ToDomain()`. **Store `StartedAtUtcTicks` (`long`) and `StartedAtOffsetMinutes` (`short`), not a `DateTimeOffset`**, and `MovingTimeTicks` (`long`), not a `TimeSpan` — SQLite stores both as text, so `OrderBy` throws `NotSupportedException` and a range `Where` fails to translate (research R5). Every query this feature makes is a range over start time
- [X] T043 [US2] GREEN: reconstruct defensively in `ToDomain()` — `new DateTimeOffset(new DateTime(utcTicks, DateTimeKind.Utc)).ToOffset(TimeSpan.FromMinutes(offsetMinutes))`. The shorter `new DateTimeOffset(utcTicks + offset.Ticks, offset)` overflows at the extremes of the range (research R5)
- [X] T044 [US2] GREEN: create `src/TrainingLoadAnalyzer.Infrastructure/Persistence/ActivityStore.cs` with `UpsertAsync` and `InRangeAsync`, configure `ActivityRow` with a composite primary key of `(Provider, ExternalId)` and an index on `StartedAtUtcTicks`, and add the migration. **Do not map `TrainingActivity` directly** — EF Core binds constructor parameters to properties by name and its fourth parameter is `activityType` against a `Type` property, which fails model build with "No suitable constructor was found" and has no Fluent API fix (research R4)
- [X] T045 [US2] CONFIRM — **the assertion the InMemory provider cannot make**: read the raw columns back with a plain `SqliteCommand` on the same connection and assert their stored form directly. The domain-level round trip in T041 passes against several wrong mappings; this one pins the storage format and is what catches a silent regression (research R8) — file: `tests/TrainingLoadAnalyzer.Infrastructure.Tests/PersistenceRoundTripTests.cs`
- [X] T046 [P] [US2] CONFIRM: assert C53 across awkward offsets — `+05:45`, `-04:00` and `+00:00` all round-trip exactly, including a one-tick fractional second. Non-hour offsets are where a naive minutes conversion breaks — file: `tests/TrainingLoadAnalyzer.Infrastructure.Tests/PersistenceRoundTripTests.cs`
- [X] T047 [US2] RED: assert FR-023, FR-030 and C54 — storing A1 twice leaves exactly one row, and the second store's values win. Confirm it fails with a primary-key violation, which is the correct failure for an insert-only implementation — file: `tests/TrainingLoadAnalyzer.Infrastructure.Tests/PersistenceRoundTripTests.cs`
- [X] T048 [US2] GREEN: make `UpsertAsync` insert-or-overwrite on `(Provider, ExternalId)` in `ActivityStore`
- [X] T049 [P] [US2] CONFIRM — **discriminating check** for C55: two activities with **identical** start, moving time and type but different ids are stored as **two** rows. Identity is the provider's id and nothing else; an implementation that deduplicated on the session's values would pass every other test in this phase — file: `tests/TrainingLoadAnalyzer.Infrastructure.Tests/PersistenceRoundTripTests.cs`

### The heart-rate series as a column

- [X] T050 [US2] RED: assert FR-024 and C53 for the series — a session carrying S3 round-trips with all three samples and their exact tick offsets. Confirm it fails because `HeartRateJson` is not populated — file: `tests/TrainingLoadAnalyzer.Infrastructure.Tests/PersistenceRoundTripTests.cs`
- [X] T051 [US2] GREEN: serialize the series as `[[ticks, bpm], …]` into `HeartRateJson` in `ActivityRow.From`, and rebuild it through the real `HeartRateSeries` constructor in `ToDomain()`. A child table is not available — `HeartRateSample` is a `readonly record struct` and `OwnsMany` requires a reference type, `CS0452` (research R6). Rebuilding through the real constructor is deliberate: it re-checks the domain's invariants so a corrupted column is caught at the boundary
- [X] T052 [P] [US2] CONFIRM: a session with no series stores SQL `NULL` in `HeartRateJson` — asserted against the raw column, not against the reconstructed object, because a JSON string `"null"` would read back the same way — file: `tests/TrainingLoadAnalyzer.Infrastructure.Tests/PersistenceRoundTripTests.cs`
- [X] T053 [P] [US2] CONFIRM: assert C57 — write a malformed `HeartRateJson` value directly with a plain `SqliteCommand`, then assert that reading that row throws from `ActivityStore`, naming the offending activity. This is the guarantee that justified `ActivityRow` over direct mapping (research R4): mapped directly, the domain's constructor would run inside EF Core's materializer and a corrupt row would throw from deep inside a query rather than at the store's boundary, where the sync can report it per row — file: `tests/TrainingLoadAnalyzer.Infrastructure.Tests/PersistenceRoundTripTests.cs`
- [X] T054 [P] [US2] CONFIRM: assert FR-021 and C58 — `ActivityRow` has **no** load column, and `grep -rn 'CalculateTrainingLoad\|TrimpPoints' src/TrainingLoadAnalyzer.Infrastructure/` produces no output. Load is derived on read by the existing domain features, never stored here — file: `tests/TrainingLoadAnalyzer.Infrastructure.Tests/PersistenceRoundTripTests.cs`

### The sport-type table

- [X] T055 [US2] RED: assert FR-009, FR-010 and scenario US2.11 — A5 (`TrailRun`) maps to `Running` and A6 (`GravelRide`) maps to `Cycling`. Confirm it fails to compile because `StravaActivityMapper` does not exist — file: `tests/TrainingLoadAnalyzer.Infrastructure.Tests/ActivityMappingTests.cs`
- [X] T056 [US2] GREEN: create `src/TrainingLoadAnalyzer.Infrastructure/Strava/StravaActivitySummary.cs` and `src/TrainingLoadAnalyzer.Infrastructure/Strava/StravaActivityMapper.cs` with the table from [data-model.md](./data-model.md) — `Run`/`TrailRun`/`VirtualRun` to `Running`, `Ride`/`GravelRide`/`MountainBikeRide`/`VirtualRide` to `Cycling`, everything else out of scope. **Read `sport_type`, never `type`** (FR-010a): the legacy field is lossy by Strava's own documentation — a `TrailRun` reports `type: "Run"` and a `GravelRide` reports `type: "Ride"` — so branching on it silently collapses the distinctions this table depends on
- [X] T057 [US2] RED: assert scenario US2.1 and US2.10 — A3 (`Swim`) and A4 (`EBikeRide`) are both skipped with `SkipReason.SportOutOfScope`, and neither is stored. Confirm it fails because `SkipReason` does not exist — file: `tests/TrainingLoadAnalyzer.Infrastructure.Tests/ActivityMappingTests.cs`
- [X] T058 [US2] GREEN: add `src/TrainingLoadAnalyzer.Infrastructure/Sync/SkippedActivity.cs` with `SkipReason` as `SportOutOfScope` and `UnusableByDomain`, and return skips from the mapper rather than throwing
- [X] T059 [P] [US2] CONFIRM — **discriminating check** for FR-010: assert that `EMountainBikeRide` is skipped. It is the sport type most easily forgotten, it is named in the spec's ignored list for exactly that reason, and an implementation that pattern-matched on names containing "MountainBike" would wrongly accept it — file: `tests/TrainingLoadAnalyzer.Infrastructure.Tests/ActivityMappingTests.cs`
- [X] T060 [P] [US2] CONFIRM: assert FR-010b — a `Run` with `trainer: true` is imported as `Running` like any other. Strava has no treadmill sport type, so indoor training needs no special case and must not acquire one — file: `tests/TrainingLoadAnalyzer.Infrastructure.Tests/ActivityMappingTests.cs`
- [X] T061 [P] [US2] CONFIRM: assert FR-012 and FR-013 — an activity with `private: true` and A8 with `manual: true` are both imported normally. Visibility on Strava is not a statement about whether the training happened, and a session the athlete typed in is still training — file: `tests/TrainingLoadAnalyzer.Infrastructure.Tests/ActivityMappingTests.cs`

### The fields that are easy to get wrong

- [X] T062 [US2] RED: assert scenario US2.3 and FR-015 — A1 maps to a start of `2026-09-10T07:30:00+03:00`, carrying both the instant and the offset, so it falls on the correct local day. Confirm it fails because the offset is dropped — file: `tests/TrainingLoadAnalyzer.Infrastructure.Tests/ActivityMappingTests.cs`
- [X] T063 [US2] GREEN: build the start from `start_date` plus `utc_offset` (seconds) in `StravaActivityMapper`. **Do not parse `start_date_local`** — Strava renders it with a trailing `Z` while it holds local wall-clock time, which is a trap that produces a plausible and wrong answer (research R15)
- [X] T064 [US2] RED: assert scenario US2.4 and FR-016 — A1's moving time is 52 minutes, not the 68 minutes of its `elapsed_time`. Confirm it fails against an implementation that reached for elapsed time — file: `tests/TrainingLoadAnalyzer.Infrastructure.Tests/ActivityMappingTests.cs`
- [X] T065 [US2] GREEN: map `moving_time` in `StravaActivityMapper`
- [X] T066 [P] [US2] CONFIRM: assert FR-014 — A1's `ExternalId` is exactly `"11000000001"`, stored verbatim with no prefix, no parse and no reformat, and the row's `Provider` is `"Strava"` in a separate column — file: `tests/TrainingLoadAnalyzer.Infrastructure.Tests/ActivityMappingTests.cs`
- [X] T067 [US2] RED: assert scenario US2.5 and FR-011 — A7, whose moving time is zero, is skipped with `SkipReason.UnusableByDomain`, and the import **continues** through the rest of the history. Confirm it fails with the `ArgumentOutOfRangeException` the domain's constructor throws, escaping and aborting the walk — file: `tests/TrainingLoadAnalyzer.Infrastructure.Tests/ActivityMappingTests.cs`
- [X] T068 [US2] GREEN: catch the domain's refusal per activity in `StravaActivityMapper`, record the identifier and reason, and continue. One unusable activity must never abort an import (C65)
- [X] T069 [P] [US2] CONFIRM: assert FR-019 — mapping the same summary twice produces the same session. The mapper reads no clock and no ambient state; the **only** time-dependent decision in the import is whether a series is fetched, which happens before the mapper runs (FR-019 as amended) — file: `tests/TrainingLoadAnalyzer.Infrastructure.Tests/ActivityMappingTests.cs`

### Heart rate: the window, the streams, and the dropouts

- [X] T070 [US2] RED: assert scenario US2.8 and FR-017 — A1, inside the measured window with `has_heartrate: true`, causes a streams request and is stored **with** its series. Confirm it fails because no streams call is made. Assert on the **recorded request**, not only on the result — file: `tests/TrainingLoadAnalyzer.Infrastructure.Tests/ActivityMappingTests.cs`
- [X] T071 [US2] GREEN: add `src/TrainingLoadAnalyzer.Infrastructure/Strava/StravaStreamSet.cs` and a streams call to the client at `GET /activities/{id}/streams` with `keys=time,heartrate` and `key_by_type=true`. Send **no `resolution` and no `series_type`** — omitting `resolution` is the only way to get full resolution, since its named values cap at 100/1,000/10,000 points and there is no value meaning "everything" (FR-017c, research R20). A missing stream is an **absent key**, not a null, so key-check rather than index
- [X] T072 [US2] RED: assert scenario US2.9 and FR-017a — A9, dated 2024 and outside the 180-day window, is stored **without** a series and **no streams request is made for it**. Confirm it fails because the window is not consulted. The second half is the point: this is a budget decision before it is a data one — file: `tests/TrainingLoadAnalyzer.Infrastructure.Tests/ActivityMappingTests.cs`
- [X] T073 [US2] GREEN: gate the streams call on all three conditions of FR-017 in the sync — `has_heartrate` true, start inside the 180 days ending at the clock's today, and no series already held. Read the clock through `TimeProvider` (research R10)
- [X] T074 [US2] RED — **the test that keeps the measured window worth having**: assert scenario US2.12, **FR-017f and FR-017g**. A1 with stream S1 (`heartrate: [0, 0, 142, 150]`) is stored with a **two-sample** series carrying **measured** load, and the result reports **2 discarded samples**. Confirm it fails with the `ArgumentOutOfRangeException` `HeartRateSeries` throws for a 0 bpm sample. Without this rule a single strap-warm-up dropout sends the whole session to estimated load, and the 180-day window spends a request per activity to achieve nothing ([research.md](./research.md) R21) — file: `tests/TrainingLoadAnalyzer.Infrastructure.Tests/ActivityMappingTests.cs`
- [X] T075 [US2] GREEN: filter implausible samples in `StravaActivityMapper`, **on the raw stream arrays, before `HeartRateSeries` is constructed**, and record the count. The domain's invariants do not move — Infrastructure simply never hands it a sample it would refuse (C71). Add `DiscardedSamples(string ExternalId, int Count)` to the result
- [X] T076 [US2] RED: assert scenario US2.13 and FR-017f's floor — A2 with stream S2 (every sample implausible) is stored **without** a series and carries estimated load, because fewer than two samples survive. Confirm it fails because one surviving sample is still passed to `HeartRateSeries`. Two is the fewest from which any load can be computed, which is why no threshold beyond it is invented — file: `tests/TrainingLoadAnalyzer.Infrastructure.Tests/ActivityMappingTests.cs`
- [X] T077 [US2] GREEN: treat a series with fewer than two surviving samples as unusable, falling to FR-017d
- [X] T078 [P] [US2] CONFIRM: assert stream fixture S4 and research R20 — a response whose `heartrate` key is simply **absent** yields a session with no series and no error. Requesting `time,heartrate` on an activity without heart rate returns an object containing only `time` — file: `tests/TrainingLoadAnalyzer.Infrastructure.Tests/ActivityMappingTests.cs`
- [X] T079 [US2] RED: assert FR-017d and C67 — A10, whose streams request returns **HTTP 404**, is stored without a series, with `HeartRateOutstanding` true and a recorded reason, and the sync **continues**. Confirm it fails because the 404 is treated as an error. A 404 here means "this activity has no streams" — it is what manual activities return — and is not a failure at all — file: `tests/TrainingLoadAnalyzer.Infrastructure.Tests/ActivityMappingTests.cs`
- [X] T080 [US2] GREEN: treat 404 from the streams endpoint as "no streams" in the client, and set `HeartRateOutstanding` on the row when a series was owed and did not arrive
- [X] T081 [P] [US2] CONFIRM — **discriminating check** for FR-017b and C56: store A1 **with** its series, then run a second import in which the stub would return a *different* series, and assert the stored series is unchanged and no streams request was made. The measured window governs what is newly retrieved, never what is kept; without this test an implementation that refetches on every sync passes everything else — file: `tests/TrainingLoadAnalyzer.Infrastructure.Tests/ActivityMappingTests.cs`

### Walking the whole history

- [X] T082 [US2] RED: assert scenario US2.2 — a history spanning three pages is walked completely, and no activity is imported twice because of a page boundary. Confirm it fails because only the first page is read — file: `tests/TrainingLoadAnalyzer.Infrastructure.Tests/ActivityMappingTests.cs`
- [X] T083 [US2] GREEN: create `src/TrainingLoadAnalyzer.Infrastructure/Strava/StravaApiClient.cs` with a paged walk of `GET /athlete/activities` at `per_page=200`, anchored with `after`. **Do not depend on the order results arrive in** — sort client-side by start time and dedupe by id. Strava's current reference documents no ordering for this endpoint at all; the familiar "`after` returns oldest-first" rests on a 2015 forum post, never restated (research R15). `per_page=200` is likewise real but undocumented — worth a comment saying so
- [X] T084 [US2] GREEN: create `src/TrainingLoadAnalyzer.Infrastructure/Sync/StravaActivitySync.cs`, `SyncResult.cs` and `SyncOutcome.cs`, with `SyncAsync` walking the client's pages, mapping, and storing. `SyncOutcome` gets `Completed` only for now — the other four values all arrive in Phase 6, each forced by its own RED step
- [X] T085 [US2] CONFIRM: assert scenario US2.1 end to end — the full fixture history yields **7 stored sessions** (A1, A2, A5, A6, A8, A9, A10) and **3 skips** (A3 and A4 out of scope, A7 unusable), each skip carrying its identifier and reason. A9 is stored like any other ride — being outside the measured window costs it its heart-rate series, not its place in the history — file: `tests/TrainingLoadAnalyzer.Infrastructure.Tests/ActivityMappingTests.cs`
- [X] T086 [US2] CONFIRM: assert scenarios US2.6 and US2.7 — after an import, a new context reads every session back; and running the same import again leaves the stored count unchanged with nothing duplicated — file: `tests/TrainingLoadAnalyzer.Infrastructure.Tests/ActivityMappingTests.cs`
- [X] T087 [US2] CONFIRM — **the test that shows the feature does what it is for**: assert SC-003. Import the fixture history, read it back through `ActivityStore.InRangeAsync`, and feed those sessions to feature 002's `TrainingLoadAggregator` and feature 003's `TrainingMetricsCalculator` — asserting one daily total, one weekly total, and one fitness figure, with no network and no Strava reachable. Every other task in this feature tests the importer; this is the only one that shows the imported history actually reaches the calculators it exists to serve. A1 and A2 both carry measured load, so the daily totals are reproducible by hand — file: `tests/TrainingLoadAnalyzer.Infrastructure.Tests/ImportedHistoryTests.cs`
- [X] T088 [US2] CONFIRM: the remaining half of scenario US1.5, deferred from T033 — with no connection stored, `SyncAsync` refuses rather than attempting an unauthenticated request — file: `tests/TrainingLoadAnalyzer.Infrastructure.Tests/ImportedHistoryTests.cs`
- [X] T089 [US2] REFACTOR: with the phase green, check the one thing this story can get structurally wrong — Strava vocabulary leaking past the mapper. `grep -rn 'sport_type\|has_heartrate\|utc_offset' src/TrainingLoadAnalyzer.Infrastructure/Persistence/ src/TrainingLoadAnalyzer.Infrastructure/Sync/` must produce no output. `Strava/` knows about a third party, `Persistence/` knows about storage, and only `Sync/` knows both
- [X] T090 [US2] VERIFY: run `dotnet test` — all green, 204 domain tests included. Confirm no `double` and no load calculation appears anywhere in Infrastructure, and that `git diff --stat main -- src/TrainingLoadAnalyzer.Domain` is empty

**Checkpoint**: User Story 2 is complete. A full Strava history imports into sessions the existing
domain features can compute against, recent heart-rate data survives real-world dropouts as measured
load, and everything unusable is accounted for by identifier and reason.

---

## Phase 5: User Story 3 - Keep it up to date without re-reading four years (Priority: P3)

**Goal**: the second and every later sync asks only for what has happened since the last one, catches
a late upload, and reconciles away what the athlete deleted — without ever deleting on the strength
of a read that did not finish.

**Independent Test**: import the fixture history, change the stub, sync again, and inspect both what
was **requested** and what was stored. The request assertion is the requirement.

### The resume point

- [X] T091 [US3] RED: assert scenario US3.1 and FR-027 — after an import, adding one new ride and syncing again stores one session, leaves the others untouched, and issues a request whose `after` parameter is **not** the epoch. Confirm it fails because every sync re-reads the whole history — file: `tests/TrainingLoadAnalyzer.Infrastructure.Tests/IncrementalSyncTests.cs`
- [X] T092 [US3] GREEN: create `src/TrainingLoadAnalyzer.Infrastructure/Persistence/SyncState.cs` with `ResumePointUtcTicks`, `LastSyncStartedAtUtcTicks` and `LastOutcome` (FR-025), add the migration, and have `SyncAsync` read the resume point and pass it as `after`
- [X] T093 [US3] RED: assert scenario US3.3 and FR-028 — an activity dated three days before the latest stored one, uploaded late, is imported by a routine sync. Confirm it fails because the resume point sits exactly at the latest stored activity, so the late upload is never asked for — file: `tests/TrainingLoadAnalyzer.Infrastructure.Tests/IncrementalSyncTests.cs`
- [X] T094 [US3] GREEN: subtract the **seven-day look-back window** when computing the resume point. Re-reading a week is harmless because every write is an upsert (FR-023), and it is the price of not missing a late upload
- [X] T095 [US3] GREEN: recompute and persist the resume point at the end of each sync as `min(latest stored activity start, earliest activity with an outstanding series) − 7 days`, per [data-model.md](./data-model.md). On a first sync, with no activities at all, it is the Unix epoch
- [X] T096 [US3] RED: assert FR-017e and FR-029 — with A10's series outstanding, the resume point sits **behind** A10, so the next sync re-reads it and retries the streams request. Confirm it fails because only the latest stored activity is considered. This is why no separate queue of outstanding work exists (research R18) — file: `tests/TrainingLoadAnalyzer.Infrastructure.Tests/IncrementalSyncTests.cs`
- [X] T097 [US3] GREEN: add the second term to the resume-point computation
- [X] T098 [P] [US3] CONFIRM — assert FR-017e's self-limiting property: advance the fixed clock past A10's 180-day window and confirm the resume point is **no longer** held back by it. A series that never arrives must degrade to estimated load, not wedge the sync forever — file: `tests/TrainingLoadAnalyzer.Infrastructure.Tests/IncrementalSyncTests.cs`
- [X] T099 [P] [US3] CONFIRM: assert FR-029 — an activity read but not stored does not move the resume point past itself — file: `tests/TrainingLoadAnalyzer.Infrastructure.Tests/IncrementalSyncTests.cs`
- [X] T100 [P] [US3] CONFIRM: assert scenario US3.2, FR-033 and C60 — a sync finding nothing new returns `Completed` with zero counts. Not a failure, not an empty history, not an error — file: `tests/TrainingLoadAnalyzer.Infrastructure.Tests/IncrementalSyncTests.cs`
- [X] T101 [P] [US3] CONFIRM: assert scenario US3.4 — re-reading an activity already held leaves one session, matching what Strava reports now — file: `tests/TrainingLoadAnalyzer.Infrastructure.Tests/IncrementalSyncTests.cs`

### Reconciliation

- [X] T102 [US3] RED: assert scenario US3.6, FR-031 and FR-031a — a stored session for an activity dated three days ago that has since been deleted on Strava is **removed** by a completed routine sync, and the removal is reported by identifier. Confirm it fails because nothing reconciles — file: `tests/TrainingLoadAnalyzer.Infrastructure.Tests/ReconciliationTests.cs`
- [X] T103 [US3] GREEN: collect the set of activity ids seen across the span actually read, and after a completed walk delete stored rows whose start falls inside that span and whose id is not in the set. The range query is possible **only** because of the `StartedAtUtcTicks` column (research R5, R19). Add `RemovedActivity(string ExternalId, RemovalReason Reason)` and report it on `SyncResult` (FR-031d)
- [X] T104 [US3] RED — **the test the whole design rests on**: assert scenario US3.8, FR-031c and C61. With a stored session whose activity has been deleted, have the stub **drop the connection** partway through so the span is **not** read to completion, and assert **nothing is removed**. Confirm it fails because reconciliation runs against a partial set of seen ids. The failure is a dropped connection rather than a rate limit because 429 handling does not exist until T115, a phase later; the 429 variant of this assertion is T132. An activity missing from a truncated read is missing from the read, not from Strava; deleting an athlete's training on the strength of a dropped connection is the one failure this feature must never produce — file: `tests/TrainingLoadAnalyzer.Infrastructure.Tests/ReconciliationTests.cs`
- [X] T105 [US3] GREEN: set the "span read to completion" flag in **exactly one place**, after the last page, and make reconciliation unreachable without it. One place, because a second assignment is how this guarantee erodes
- [X] T106 [P] [US3] CONFIRM: assert scenario US3.7 and FR-031b — a session whose activity was deleted two months ago **survives** a routine sync, because a routine sync does not read that far back. This is a stated limit rather than a defect: Strava's listing is filtered by when an activity happened, not by when it changed — file: `tests/TrainingLoadAnalyzer.Infrastructure.Tests/ReconciliationTests.cs`
- [X] T107 [US3] RED: assert scenario US3.9 and FR-031e — a stored cycling session whose activity was corrected on Strava to `EBikeRide`, dated inside the look-back window, is **removed** by a completed sync. Confirm the expected behaviour falls out of T103 with no new code: the corrected activity is filtered by the sport-type table, so its id never enters the seen set, so reconciliation removes it. If it needs a rule of its own, the design drifted — file: `tests/TrainingLoadAnalyzer.Infrastructure.Tests/ReconciliationTests.cs`
- [X] T108 [US3] RED: assert scenario US3.5 and FR-032 — a full resynchronization reads the whole history regardless of the resume point and duplicates nothing. Confirm it fails because `FullResyncAsync` does not exist — file: `tests/TrainingLoadAnalyzer.Infrastructure.Tests/ReconciliationTests.cs`
- [X] T109 [US3] GREEN: add `FullResyncAsync` to `StravaActivitySync`, ignoring the resume point and reconciling over the whole history — subject to the same completeness guard, so a full resync that stopped early removes nothing either
- [X] T110 [P] [US3] CONFIRM: assert FR-017b against a full resync — a stored series survives one, even for an activity now outside the measured window. This is the path most likely to strip a series by accident — file: `tests/TrainingLoadAnalyzer.Infrastructure.Tests/ReconciliationTests.cs`
- [X] T111 [US3] VERIFY: run `dotnet test` — all green. Confirm `SyncOutcome` still has only the values a RED step has forced, and that reconciliation has exactly one call site guarded by exactly one flag

**Checkpoint**: User Stories 1–3 are complete. Routine syncs are cheap, late uploads are caught,
recent corrections and deletions are reconciled, and nothing is ever deleted on the strength of a read
that did not finish.

---

## Phase 6: User Story 4 - Survive Strava's limits and a dropped connection (Priority: P4)

**Goal**: a sync that cannot continue stops cleanly, keeps what it stored, says why and when it can be
retried, and the next one picks up where it left off.

**Independent Test**: make the stub refuse with a rate-limit response, and separately fail mid-page;
read the stored sessions, the outcome, and the resume point.

### Reading the budget

- [X] T112 [US4] RED: assert FR-034 — the client parses `X-ReadRateLimit-Limit: 100,1000` and `X-ReadRateLimit-Usage: 98,412` into a fifteen-minute and a daily figure. Confirm it fails because `RateLimitStatus` does not exist. Match header names **case-insensitively**: Strava's own documentation is inconsistent about their casing — file: `tests/TrainingLoadAnalyzer.Infrastructure.Tests/RateLimitTests.cs`
- [X] T113 [US4] GREEN: create `src/TrainingLoadAnalyzer.Infrastructure/Strava/RateLimitStatus.cs` and parse both headers on every response. Use the **read** headers, not `X-RateLimit-*`: the read limit of 100 per fifteen minutes and 1,000 per day is the binding one for an importer, and the overall 200/2,000 is looser (research R16)
- [X] T114 [US4] RED: assert scenario US4.1, FR-035 and C62 — a stub reporting the read limit reached stops the sync, keeps every session already stored, and returns `RateLimited` with `RetryAfter` set. Confirm it fails because the walk continues — file: `tests/TrainingLoadAnalyzer.Infrastructure.Tests/RateLimitTests.cs`
- [X] T115 [US4] GREEN: add `SyncOutcome.RateLimited`, stop the walk before the limit is exceeded rather than after, and set `RetryAfter`
- [X] T116 [US4] RED: assert C62's second half — with the clock at `10:07:33Z`, `RetryAfter` is **`10:15:00Z`**, not `10:22:33Z`. Confirm it fails against a fixed fifteen-minute delay. Strava's windows reset at natural quarter-hours — :00, :15, :30, :45 — so a fixed delay waits roughly twice as long as it needs to (research R16) — file: `tests/TrainingLoadAnalyzer.Infrastructure.Tests/RateLimitTests.cs`
- [X] T117 [US4] GREEN: compute `RetryAfter` as the next quarter-hour boundary, and the daily reset as the next midnight UTC
- [X] T118 [US4] RED: assert scenario US4.2 and FR-036 — a sync run after one that stopped at the limit resumes from the stored resume point and does not re-read the pages already stored. Confirm it fails because the resume point was not advanced over the completed pages — file: `tests/TrainingLoadAnalyzer.Infrastructure.Tests/RateLimitTests.cs`
- [X] T119 [US4] GREEN: persist the resume point over durably-stored work before returning `RateLimited` (FR-029, C64)

### Failing, and failing safely

- [X] T120 [US4] RED: assert scenario US4.3, FR-026 and FR-037 — a connection failure partway through a page stores **no partially mapped session**, keeps the sessions from completed pages, and returns `Interrupted`. Confirm it fails because a half-written page is committed — file: `tests/TrainingLoadAnalyzer.Infrastructure.Tests/RateLimitTests.cs`
- [X] T121 [US4] GREEN: store each page in one transaction, and add `SyncOutcome.Interrupted`
- [X] T122 [US4] GREEN: add a bounded retry — three attempts with exponential backoff — for connection failures and 5xx responses only, in `StravaApiClient`. Hand-written, about fifteen lines; no resilience package (research R17, Principle III)
- [X] T123 [US4] RED — **discriminating check** for C68: assert that a 429 is **never retried**. Confirm it fails against a retry policy that treats any failed response as transient. Strava's documentation is explicit that requests violating the short-term limit still count toward the daily one, so retrying into a limit burns a budget that resets only at midnight UTC. A 429 is a stop signal, not a failure — file: `tests/TrainingLoadAnalyzer.Infrastructure.Tests/RateLimitTests.cs`
- [X] T124 [US4] CONFIRM: assert FR-037's other exclusions — a rejected credential and a 4xx are not retried either. Only a dropped connection and a 5xx are plausibly transient — file: `tests/TrainingLoadAnalyzer.Infrastructure.Tests/RateLimitTests.cs`
- [X] T125 [US4] CONFIRM: assert scenario US4.4 — after an interruption, the next sync re-reads the incomplete page and stores each activity **exactly once** — file: `tests/TrainingLoadAnalyzer.Infrastructure.Tests/RateLimitTests.cs`

### Refusing, and reporting

- [X] T126 [US4] RED: assert scenario US4.6, FR-040 and C63 — a second sync started while one is running returns `Refused` rather than blocking or running alongside. Confirm it fails because both run — file: `tests/TrainingLoadAnalyzer.Infrastructure.Tests/RateLimitTests.cs`
- [X] T127 [US4] GREEN: add `SyncOutcome.Refused` behind a `SemaphoreSlim` held for the sync's duration, refusing rather than waiting. In-process is sufficient: the spec's Assumptions establish a single-process store, so a database flag would guard a case that cannot occur and would need its own crash-recovery story (research R22)
- [X] T128 [US4] RED: assert `ReconnectionRequired` as a sync outcome — a revoked credential encountered mid-sync stops the sync, keeps what was stored, and surfaces as `SyncOutcome.ReconnectionRequired` rather than as an escaping exception (FR-006, spec's edge case) — file: `tests/TrainingLoadAnalyzer.Infrastructure.Tests/RateLimitTests.cs`
- [X] T129 [US4] GREEN: add `SyncOutcome.ReconnectionRequired` and catch `ReconnectionRequiredException` at the sync boundary
- [X] T130 [US4] CONFIRM: assert scenario US4.5, FR-038 and FR-039 — every sync reports imported, updated, removed, skipped-with-reasons, outstanding-series and discarded-sample counts, and whether it finished or stopped early and why. A sync that stopped early is distinguishable from one that completed, both in the result and in the stored `SyncState` — file: `tests/TrainingLoadAnalyzer.Infrastructure.Tests/RateLimitTests.cs`
- [X] T131 [P] [US4] CONFIRM: assert C66 — no field or message on `SyncResult`, in any outcome, contains a token — file: `tests/TrainingLoadAnalyzer.Infrastructure.Tests/RateLimitTests.cs`
- [X] T132 [P] [US4] CONFIRM — **discriminating check** for C61 across every outcome: for each of `RateLimited`, `Interrupted`, `ReconnectionRequired` and `Refused`, assert `Removed` is empty. T104 pins the rate-limit case; this pins the other three, which are the ones a later refactor is most likely to miss — file: `tests/TrainingLoadAnalyzer.Infrastructure.Tests/ReconciliationTests.cs`
- [X] T133 [US4] VERIFY: run `dotnet test` — all green. Confirm `SyncOutcome` now has exactly the five values in [data-model.md](./data-model.md) and no sixth

**Checkpoint**: all four user stories are complete. The first import of a real multi-year history can
stop at the rate limit and finish on a later run, and no failure mode costs the athlete training they
actually did.

---

## Phase 7: Polish, Precision, and Compliance Review

**Purpose**: the checks that only make sense once everything is green. These are the commands in
[quickstart.md](./quickstart.md) under "Checking the constitution held", run as tasks.

- [X] T134 [P] Principle II / FR-018 / C69 — `grep -E 'PackageReference|ProjectReference' src/TrainingLoadAnalyzer.Domain/*.csproj` produces no output, and the domain's tree hash matches the one recorded in T002
- [X] T135 [P] Principle II / C69 — `grep -ril -E 'strava|oauth|sport_type|access_token' src/TrainingLoadAnalyzer.Domain/` produces no output
- [X] T136 [P] FR-041 — `git diff --stat main -- src/TrainingLoadAnalyzer.Domain tests/TrainingLoadAnalyzer.Domain.Tests` is empty, and the domain suite still reports **204 passing**
- [X] T137 [P] Principle III / research R8, R9, R17 — `grep -rn -E 'InMemory|Moq|NSubstitute|FakeItEasy|Polly|WireMock' src/TrainingLoadAnalyzer.Infrastructure tests/TrainingLoadAnalyzer.Infrastructure.Tests` produces no output. The risk is not a package added deliberately but one added in passing, to solve a problem research already solved without it
- [X] T138 [P] FR-005 / C51 / C66 — review every log, message and `ToString` reachable from Infrastructure for a credential
- [X] T139 [P] FR-042 / SC-011 — confirm the whole suite runs with no network and no database file: `grep -rn 'DataSource=[^:]' tests/TrainingLoadAnalyzer.Infrastructure.Tests/` produces no output, and the suite passes with networking unavailable
- [X] T140 [P] FR-043 — confirm no fixture contains real personal data: identifiers are invented, and nothing was pasted from a real account
- [X] T141 FR-017f / C71 — `git diff main -- src/TrainingLoadAnalyzer.Domain/HeartRateSeries.cs` is empty. Sample filtering happens in the mapper; the domain's invariants must not have been relaxed to accommodate a provider
- [X] T142 [P] C59 — assert by reflection that `StravaActivitySync` exposes exactly two public methods, `SyncAsync` and `FullResyncAsync`: no per-activity import method, no streams-only method, and no reconciliation entry point of its own. Every test in Phases 4–6 passes just as well against a class that has grown a convenience method; only this one pins the surface — file: `tests/TrainingLoadAnalyzer.Infrastructure.Tests/ReconciliationTests.cs`
- [X] T143 REFACTOR: review the three folders for the boundary they exist to make visible — `Strava/` referencing nothing from `Persistence/`, `Persistence/` referencing nothing from `Strava/`, and `Sync/` the only place that knows both
- [X] T144 Principle VI — review every refusal and failure path for a message that names its rule. Nothing silently swallowed, nothing reduced to a generic failure
- [X] T145 Constitution compliance review against all seven principles, recording the result in the feature's completion notes. Principles I, II and III are named in the constitution as most at risk of erosion; **Principle I is this feature's residual risk**, per the Constitution Check in [plan.md](./plan.md), so record honestly whether every production member arrived via a failing test
- [X] T146 VERIFY: `dotnet build -warnaserror` clean and `dotnet test` fully green — features 001–005 together

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)** — blocks everything. Nothing compiles until the projects exist
- **Phase 2 (Foundational)** — blocks everything. No test can be written without the doubles
- **Phase 3 (US1)** — creates `ImportDbContext` and the first migration, which US2, US3 and US4 all build on
- **Phase 4 (US2)** — creates `ActivityRow`, the mapper, the client's paged walk, and `StravaActivitySync`. US3 and US4 both extend these
- **Phase 5 (US3)** — creates `SyncState` and reconciliation. US4 extends the sync's outcomes
- **Phase 6 (US4)** — adds four `SyncOutcome` values and the retry policy
- **Phase 7 (Polish)** — after everything

### User Story Dependencies

The stories are **not** fully independent, and it is more honest to say so than to claim otherwise:

- **US1** is genuinely independent. It stores a connection and never touches an activity
- **US2 depends on US1** for `ImportDbContext` and for a connection to authorize against
- **US3 depends on US2** — there is nothing to be incremental against until a history has been imported
- **US4 depends on US2** for a walk to interrupt, and touches US3's resume point and reconciliation

This is the same honest ordering feature 004 recorded: each story is independently *testable* once the
ones before it are in place, and each is a deliverable increment, but they are not interchangeable.

### Within Each Story

RED before GREEN, always. CONFIRM tasks may be written at any point after the behaviour they pin
exists. VERIFY closes each phase.

### Parallel Opportunities

- **T008, T009, T010** — the three test doubles, different files, no dependency between them
- **T013, T025, T030, T036, T037** — US1's CONFIRM tasks, once the behaviour each pins exists
- **T046, T049, T052, T054** — persistence CONFIRMs, all in `PersistenceRoundTripTests.cs`, independent assertions
- **T059, T060, T061, T066, T069, T078** — mapping CONFIRMs, independent of one another
- **T098, T099, T100, T101** — resume-point CONFIRMs
- **T134 – T140** — the compliance greps, all read-only and independent

Note that `[P]` here means "no dependency", not "edit the same file simultaneously". Several parallel
tasks land in one test file; run them in any order, but not concurrently in the same file.

---

## Implementation Strategy

### MVP scope

**User Story 1 alone is not a useful MVP for this feature**, unlike in features 002–004. A connected
account that has imported nothing delivers no value to the athlete. The first genuinely useful
increment is **US1 + US2**: connect, and import the history. At that point every figure features
001–004 produce has real training behind it, which is the whole point of feature 005.

US3 and US4 are what make it usable more than once, and usable on a real multi-year history.

### Incremental delivery

1. **Phases 1–2** — scaffolding and doubles. Nothing works yet; nothing is claimed to
2. **Phase 3 (US1)** — an athlete can connect. Demonstrable, and the security-sensitive half of the
   feature is done and tested first
3. **Phase 4 (US2)** — the history imports. **This is the first shippable increment**, and the point
   at which the dashboard in Feature 6 would have something to show
4. **Phase 5 (US3)** — syncs become cheap and corrections propagate
5. **Phase 6 (US4)** — a real multi-year first import completes across more than one run
6. **Phase 7** — compliance review

### A note on the first real import

Do not be alarmed when the first import against a real account stops partway through. Roughly 140
requests against a ceiling of 100 per fifteen minutes means it **will** stop once and resume
(research R24). That is Phase 6 working, on the first run rather than in an edge case nobody sees —
which is also why Phase 6 is not optional polish.

---

## Notes

- **146 tasks**, against 58 functional requirements, 14 success criteria, 26 new contract guarantees
  (C46–C71), and 4 user stories
- Phase 1 is the **only** non-TDD phase, and it creates no production type. Every task from T008
  onward starts RED — the mitigation the Constitution Check in [plan.md](./plan.md) records for this
  feature's named Principle I risk
- Eight tasks are marked **discriminating checks** — T036, T049, T059, T081, T104, T123, T132, T142. Each
  fails against a plausible wrong implementation that every other test in its phase would accept.
  T104 is the most important test in the feature: it is what stops a dropped connection deleting an
  athlete's training
- The three answers from planning are load-bearing and easy to undo by accident. T028–T030 pin
  FR-002a, T074–T077 pin FR-017f and FR-017g, and T141 pins that neither was achieved by changing the
  domain
