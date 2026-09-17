# Phase 0 Research: Strava Import

**Feature**: 005-strava-import | **Date**: 2026-09-17

Decisions taken before any code is written, with the reasoning and the alternatives that were
rejected. Where a decision has a condition under which it should be revisited, that trigger is stated
so the next feature does not have to re-derive it.

This feature differs from 001–004 in kind: it is the first to leave the domain, the first to add a
project, the first to add a package, and the first whose correctness depends on a third party's
behaviour. Two research agents were dispatched — one over Strava's published API, one building and
running throwaway EF Core probes against copies of this repository's domain types. Findings marked
**verified** below were observed by running them, not reasoned about. Findings about Strava's API are
marked with the confidence the documentation supports, because a surprising amount of what everyone
"knows" about that API is not in its reference at all.

**Three items were put to the developer rather than settled here — R2, R14 and R21 — and all three
were answered on 2026-09-17.** Each is marked ✅ RESOLVED below with the answer and what it changed.
R14 and R21 amended the specification before tasks were generated.

---

## R1: Which projects this feature creates

**Decision**: create `src/TrainingLoadAnalyzer.Infrastructure/` and
`tests/TrainingLoadAnalyzer.Infrastructure.Tests/`. Create no other project. Leave
`TrainingLoadAnalyzer.Domain` and its test project untouched, including their `.csproj` files.

**Rationale**: Constitution Principle V is not a judgement call — it requires Strava OAuth, token
handling, API access, pagination and rate-limit handling to be isolated in an infrastructure layer and
tested at its own boundary, separately from domain logic. The project plan's section 22.3 records the
same conclusion and names this feature as the moment it becomes real. Entity Framework Core sits on
the same side of that line: it is the reason the domain must not acquire a package reference.

**`TrainingLoadAnalyzer.Application` is still not created.** Section 22.2 names this feature as the
revisit point, so it was revisited. Its three conditions are: the same use case running from two
hosts; both Blazor components and API endpoints calling the same orchestration; or wanting the
compiler to keep orchestration away from host types. **None has moved**, for the simple reason that
this feature introduces no host at all — there is no web project, no API project, and no UI. A use
case with exactly one caller, which does not yet exist, does not need its own assembly.

**Revisit trigger**: Feature 6 (Dashboard). If the Blazor render-mode decision in section 22.1 lands
on WebAssembly or Auto, a contracts project becomes necessary and the Application question should be
asked again at the same time. If it lands on Interactive Server, as section 22.1's default expects,
the dashboard injects the sync and query services straight from DI and the answer stays no.

**Alternatives rejected**: putting the Strava client into the Domain project (violates Principle V and
Principle II outright); creating Application and Infrastructure together now (Principle III — an empty
layer added because a diagram in the project plan shows one); a single `Infrastructure` project with
no separate test project (Principle V requires the boundary to be *tested at its own boundary*, and
mixing 204 fast domain tests with tests that open databases would slow the loop everyone runs).

---

## R2: ✅ RESOLVED — whether the domain gains an `IActivitySource` port

**The question**: Constitution Principle II says external activity data "MUST cross into the domain
only through an explicit abstraction (e.g. `IActivitySource`) owned by the domain/application layer
and implemented by infrastructure." Taken literally, feature 005 must add an interface to the domain
project. Principle III says an abstraction with a single implementation and no proven need for
substitution "is treated as a defect and MUST be removed or justified in writing."

**Why it is genuinely open**: in this feature both sides of such an interface live in Infrastructure.
Strava's client implements it and the sync calls it; nothing in the domain ever holds one. An
interface declared in the domain, implemented in infrastructure, and consumed only in infrastructure
decouples nothing — it is the exact shape Principle III calls a defect. But Principle II names the
type, and Governance says the constitution wins over a plan.

**Recommendation**: **no port interface in the domain for this feature.** Principle II's rationale is
stated in the constitution itself — "enables independent testing of training-load logic with
fakes/in-memory data and protects the core learning objective from third-party API churn" — and that
is already achieved, completely, by the project boundary: the domain has zero package references and
zero project references, and features 001–004 are tested with 204 tests that have never seen an
activity source. The `(e.g.)` reads as illustrative, and the project plan's section 10 agrees: "The
exact interface should only be decided based on tests and actual use cases."

**Revisit trigger**: the first time something outside Infrastructure needs activity data — Feature 6
reading stored sessions — an interface has a real consumer on the other side of a real boundary, and
should be introduced *then*, shaped by that caller.

**Answered 2026-09-17: no port.** The recommendation was accepted. No code changes as a result; the
revisit trigger above is what Feature 6 should act on. Recorded here rather than left implicit, because
a reviewer reading Principle II will reasonably ask why the interface it names does not exist.

---

## R3: Where the sync orchestration lives

**Decision**: in `TrainingLoadAnalyzer.Infrastructure`, under a `Sync/` folder, as ordinary classes.
Not in a separate assembly, not behind an interface, not registered through a DI container this
feature introduces.

**Rationale**: follows R1. Section 22.2's rule is that use cases live in a `Features/`-style folder
inside whichever project hosts them until an assembly earns its place; here that project is
Infrastructure, because the sync is the integration. A DI container is a host concern, and this
feature has no host — the classes take their collaborators as constructor parameters, which is all the
tests need and all Feature 6 will need to register.

**Alternatives rejected**: a `Features/` folder in a new Application project (R1); static classes as
features 002–004 used (the sync holds a client, a database context, a clock and a rate-limit budget —
it has state and collaborators, unlike a pure calculation).

---

## R4: Mapping the domain type directly versus an Infrastructure persistence entity

**Decision**: an Infrastructure-owned persistence entity, `ActivityRow`, with `From(...)` and
`ToDomain()` conversions. Do **not** map `TrainingActivity` directly with EF Core.

**Rationale — this one was decided by running it, not by taste.** Mapping `TrainingActivity` directly
fails at model build, verified:

```text
InvalidOperationException: No suitable constructor was found for the type 'TrainingActivity'.
    Cannot bind 'activityType' in 'TrainingActivity(string externalId, DateTimeOffset startedAt,
    TimeSpan movingTime, ActivityType activityType, HeartRateSeries heartRate)'
```

EF Core binds constructor parameters to properties **by name**, and this constructor's fourth
parameter is `activityType` while the property is `Type`. There is no Fluent API to correct it —
EF Core's own documentation says configuration of specific constructors "is planned for a future
release". The only fixes are to rename a parameter in a signed-off domain type *because a database
library wants it*, or to bind the parameter to a shadow property, which was verified to work and makes
`Type` invisible to LINQ.

That is the headline, but the full comparison is what settles it. Direct mapping additionally needs:
an explicit `.Property()` for all five get-only properties, because EF Core does not map properties
without setters by convention; a `ValueConverter` **and** a `ValueComparer` for the heart-rate series;
two shadow properties for the provider and the outstanding-series flag, written through untyped
`ChangeTracker` calls at every insert; and a composite key built from one real and one shadow
property. Against that, `ActivityRow` is about twenty-five lines with `init` properties plus a
seven-line `IEntityTypeConfiguration`.

There is a second, quieter reason. `TrainingActivity`'s constructor validates, and under direct
mapping that validation runs *inside EF Core's materializer* — a corrupt or hand-edited row throws
from deep inside a query instead of returning a row. With `ToDomain()`, the constructor runs on the
sync's own call stack, where FR-011's "skip it and say why" is expressible per row. For an importer
whose whole job is to handle awkward data gracefully, that is worth having.

**Alternatives rejected**: direct mapping after renaming the domain parameter (lets persistence
dictate a domain signature — the precise inversion Principle II exists to prevent, and it would edit
feature 001); direct mapping with the shadow-property binding (verified working, but `Type` stops
translating to SQL and every insert becomes three statements).

---

## R5: Storing an instant with its offset, and a duration, on SQLite

**Decision**: `ActivityRow` stores `StartedAtUtcTicks` (`long`), `StartedAtOffsetMinutes` (`short`)
and `MovingTimeTicks` (`long`). It stores no `DateTimeOffset` and no `TimeSpan` column.

**Rationale**: SQLite has no date type. EF Core stores `DateTimeOffset` as text, and round-trip
fidelity is **not** the problem — it was verified exact across `+03:00` with one-tick precision,
`+05:45`, `-04:00` and `+00:00`. The problem is querying. Verified:

```text
OrderBy(x => x.StartedAt)      -> NotSupportedException: SQLite does not support expressions of
                                  type 'DateTimeOffset' in ORDER BY clauses
Where(x => x.StartedAt > cut)  -> InvalidOperationException: could not be translated
```

Equality translates, but as *string* equality — the same instant written with a different offset does
not match. EF Core's own SQLite limitations page recommends against `DateTimeOffset` for this reason.
This feature cannot live with that: FR-027 asks for every activity from a resume point onward, and
FR-031's reconciliation asks which stored sessions fall inside a span. Both are range queries over
start time, and both must run in SQL rather than by loading the whole table.

Storing ticks and an offset in minutes restores them. Verified: `Where(r => r.StartedAtUtcTicks >
cutoff).OrderBy(...)` translates to real SQL with an index on the column, and reconstruction is exact
for all four offsets above.

**Reconstruct defensively**: `new DateTimeOffset(new DateTime(utcTicks, DateTimeKind.Utc))
.ToOffset(TimeSpan.FromMinutes(offsetMinutes))`, not `new DateTimeOffset(utcTicks + offset.Ticks,
offset)`. The second form overflows at the extremes of the range.

**This is a second thing `ActivityRow` buys that direct mapping cannot express** (R4): the domain type
has one `DateTimeOffset` property and no place to put two columns.

---

## R6: Storing the heart-rate series

**Decision**: one nullable text column, `HeartRateJson`, holding an array of `[ticks, bpm]` pairs.
Serialized in `From(...)` and rebuilt through `new HeartRateSeries(...)` in `ToDomain()`.

**Rationale**: the child-table option is not available — `HeartRateSample` is a `readonly record
struct`, and `OwnsMany` requires a reference type. Verified, at compile time:

```text
CS0452: The type 'HeartRateSample' must be a reference type in order to use it as parameter
        'TNewDependentEntity'
```

EF Core 10's complex types do not rescue it either: `ComplexCollection(...)`, with or without
`.ToJson()`, fails model build because complex and owned members cannot bind to constructor
parameters. A child table would therefore mean inventing a mutable per-sample class, giving thousands
of rows per activity for data that is only ever read whole.

JSON was verified exact, including sub-tick sample offsets and a genuinely `NULL` column for an
activity with no series. On `ActivityRow` it needs no `ValueConverter` and no `ValueComparer` at all —
it is a `string?` property that `From`/`ToDomain` fill.

Rebuilding through the real `HeartRateSeries` constructor on load is deliberate: it re-checks the
ascending-order and plausible-range invariants, so a corrupted column is caught at the boundary rather
than silently producing a wrong TRIMP figure.

**The column is opaque to SQL** — nothing can be indexed or queried inside it. That is acceptable here
because no requirement asks a question about an individual sample.

**Revisit trigger**: a feature wanting to query inside the series (time in zone as a stored column,
say) should add a derived column rather than restructure this one.

---

## R7: Creating the schema — migrations or `EnsureCreated`

**Decision**: EF Core migrations, with `Microsoft.EntityFrameworkCore.Design` as a private-asset
package reference and `dotnet-ef` as a **local** tool so the version is pinned in the repository.

**Rationale**: `EnsureCreated` is genuinely defensible for one user with one local file, and it is less
machinery today. It is rejected because of what it costs later, which is documented and specific: it
writes no migrations-history table, and if any table already exists it will not initialise the schema
— so the first time a column is added, existing databases keep the old schema silently and queries
fail at runtime on the missing column. EF Core's documentation states that transitioning from
`EnsureCreated` to migrations is not seamless and that "the simplest way to do it is to drop the
database and re-create it".

For this database, "drop and re-create" is not cheap. It holds **imported** data behind a limit of
1,000 read requests per day (R16), so re-importing a multi-year history costs days, not minutes. And
the schema will change: Feature 6 will want something, a second provider would want a column, and R6
already records a likely index. Paying one package reference and one generated file per change is the
cheaper side of that trade.

**Alternatives rejected**: `EnsureCreated` with a written-down acceptance of "delete the `.db` and
re-import on any schema change" (legitimate, and it was weighed — the rate limit is what defeats it);
hand-written SQL DDL (all of migrations' cost, none of its tooling).

---

## R8: Testing the database boundary

**Decision**: real SQLite, in memory, via a `SqliteConnection("DataSource=:memory:")` that is opened
and **kept open** for the lifetime of the test class, with the connection *object* — not a connection
string — passed to `UseSqlite`. Do not add `Microsoft.EntityFrameworkCore.InMemory`.

**Rationale**: verified, passing the connection string instead makes EF Core open and close a
connection per operation, so `EnsureCreated` builds the schema and the database then evaporates:
`SQLite Error 1: 'no such table: Activities'`. Closing and reopening the same connection object
destroys the data too. Two `:memory:` connections are two separate databases, which is exactly the
isolation wanted — one connection per test class, safe to run in parallel.

The EF Core InMemory provider was compared side by side and is the wrong tool here, verified: under
it, both `OrderBy(a => a.StartedAt)` and `Where(a => a.StartedAt > x)` **succeed**, hiding the two
real SQLite failures from R5. A suite built on it would green-light queries that throw in production.

EF Core's documentation warns against SQLite as a *fake for a different production database*. That
warning does not apply here: SQLite **is** production for this project. What `:memory:` does not cover
is file creation, journal modes, concurrent writers, and drift between the migrated schema and an
existing file — none of which this feature's requirements turn on.

**Assert twice**: on the reconstructed domain object (`EqualsExact` for the instant, `SequenceEqual`
for the samples) *and* on the raw column values through a plain `SqliteCommand` on the same
connection. The second assertion is what catches a silent mapping regression, and it is impossible
with the InMemory provider. FR-024 is a strong enough requirement to deserve both.

---

## R9: Testing the HTTP boundary

**Decision**: a hand-written `HttpMessageHandler` stub in the test project, returning canned responses
per request. No mocking library, no WireMock, no recorded-cassette library.

**Rationale**: Principle IV permits a mocking library only when a genuine need exists. The need here
is to control an external boundary, which is real — but a subclass of `HttpMessageHandler` with a
queue of responses is about thirty lines and gives exact control over status codes, headers (which
matters enormously for R16's rate-limit headers) and bodies. A mocking library would add a dependency
to express the same thing less directly.

This is what makes FR-042 and SC-011 achievable: authorization, renewal, paging, mapping, storage,
resumption and limit handling all become testable with no network and no Strava account.

**Fixtures are purpose-built and anonymized** (FR-043). Strava MCP may be used while writing them to
learn what real payloads look like; the payloads themselves do not enter the repository.

---

## R10: The clock

**Decision**: `TimeProvider`, taken as a constructor parameter, with `TimeProvider.System` in
production and a fake in tests.

**Rationale**: three requirements depend on "now" and cannot be tested without controlling it —
FR-017a's 180-day measured window, FR-004's token-expiry check, and FR-035's "earliest time the sync
can usefully be retried". `TimeProvider` is a base class library type on .NET 8 and later, so this
costs no package, and it is the platform's own answer rather than an abstraction this project invents.

Note the contrast with features 001–004, which were forbidden from reading the clock at all. That
prohibition was a property of pure calculation. A sync is inherently a thing that happens at a time;
the discipline here is that the time is an *input*, not an ambient read.

---

## R11: What of OAuth this feature builds, and what it defers

**Decision**: this feature implements everything from the authorization code onward — exchanging the
code for tokens, storing them, renewing them, detecting rejection, and revoking on disconnect. It does
**not** build the browser redirect or the listener that catches it.

**Rationale**: the specification's Assumptions already draw this line — "The consent redirect has to
land somewhere ... but what those look like belongs to Feature 6" — and User Story 1's first
acceptance scenario is worded to match: the analyzer "exchanges the returned authorization grant". A
loopback HTTP listener would be a host, and this feature has none (R1).

**What this costs**: validating against real Strava during this feature means pasting an authorization
code obtained by visiting the authorize URL in a browser. [quickstart.md](./quickstart.md) documents
that as a manual step. It is a fair trade for not building a throwaway host.

**Confirmed details** for the parts that are built: authorize at `https://www.strava.com/oauth/authorize`
with `client_id`, `redirect_uri`, `response_type=code`, `scope`, optional `approval_prompt` and
`state`; `localhost` and `127.0.0.1` are white-listed as redirect targets, which is what makes
Feature 6's listener possible. Token exchange and refresh both `POST` to
`https://www.strava.com/oauth/token`. Denial returns `error=access_denied` rather than a code.

**One documentation inconsistency to be aware of**: Strava's own authentication page gives the token
endpoint as `https://www.strava.com/oauth/token` in prose but uses `https://www.strava.com/api/v3/oauth/token`
in both of its cURL examples, and never reconciles them. Use the documented prose form; if it fails,
the other is the fallback and that discovery belongs in a comment, not in a silent switch.

---

## R12: Where tokens and application credentials live

**Decision**: the athlete's tokens live in the SQLite database beside the activities, in a single-row
connection table. The application's own `client_id` and `client_secret` are **configuration**, read
from environment variables, and never stored in the database and never committed.

**Rationale**: FR-003 requires the connection to survive a restart, which rules out memory. FR-005
forbids anything reaching source control, and the repository's `.gitignore` already anticipates this
with entries for `*.db`, `.env`, `.env.*` and `appsettings.*.local.json` — that groundwork was laid
before this feature started.

The split matters: the tokens belong to the athlete and the database is their data, while the client
secret belongs to the application registration and is the same whoever runs it. Putting the secret in
the athlete's database would mean a copied database file leaks the application's identity too.

**Not encrypted at rest.** The database sits on the athlete's own machine holding their own training
data; an attacker who can read it can read the activities anyway. This is recorded as a deliberate
decision rather than an oversight, and it is the one place where a reviewer might reasonably disagree.

**Revisit trigger**: the moment this runs anywhere but the athlete's own machine, or holds more than
one athlete, both of which the specification's Assumptions currently exclude.

---

## R13: Refresh-token rotation

**Decision**: persist the refresh token returned by **every** token response, replacing the stored one,
before the access token is used for anything.

**Rationale**: this is a correctness trap, not a nicety. Strava's documentation is explicit: "A refresh
token is issued back to the application after all successful requests ... The refresh token may or may
not be the same refresh token used to make the request. Applications should persist the refresh token
contained in the response, and always use the most recent refresh token ... Once a new refresh token is
returned, the older refresh token is invalidated immediately."

An implementation that keeps the original refresh token works in testing and then locks the athlete out
permanently the first time Strava rotates it — and the symptom is FR-006's reconnection-required
failure, which looks like the athlete revoked access. FR-004 already says "including a replacement
renewal credential when Strava issues one"; this records *why* that clause is not optional.

Also confirmed and useful: refreshing early is harmless. Strava returns the existing access token if it
has more than an hour left, and when it does issue a new one, "both the newer and older access tokens
can be used until they expire."

---

## R14: ✅ RESOLVED — when Strava grants less than was asked for

**The question**: FR-002 requires read access covering private activities, which is the
`activity:read_all` scope. The athlete can untick that on Strava's consent page and approve the rest.
The token response then reports what was actually granted — Strava's documentation says the granted
scope "may differ from the originally-requested list if the athlete unchecked any of the requested
scopes on the Authorization page" — and the specification says nothing about this case.

**Why it matters**: the failure is silent and it is the worst kind this feature can produce. With only
`activity:read`, private activities are simply filtered out of every response. The athlete's history
imports successfully, reports success, and under-reports their training — and every figure downstream,
fitness, fatigue, form and trend, is wrong in a way nothing in the analyzer can detect. It is exactly
the failure FR-006 was written to prevent in the revoked-access case, arriving through a different door.

**Recommendation**: **refuse the connection**, naming the scope that was withheld, on the same terms as
FR-006's reconnection-required outcome. The athlete reconnects and ticks the box; the cost of being
wrong in the other direction is a permanently and invisibly wrong training history.

**The alternative worth weighing**: accept the connection, record it as degraded, and state on every
sync summary that private activities are excluded. This respects an athlete who deliberately withheld
the scope. It is rejected as the default because the warning has to survive into Feature 6's dashboard
to do any good, and nothing yet guarantees that it will.

**Answered 2026-09-17: refuse.** The recommendation was accepted and written into the specification as
**FR-002a**, together with User Story 1 scenario 7, a new edge case, and a widened SC-009. The refusal
must be distinguishable from a rejected credential, and must tell the athlete what to approve when they
reconnect — a connection that cannot see private activities is not a degraded connection here, it is a
refused one.

---

## R15: Walking the activity list

**Decision**: request `per_page=200`; page with `page` and `after`; **do not depend on the order
results arrive in**; sort client-side by start time; deduplicate by activity id; treat every write as
an upsert.

**Rationale**: the widely-known behaviour here is not actually documented. The current API reference
says nothing about ordering for any activity endpoint. The claim that results are newest-first by
default and flip to oldest-first when `after` is supplied rests on a 2015 forum post by a Strava
engineer, echoed by client libraries, never restated in the modern documentation and never
contradicted in the changelog. That is good enough to *expect* but not to *depend on*, and the cost of
not depending on it is one sort.

`per_page=200` is in the same category: the documentation states only the default of 30 and no maximum.
The value 200 comes from Strava staff on Strava's own forum, and exceeding it returns HTTP 400. It is
reliable enough to use and worth a comment saying it is undocumented. It is also what makes the request
budget work — 1,200 activities is six requests at 200 per page, not forty at 30.

Offset paging is structurally unstable if activities arrive mid-walk: with newest-first order a new
activity shifts every later item down a slot, skipping one. Anchoring the walk with `after` makes the
prefix immutable, so new activities only ever append — which is the reason `after` is the standard
choice for backfills, independent of its ordering effect. FR-023's upsert-by-identity makes the
remaining overlap harmless.

**Also confirmed**: `before`/`after` are epoch seconds. The documentation does not say whether they are
inclusive or exclusive, nor whether they compare against `start_date` or `start_date_local`. The
look-back window of FR-028 makes this immaterial — seven days of slack absorbs a one-second boundary
question entirely.

---

## R16: Staying inside the rate limit

**Decision**: read both the limit and the usage headers on every response, stop **before** the read
limit is reached rather than after, and compute the retry time from the window boundary.

**Confirmed numbers**: the binding constraint for a read-only importer is the non-upload limit —
**100 requests per 15 minutes and 1,000 per day** — reported in `X-ReadRateLimit-Limit` and
`X-ReadRateLimit-Usage`, each two comma-separated integers, fifteen-minute value then daily. The
overall limit (200/2,000) is looser and not the one that binds. Header names must be matched
case-insensitively; Strava's own documentation is inconsistent about their casing.

**The window alignment is documented and it changes the retry logic**: "An application's 15-minute
limit is reset at natural 15-minute intervals corresponding to 0, 15, 30 and 45 minutes after the
hour. The daily limit resets at midnight UTC." So FR-035's "earliest time the sync can usefully be
retried" is the next quarter-hour boundary, not fifteen minutes from now — which on average halves the
wait.

**This is why FR-037's retry policy must not retry a 429**: "requests violating the short term limit
will still count toward the long term limit." Retrying into a limit burns the daily budget, which
resets only at midnight UTC. A 429 is a stop signal, not a transient failure.

**One planning fact worth knowing**: a newly registered Strava application has an athlete capacity of
one — "Single Player Mode" — which is exactly what this project needs and costs nothing to stay
within.

---

## R17: Retrying transient failures

**Decision**: a hand-written bounded retry — three attempts with exponential backoff — for connection
failures and 5xx responses only. No Polly, no resilience package.

**Rationale**: FR-037 requires a bounded retry for plausibly transient failures and forbids retrying
anything else. That is about fifteen lines. Polly would be a dependency carrying a policy engine to
express one policy, against Principle III.

**What must not be retried**, per FR-037 and R16: 429 (a limit, not a failure), 401 after a refresh has
already been attempted (a rejected credential — FR-006), and 4xx generally (a malformed request will
be malformed again). A 404 from the streams endpoint is a special case and is not a failure at all —
see R20.

---

## R18: The resume point

**Decision**: store a single instant. On each sync, request from
`min(latest successfully stored activity start, earliest activity with an outstanding series) − 7 days`.
On the very first sync, request from the epoch.

**Rationale**: FR-028 sets the seven-day look-back and FR-029 forbids advancing over anything not
durably stored. FR-017e adds the second term: an activity stored without the series it should have got
must not be left behind, and rather than keeping a separate queue of outstanding work, it simply holds
the resume point back so the next sync re-reads it. Re-reading is free of consequence because every
write is an upsert (FR-023).

**The self-limiting property matters**: an activity whose series never arrives holds the resume point
back only until it ages out of the 180-day measured window, after which it no longer qualifies under
FR-017 and stops counting. So a permanently failing series degrades to estimated load rather than
wedging the sync forever.

**Alternatives rejected**: storing the last page number (meaningless once the history shifts); storing
the highest activity id seen (Strava ids are not ordered by activity date — a backdated upload gets a
new high id, so the watermark would skip it); keeping an explicit outstanding-series work queue (more
state to keep consistent than the resume point already provides).

---

## R19: Reconciliation

**Decision**: within one sync, collect the set of activity ids seen across the span actually read;
afterwards, and only if the span was read to completion, delete stored rows whose start falls inside
that span and whose id is not in the set.

**Rationale**: this is FR-031 stated as an algorithm. The span is known — it is the resume point
onward — and the R5 decision to store `StartedAtUtcTicks` is what lets "stored rows inside that span"
be a SQL query rather than a full table scan in memory.

**The completeness guard is the whole safety story** (FR-031c). The set of seen ids is only meaningful
if every page was read; a sync stopped by a rate limit has a *partial* set, and deleting against it
would remove training the athlete actually did. The flag saying "this span was read to completion"
must therefore be set in exactly one place, after the last page, and reconciliation must be
unreachable without it.

**FR-031e falls out for free**: an activity whose sport type changed to one outside FR-010's table is
filtered out during mapping, so its id never enters the seen set, so reconciliation removes it — the
same path as a deletion, with no separate rule to implement.

---

## R20: Retrieving heart-rate streams

**Decision**: `GET /activities/{id}/streams` with `keys=time,heartrate` and `key_by_type=true`,
sending **no** `resolution` and no `series_type` parameter. Call it only when `has_heartrate` is true,
the activity is inside the measured window, and no series is already held.

**Rationale and confirmed details**: `keys` and `key_by_type` are the only parameters the current
reference documents, and `key_by_type` is documented as "Must be true." `resolution` and `series_type`
appear in the current reference only as *response* fields; as request parameters they survive only in
Strava's retired documentation, where the default was "all" — every point — and the named values
`low`/`medium`/`high` cap at 100/1,000/10,000 points. **There is no value meaning "full resolution";
omitting the parameter is how you get it.** That is how FR-017c is satisfied.

The response is an object keyed by stream type, each carrying a `data` array. `time` is integer
seconds and `heartrate` integer bpm, index-aligned. Two behaviours must be coded for, both confirmed by
practitioners rather than by the current reference:

- **A missing stream is an absent key, not a null.** Requesting `time,heartrate` on an activity
  without heart rate returns an object containing only `time`. Key-check; never index.
- **An activity with no streams at all returns HTTP 404**, notably manual activities. This is "no
  streams", not an error, and must not go down R17's retry path or FR-037's failure path. It lands on
  FR-017d — store the session, record the reason, carry on.

Filtering on `has_heartrate` before calling is not an optimisation, it is budget management: every
avoided call is one of 1,000 daily requests kept (R16).

**Sampling is not 1 Hz.** Strava's streams are irregularly spaced because of recording gaps and smart
recording, so `time` must actually be read rather than assumed uniform. This suits
`HeartRateSeries.TrimpPoints` exactly as feature 001 built it — it charges each gap to the sample
preceding it — but it means `time[i]` is load-bearing data, not an index.

---

## R21: ✅ RESOLVED — heart-rate samples the domain refuses

**The question**: `HeartRateSeries` enforces two invariants, set by feature 001 and covered by its
tests: sample times strictly ascending, and every sample between 20 and 250 bpm. A real Strava
heart-rate stream routinely violates the second. Zero values appear where the strap dropped out, and
most commonly in the opening seconds before it picks the signal up at all.

The specification currently forbids doing anything about it. Its Assumptions say the import "does not
second-guess a moving time, correct a sport type, or **repair a heart-rate series**". Under that rule,
a single zero sample anywhere in a three-thousand-sample ride makes the whole series unusable, sending
the activity down FR-017d to estimated load.

**Why this needs deciding rather than coding**: it would quietly undo the Q1 decision. The measured
window was chosen so that recent training carries measured load; if most real rides fall back to
estimated anyway, the feature spends a request per activity to achieve nothing. But the alternative —
dropping samples — changes the TRIMP figure, and `TrimpPoints` charges each gap to the sample before
it, so dropping a dropout lets the previous sample's zone weight cover the gap. That is a defensible
reading of a dropout and it is still, unmistakably, repair.

**Recommendation**: **drop samples outside the plausible range, keep the rest, and treat the series as
unusable only if fewer than two samples survive** — two being the minimum that scores anything at all,
so no arbitrary threshold is invented. Record the number dropped on the sync summary so it is visible
rather than silent. Then narrow the Assumption so "does not repair" continues to cover moving time and
sport type, and state the heart-rate rule explicitly instead.

**Alternatives worth weighing**: leave the Assumption as written and accept estimated load for any
activity with a single dropout (faithful to what is written, probably useless in practice); drop only
*leading and trailing* implausible samples and refuse a series with a dropout in the middle (models the
strap-warm-up case precisely and is more conservative, at the cost of a more complicated rule and of
still losing mid-ride dropouts, which are common on cold days).

**What is not on the table**: changing `HeartRateSeries`. Feature 001 is signed off, its invariants are
tested, and loosening them to accommodate one provider would push Strava's data shape into the domain
— Principle II, directly.

**Answered 2026-09-17: drop and count.** The recommendation was accepted and written into the
specification as **FR-017f** and **FR-017g**, with two acceptance scenarios, a new edge case, and a
rewritten Assumption that now scopes "does not repair" to moving time and sport type and states the
heart-rate rule explicitly. The count of discarded samples joins `SyncResult`, because FR-017g makes
the repair reportable rather than silent. Note what this does **not** change: the domain's invariants
are untouched, and the discarding happens in Infrastructure before `HeartRateSeries` is ever
constructed — so the domain still refuses exactly what it refused in feature 001.

---

## R22: The concurrency guard

**Decision**: an in-process lock — a `SemaphoreSlim` held for the duration of a sync, refusing rather
than waiting.

**Rationale**: FR-040 forbids two concurrent syncs for the same connection. The specification's
Assumptions establish that the store is local and single-process, so a database-held "running" flag
would guard against a case that cannot occur, and would additionally need a crash-recovery story for
the flag itself. Refusing rather than queueing is what FR-040 asks for.

**Revisit trigger**: anything that makes a second process plausible — a background scheduler, a second
host — at which point the flag and its recovery become real work.

---

## R23: What this feature deliberately does not build

Recorded so that a reviewer can tell a decision from an omission, and so `/speckit-tasks` does not
generate work for any of it:

- **No webhook subscription.** Strava's own rate-limit guidance recommends webhooks over polling, and
  they would catch every edit and deletion as it happens, answering R19 completely. They need a
  publicly reachable callback and a subscription lifecycle. Out of scope by the specification's
  Assumptions, and squarely the Strava time-sink the project plan's risk section warns about.
- **No background scheduler.** Syncs are triggered; nothing schedules them.
- **No DI container registration.** Feature 6 brings a host and will do this.
- **No `IActivitySource`**, pending R2.
- **No second provider, and no provider-neutral abstraction over Strava.** The `Provider` column exists
  because identity requires it (FR-023), not as a seam for Garmin.
- **No retry/resilience package, no mocking library, no assertion library.** R9, R17.
- **No encryption at rest.** R12.
- **No load calculation anywhere in this feature.** FR-021 — the import makes sessions available and
  nothing more.

---

## R24: Performance and scale

No performance requirement is stated or implied, and the binding constraint is not this code — it is
Strava's 1,000 daily read requests (R16).

A four-year history of roughly 1,200 activities is **6 list requests** at 200 per page. The measured
window adds one request per heart-rate-carrying activity inside 180 days — for an athlete training
five times a week, roughly 130 activities, so **around 130 stream requests**. A first import therefore
costs on the order of 140 requests: inside the daily allowance, but above the 100-per-fifteen-minutes
limit, so **a first import will stop once and resume**. That is by design (FR-035, FR-036) and it means
the resumption path is exercised on the very first run rather than being an edge case nobody sees.

A routine sync costs one list request, plus one stream request per new heart-rate activity — typically
two or three.

Database volumes are trivial: 1,200 rows, with the heart-rate JSON dominating storage at roughly 20–40 KB
per hour-long activity at one sample per second.

---

## Resolved and unresolved

| # | Decision | Status |
|---|----------|--------|
| R1 | Infrastructure + its test project; no Application project | ✅ Decided |
| R2 | Whether the domain gains an `IActivitySource` port | ✅ Resolved — no port |
| R3 | Sync orchestration lives in Infrastructure under `Sync/` | ✅ Decided |
| R4 | `ActivityRow` persistence entity, not direct mapping | ✅ Decided (verified) |
| R5 | Ticks + offset minutes, not `DateTimeOffset` columns | ✅ Decided (verified) |
| R6 | Heart-rate series as a JSON text column | ✅ Decided (verified) |
| R7 | Migrations, not `EnsureCreated` | ✅ Decided |
| R8 | In-memory SQLite, connection kept open; not the InMemory provider | ✅ Decided (verified) |
| R9 | Hand-written `HttpMessageHandler` stub | ✅ Decided |
| R10 | `TimeProvider` | ✅ Decided |
| R11 | Code exchange onward; redirect capture deferred to Feature 6 | ✅ Decided |
| R12 | Tokens in the database, client secret in the environment | ✅ Decided |
| R13 | Persist the rotated refresh token every time | ✅ Decided |
| R14 | Granted scope narrower than requested | ✅ Resolved — refuse (FR-002a) |
| R15 | `per_page=200`, `after`-anchored, order not depended on | ✅ Decided |
| R16 | Read limit 100/15min and 1,000/day; retry at the quarter-hour | ✅ Decided |
| R17 | Hand-written bounded retry; never retry a 429 | ✅ Decided |
| R18 | Resume point = earliest of two instants, minus seven days | ✅ Decided |
| R19 | Reconcile only over a span read to completion | ✅ Decided |
| R20 | Streams with no `resolution`; 404 means "no streams" | ✅ Decided |
| R21 | Heart-rate samples the domain refuses | ✅ Resolved — drop and count (FR-017f, FR-017g) |
| R22 | In-process lock, refusing rather than queueing | ✅ Decided |
| R23 | What is deliberately not built | ✅ Decided |
| R24 | Scale; a first import will stop once and resume | ✅ Decided |

**All 24 decisions are settled.** R2, R14 and R21 were answered on 2026-09-17; R14 and R21 amended the
specification before any task was generated, which is what Principle VII asks for — the ambiguity was
resolved in the specification rather than in an implementation decision nobody reviews. Nothing is
outstanding, and `/speckit-tasks` may run.

---

## Sources

**Strava** — developers.strava.com: [authentication](https://developers.strava.com/docs/authentication/),
[rate limits](https://developers.strava.com/docs/rate-limits/),
[API reference](https://developers.strava.com/docs/reference/) and the Swagger definitions it is
generated from (`swagger.json`, `activity.json`, `sport_type.json`, `activity_type.json`,
`stream.json`), [changelog](https://developers.strava.com/docs/changelog/). Undocumented behaviours —
list ordering, the `per_page` maximum of 200, the 404-on-no-streams response, and the
`resolution`/`series_type` request parameters — come from Strava staff posts on the official
strava-api Google Group and from Strava's retired `strava.github.io/api/v3` documentation. Each is
marked in place above; none is relied on for correctness.

**EF Core** — `dotnet/EntityFrameworkCore.Docs` on `main`: constructor binding, SQLite provider
limitations, testing strategy, `EnsureCreated` versus migrations. All EF Core findings above were
additionally **verified by building and running probe projects** against copies of this repository's
domain types on EF Core 10.0.0 and 10.0.12, SDK 10.0.100.

**Package versions** — `dotnet package search`: `Microsoft.EntityFrameworkCore.Sqlite` 10.0.12 is the
current 10.0.x release. 10.0.0 raises `NU1903` for a high-severity advisory in a transitive
`SQLitePCLRaw.lib.e_sqlite3` 2.1.11 dependency; 10.0.12 depends on 2.1.12 and restores clean.

**This repository** — the constitution, the project plan's sections 10, 18, 21, 22.2 and 22.3, and
features 001–004's specifications, plans and source.
