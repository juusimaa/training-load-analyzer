# Phase 0 Research: Dashboard

**Feature**: 006-dashboard | **Date**: 2026-09-18 | **Plan**: [plan.md](./plan.md)

Twenty-two decisions. Four were put to the developer and answered on 2026-09-18 (R4–R7); all four
amended [spec.md](./spec.md) before any task was generated. Seven were settled by **building and
running probes** rather than by reasoning (R8–R13, R16), and three of those came out against the
obvious answer.

Every entry records the decision, why, what else was weighed, and — where one exists — the trigger
that should reopen it.

---

## Decisions closing open questions from the project plan

### R1 — Blazor render mode: Interactive Server

**Decision**: Interactive Server, closing project plan section 22.1.

**Rationale**: 22.1's stated default was Interactive Server "if nothing forces otherwise", and nothing
does. The alternative — WebAssembly or Auto — requires an HTTP API and a shared contracts project
referenced from both ends, because components running in the browser cannot inject `ActivityStore`.
That is two extra projects and a wire format to agree, in service of a dashboard one person opens on
their own machine. FR-013 wants the page served from locally-stored data; a server-rendered component
reads that store directly.

**Alternatives considered**: Interactive WebAssembly (rejected: the API and contracts project are pure
cost here, and the README already records the refactoring path); Interactive Auto (rejected: it is
both of the above plus a second startup path); static SSR with no interactivity (rejected: FR-009's
sync button and FR-010's loading state need a round trip without a full page reload).

**Revisit trigger**: the README's own condition — scalability or a genuine separation-of-concerns need.
Neither can arise from a single-athlete MVP.

### R2 — `TrainingLoadAnalyzer.Application` is still not created

**Decision**: not created. Dashboard and sync orchestration live in a `Features/` folder inside
`TrainingLoadAnalyzer.Web`, exactly as project plan 22.2 prescribes.

**Rationale**: 22.2 names three conditions, and this feature moves none of them. (1) *The same use case
runs from two hosts* — there is one host. (2) *Blazor components and API endpoints call the same
orchestration* — the two minimal-API endpoints this feature adds (R6) call `StravaAuthorization`
directly and share nothing with the dashboard components. (3) *The compiler is wanted to keep
orchestration away from host types* — `DashboardViewBuilder` is a pure static function over domain
types and takes no host dependency to be kept away from.

Feature 005's R1 revisited this and declined because no host existed. A host now exists, and the answer
is still no, for different reasons — which is worth recording, because "the host has arrived" is the
argument a reviewer will expect to have carried.

**Revisit trigger**: unchanged — 22.2's three conditions. The most likely one to move is (2), if the
README's WebAssembly refactoring ever happens.

### R3 — No `IActivitySource` port in the domain

**Decision**: no port. Feature 005's R2 recorded a revisit trigger against this feature; it is
revisited here and declined.

**Rationale**: 005's R2 declined the port because both sides of it would have lived in Infrastructure,
so it would decouple nothing. The stated hope was that it would "arrive in Feature 6, shaped by its
first real consumer". The consumer has arrived and does not want one: the dashboard reads
`TrainingActivity` from `ActivityStore` and hands it to the domain's static calculators. Inserting an
interface between `TrainingLoadAnalyzer.Web` and `ActivityStore` would give it one implementation, one
caller, and no second provider in sight — the shape Principle III calls a defect.

Constitution Principle II's rationale is "independent testing of training-load logic with fakes" and
"protection from third-party API churn". Both already hold: the domain's 204 tests use in-memory
activities, and no Strava vocabulary crosses into `Domain` or into the dashboard's read model.

**Revisit trigger**: a second activity provider, or a dashboard test that cannot be written without
substituting the store. Neither exists (see R17 — the store is exercised for real).

---

## Decisions put to the developer

These four were raised rather than settled here, under Principle VII. All were answered on 2026-09-18
and all four changed [spec.md](./spec.md).

### R4 — The athlete's maximum heart rate comes from configuration

**The gap**: every measured training load is `CalculateTrainingLoad(int maximumHeartRate)`, and
**nothing in the system holds that number**. Feature 001 refused to put it on the activity (001 FR-015),
feature 002 refused to introduce an `AthleteProfile` for it (002 data-model, declined types), and
feature 005's Assumptions state explicitly that it "neither reads it from Strava nor stores it" because
load is computed on read. Every call site so far has been a test, where the author picks 190. The
dashboard is the first call site with a real athlete behind it, and the specification was silent.

**Decision**: a configuration value, `Athlete:MaximumHeartRate`, with startup refusing to run when it
is absent or not positive. Written into the spec as **FR-014** and **FR-015**.

**Rationale**: it matches how `StravaCredentials` already reaches the application (005 R12 — belongs to
whoever runs the analyzer, not in a database column), it adds no entity, no migration, no form and no
validation rules, and the MVP is explicitly single-athlete. Refusing to start is the deliberate part:
the failure mode this avoids is an application that starts against a default nobody chose and displays
confidently wrong TRIMP figures, which Principle VI calls a correctness-relevant failure that must not
be swallowed.

**Alternatives considered**: a stored athlete-profile row with a settings page (rejected: an entity, a
migration, a form and its validation, none of which the specification asks for — and 002 already
declined this type once); a hardcoded constant (rejected: invisible to the person it describes);
deriving `220 − age` (rejected: it invents a formula the specification does not state and still needs
the age from somewhere).

**Revisit trigger**: a second athlete, or a request to change the value without a restart.

### R5 — The weekly trend uses feature 004's thresholds, not a second rule

**The conflict**: 006's FR-005 said a change of "≥5%" is significant. Feature 004 FR-010 — built,
tested and shipped as `WeeklyLoadTrend.Classification` — requires **both** a relative change of ≥0.15
**and** an absolute floor of ≥50 points, and argues at length for the floor (004 FR-013, FR-014: a
large proportion of a small week is not a significant change). Two different rules would have been
computed over the same two weeks.

**Decision**: the dashboard displays `TrendClassification` as feature 004 computes it, and applies no
threshold of its own. Written into the spec as a rewritten **FR-005** plus **FR-005a**.

**Rationale**: the alternative is a threshold rule in the UI layer duplicating one in the domain, which
contradicts Principle II's direction of travel, Principle III, and the 004 specification's own
instruction that this feature "must not duplicate" what 002–004 produced. A dashboard that called a
week "increasing" while the domain called it "steady" would be the same defect Principle VI exists to
prevent, in display form.

**Alternatives considered**: amending 004's threshold down to 5% (rejected: reopens a signed-off feature
and discards a floor 004 argued for deliberately); showing both — a 5% arrow and 004's classification
(rejected: two notions of "trend" that disagree on the same week, shown side by side).

**Consequence worth naming** (see R14): because `Classification` is `Indeterminate` for any week the
range cuts short (004 FR-017), the *current* ISO week is indeterminate on every day but Sunday. That is
correct and it is also, on its own, useless to the athlete — which is what R14 resolves.

### R6 — The connect flow is in scope for this feature

**The gap**: US5 scenario 5 requires an error "directing them to reconnect", and FR-011 requires an
empty state with "guidance on next steps". **There was nowhere to direct anyone.** Feature 005 built
`StravaAuthorization` — `BuildAuthorizeUrl`, `ExchangeAsync`, `RefreshAsync`, `DisconnectAsync` — but no
host, so it can only be driven from a test. No US5 scenario could be run end to end, and
[quickstart.md](./quickstart.md) could not have been written honestly.

**Decision**: a minimal connect flow is in scope — a redirect to Strava's consent page and an OAuth
callback that completes the exchange. Written into the spec as **FR-016**, **FR-017**, **FR-018**.

**Rationale**: it adds no domain and no infrastructure code; it is two endpoints calling methods that
already exist and are already tested at their own boundary. Without it the feature is undemonstrable,
which makes its own acceptance criteria unverifiable.

**Alternatives considered**: a message with no destination (rejected: leaves the application unusable by
anyone who has not hand-seeded a database, and US5's happy path untestable outside tests); a dev-only
seeding command (rejected: same amount of work, less of the specification satisfied, and it would have
to be removed later anyway).

**Scope discipline**: the flow is *connect* and nothing else. No disconnect button, no account
management, no token-refresh scheduler. `DisconnectAsync` exists and stays uncalled.

### R7 — SC-005's ten seconds applies to an incremental sync

**The conflict**: SC-005 required a manual sync of "up to 2 years of activities" inside ten seconds.
Feature 005's own R24 established that ~1,200 activities cost roughly 140 Strava requests against an
allowance of 100 per fifteen minutes, so a first import **will** stop at the limit and resume — and
005's plan calls that "something the first run exercises rather than an edge case". The criterion was
unmeetable as written.

**Decision**: restated to bound a sync of an already-current history. A first import is explicitly
excluded and covered by FR-010's rate-limit messaging instead.

**Rationale**: the button's actual job is "make me current before I decide today's session". Ten seconds
is the right bound for that and is measurable. Keeping the old wording would have shipped a success
criterion known to fail.

---

## Decisions settled by probe

Probes were built under
`/private/tmp/.../scratchpad/probe` against a scaffolded Blazor Web App and the repository's real
`Domain` and `Infrastructure` projects. They are not part of the deliverable.

### R8 — The chart is hand-written inline SVG; no charting package

**Decision**: a `<svg>` with one `<polyline>` per series and a legend, rendered by a Blazor component.
No JavaScript, no JS interop, no charting library.

**Rationale**: US3 scenario 3 asks for a tooltip **or** "a simple legend showing what each line
represents" — the specification offers the cheaper option itself. Three polylines over at most 180
points is a `string.Join` over a projection; a charting package brings a JavaScript bundle, an interop
boundary, a render-mode interaction with prerendering, and a version to keep current, in exchange for
features the specification does not ask for. Constitution Technology Constraints require a documented
reason to introduce frontend technology, and there is none.

Being plain markup also makes it **testable by the same means as everything else**: a bUnit assertion
on the `points` attribute checks the geometry directly, which is what SC-003 ("no data points are
skipped or duplicated") actually asserts. A canvas-based library would have put the geometry out of
reach of any test in this repository.

**Alternatives considered**: Chart.js via interop, ApexCharts.Blazor, Plotly.Blazor (all rejected on the
above); a server-rendered PNG (rejected: an image library, and no legend semantics).

**Revisit trigger**: a specification requiring zoom, pan, or cross-series interaction.

### R9 — Every number inside SVG geometry is formatted with the invariant culture

**This is the finding the probes exist for.** The developer's machine is `fi-FI`. Rendering

```razor
<polyline points="@string.Join(" ", Values.Select(v => $"{v.X},{v.Y}"))" />
```

with the points `(0, 45.3)` and `(1.5, 12.25)` produced, verbatim:

```html
<polyline points="0,45,3 1,5,12,25" ...>
```

The decimal comma collides with SVG's coordinate separator, and two points silently became five
malformed ones. It throws nothing, logs nothing, and renders a wrong chart. It would have looked
perfect on an `en-US` CI machine and broken on the developer's own laptop.

**Decision**: all coordinate formatting uses `CultureInfo.InvariantCulture`, and a test asserts the
rendered `points` attribute against a machine-readable expectation. Displayed *metric* figures
(FR-001 – FR-004) are formatted against a fixed culture too, so SC-002's worked example "45.3" means
"45.3" on every machine — the same probe rendered `45,3` for a tile value. The spec's **Display
rounding** assumption was amended to say so.

**Alternatives considered**: setting the process culture at startup (rejected: it changes every figure
in the application to satisfy one attribute, and a future localisation would silently reintroduce the
bug); computing integer coordinates only (rejected: it constrains the chart's geometry to dodge a
formatting problem).

### R10 — Component tests use bUnit 2.11.3 on xUnit v3

**Decision**: `bunit` **2.11.3** in `TrainingLoadAnalyzer.Web.Tests`, alongside the same
`xunit.v3.mtp-v2` 4.0.1 the other two test projects use.

**Verified by probe**, because three things could have gone wrong and two did:

1. **It works.** bUnit 2.11.3 renders a `net10.0` Razor component under the Microsoft.Testing.Platform
   runner; three probe tests passed.
2. **`TestContext` is ambiguous.** bUnit's base class and xUnit v3's own `Xunit.TestContext` collide
   (`CS0104`). The base class to derive from is **`BunitContext`**, not the `TestContext` every bUnit
   1.x tutorial shows.
3. **The `xunit.runner.json` `Content` item must be dropped.** The test project needs
   `Microsoft.NET.Sdk.Razor` to compile `.razor` files, and that SDK already includes content items by
   default, so copying the existing projects' explicit `<Content Include="xunit.runner.json">` fails the
   build with `NETSDK1022`.

**Rationale for taking the dependency at all**: this is the first feature whose requirements are about
rendered output — an order (FR-007), a badge (FR-008), a rounding (SC-002), an empty state (FR-011), a
geometry (SC-003). Principle IV prefers designs testable through public behaviour, and R11 pushes as
much as possible out of the components for exactly that reason; but "the newest activity appears
first in the markup" has no non-rendering formulation. bUnit renders the real component in process,
with no browser and no mocking library, which is the least machinery that can assert it.

**Alternatives considered**: no component tests, asserting only the read model (rejected: FR-007,
FR-008 and SC-003 would be untested); Playwright or Selenium (rejected: a browser, a driver and a
running server to assert a `<span>`); `WebApplicationFactory` for everything (see R18 — it is used, but
it cannot reach an interactively-rendered component's state).

### R11 — The arithmetic lives in a pure read model, not in components

**Decision**: a static `DashboardViewBuilder.Build(activities, today, maximumHeartRate)` returning a
`DashboardView` record. Components render that record and do nothing else.

**Rationale**: it is Principle IV applied before reaching for a test tool. Every question with a right
answer — which days the chart covers, which week is current, which seven activities are recent, whether
there is enough history, what the trend classification is — is a pure function of the stored activities
and the date. Tested with plain xUnit at the same speed as the 204 domain tests. What is left for bUnit
is genuinely about markup, and R10's dependency then covers a small, honest surface instead of standing
in for a design.

It also keeps the read model free of Strava and of ASP.NET: `DashboardView` is built from
`TrainingActivity`, `DailyTrainingMetrics`, `WeeklyTrainingLoad` and `WeeklyLoadTrend` and references
nothing else.

### R12 — `AddDbContextFactory` alone, and *not* alongside `AddDbContext`

**Decision**: one registration — `builder.Services.AddDbContextFactory<ImportDbContext>(...)`. Dashboard
components create a context per read through the factory (`await using`). The scoped
`ImportDbContext` that `ActivityStore` and `StravaActivitySync` take by constructor is resolved inside
the per-sync scope R13 creates.

**Verified by probe**, and the result was not the expected one:

| Registration | `IDbContextFactory<T>` | scoped `ImportDbContext` | `StravaActivitySync` |
|---|---|---|---|
| `AddDbContextFactory` alone | ✅ | ✅ | ✅ |
| `AddDbContextFactory(..., ServiceLifetime.Scoped)` | ✅ | ✅ | ✅ |
| `AddDbContextFactory` **+** `AddDbContext` | ❌ | ❌ | ❌ `InvalidOperationException` |

`AddDbContextFactory` already registers a scoped `ImportDbContext`, so the single call serves both
needs — and adding `AddDbContext` as well, which is the obvious thing to reach for when a constructor
wants the context itself, **breaks the container at build time**: *"Cannot resolve scoped service
`IEnumerable<IDbContextOptionsConfiguration<ImportDbContext>>` from root provider."*

**Why the factory matters at all**: a Blazor Interactive Server circuit is one DI scope for the life of
the connection, which can be hours. A scoped `DbContext` injected into a component would be a
change-tracking cache that never resets and is shared by every concurrent render on that circuit.
Creating one per read through the factory is the documented pattern and gives each dashboard load its
own unit of work.

### R13 — Sync state lives in a singleton coordinator, because the circuit cannot hold it

**Decision**: a `SyncCoordinator` singleton in the Web project owning (a) the one-at-a-time guard and
(b) the last sync's outcome. It creates its own DI scope per run and calls the scoped
`StravaActivitySync`.

**Verified by probe.** `StravaActivitySync` guards concurrent syncs with an **instance** field —
`private readonly SemaphoreSlim running = new(1, 1)` — which is what returns `SyncOutcome.Refused`
under 005 FR-040. Feature 005 had no host, so one instance existed. Three registrations were tried:

| Registration | Result |
|---|---|
| `AddSingleton<StravaActivitySync>` over `AddDbContext` | ❌ *Cannot consume scoped service `ActivityStore` from singleton* |
| `AddSingleton<StravaActivitySync>` over `AddDbContextFactory` | ❌ *Unable to resolve service for type `ActivityStore`* |
| `AddScoped<StravaActivitySync>` | ✅ resolves — **and two scopes get two instances** |

So the only registration that works is the one under which **the guard does not guard**: a Blazor
circuit is a DI scope, two browser tabs are two circuits, and each gets its own semaphore. Feature 005's
FR-040 guarantee stops holding the moment a host with more than one scope exists — which is this
feature.

The coordinator is not introduced *for* that, though, and this matters under Principle III. The
specification independently requires state that outlives a circuit: the edge case *"What if the sync is
in progress when the page refreshes?"* (a refresh is a **new** circuit), FR-012's restore-on-reload, and
FR-010's "count of new activities" surviving long enough to be displayed. One singleton satisfies all
three and restores FR-040's guarantee as a by-product.

**Feature 005 is not modified.** Its semaphore becomes redundant rather than wrong — inside a single
scope it still behaves exactly as its tests assert, and changing a signed-off feature's concurrency
primitive to satisfy a consumer is a larger act than it looks. Recorded here so the completion review
examines it deliberately.

**Revisit trigger**: a second caller of `StravaActivitySync` outside the coordinator, at which point the
guard must move into Infrastructure properly.

### R16 — No caching, no stored metrics, no incremental recomputation

**Decision**: every dashboard load reads the whole activity history and recomputes everything.

**Verified by probe**, against the repository's real `Domain` and `Infrastructure` code, with a history
deliberately heavier than a real athlete's — 3 years 9 months, 953 activities, 485 of them carrying a
full-resolution heart-rate series of 3,600 samples (1.7 million samples in SQLite):

| | read from SQLite | compute | total |
|---|---|---|---|
| cold | 422 ms | 255 ms | **677 ms** |
| warm | 185 ms | 107 ms | **292 ms** |

SC-001 allows two seconds for 100+ days of history. The measured worst case is a third of that against
a history thirteen times longer. **No cache, no denormalized daily-metrics table, no background
recomputation, no memoization** — every one of which was a candidate and none of which is needed.

One inefficiency is visible and accepted: `AggregateDaily` and `AggregateWeekly` each recompute every
activity's TRIMP, so the walk happens twice (~100 ms of the total). Removing it would mean a new domain
overload taking a pre-computed daily series, which is a domain change to save time nobody is waiting
for.

**Revisit trigger**: a measured dashboard load above 2 seconds. Not a projection of one.

---

## Design decisions within the feature

### R14 — The trend shows the change always, and the label only when the week is complete

**Decision**: display `AbsoluteChange` and `RelativeChange` on every load, and
`TrendClassification` only for a completed week — showing "—" or "in progress" otherwise.

**Rationale**: this falls straight out of R5's resolution and is entirely feature 004's design, not a
display invention. `WeeklyLoadTrend` exposes the two changes unconditionally and gates `Classification`
on `IsComplete` (004 FR-016, FR-017), precisely because a week that is three days old is not comparable
like-for-like with a finished one. So the athlete sees the real numbers every day and a judgement only
when one is warranted. Without this split, FR-005 would render "indeterminate" on six days out of
seven.

**Alternatives considered**: comparing the current partial week against the same fraction of the
previous week (rejected: invents a pro-rating rule no specification states); comparing the last two
*complete* weeks instead (rejected: FR-004 asks for the *current* week's load, and the two panels would
then describe different weeks).

### R15 — One read of the history serves all five sections

**Decision**: `DashboardReader` performs a single `ActivityStore.InRangeAsync` covering the whole stored
history and hands the result to `DashboardViewBuilder`. No new store method.

**Rationale**: the obvious shape is a query per section — a "most recent 7" query for FR-007, a
current-week query for FR-004, a 180-day query for FR-006. But FR-001 – FR-003 need the *entire*
history regardless: `TrainingMetricsCalculator` refuses a history that does not reach back before the
requested range (it throws rather than assume rest days), and CTL is an exponential accumulation from
the first day. Once everything is in memory, the recent seven are a sort and a `Take`, and a dedicated
query would be a second code path returning a subset of what was already read. R16 shows the single
read is affordable.

**Revisit trigger**: R16's — if the full read ever stops being affordable, narrowing queries is the
first thing to do.

### R17 — Tests exercise the real store on real SQLite; no mocking library

**Decision**: continue feature 005's `SqliteFixture` pattern — real SQLite in memory with the connection
held open — for the reader's tests. No mocking library, no EF Core InMemory provider, no interface over
the store.

**Rationale**: 005's R8 rejected the InMemory provider on evidence (it succeeds at two queries that
throw on real SQLite), and that evidence has not changed. The dashboard's integration point is
"activities stored by the sync come back as the figures the calculators expect", which is only a real
test against a real store. Principle IV's preference for designs testable without mocking is already
satisfied: R11 makes the arithmetic a pure function, so the only thing needing a database is the thin
reader.

### R18 — `WebApplicationFactory` covers the endpoints and the empty state, not the interactive page

**Decision**: use `WebApplicationFactory` for the two connect endpoints (R6) and for the statically
rendered shell — that the dashboard route responds, that an unconnected application shows the empty
state and routes to connect. Interactive behaviour (the sync button, its loading state, the rendered
chart) is covered by bUnit.

**Rationale**: `WebApplicationFactory` issues HTTP requests. It can see what static server rendering
produces, which covers FR-011, FR-017 and the endpoints. It cannot drive a SignalR circuit, so it
cannot click the sync button — that needs a browser, which R10 already declined. The two tools cover
disjoint halves and neither duplicates the other.

**Scope**: no test performs a real OAuth exchange. The callback endpoint is tested against
`StubHttpMessageHandler`, which feature 005 already wrote for exactly this.

### R19 — Failures are displayed, never thrown into the circuit

**Decision**: the reader catches the specific exceptions the layers below are documented to throw and
converts them into displayable states; the dashboard never lets one escape into the render.

**The ones that actually exist**, each with a home in the specification:

| Source | Throws | Shown as |
|---|---|---|
| `ActivityRow.ToDomain` on a corrupt row | `ArgumentException` / `InvalidOperationException` | edge case: "display available data gracefully" |
| `TrainingMetricsCalculator.Calculate` on a short or discontinuous history | `ArgumentException` | FR-011 empty state, or "Not enough data" (US3 scenario 2) |
| `TrainingLoadTrendCalculator.Calculate` with under two ISO weeks | `ArgumentException` | US2 scenario 3's "—" |
| `StravaActivitySync.SyncAsync` with no connection | `InvalidOperationException` | FR-017 / US5 scenario 5 |
| `CalculateTrainingLoad` with a non-positive maximum | `ArgumentOutOfRangeException` | cannot occur — FR-015 refuses at startup |

**Rationale**: Principle VI requires deliberate handling and forbids silent swallowing, and the
specification's edge cases ask for "—" or "Data unavailable" rather than a crash. The last three rows
are the important ones: **they are reachable on ordinary data, not just corrupt data.** A brand-new
athlete with four days of history hits two of them. The read model must therefore *check* the
preconditions rather than catch the exceptions where it can — a history shorter than two ISO weeks is a
question with an answer, not an error — and catch only where it cannot.

No logging infrastructure is introduced; ASP.NET Core's `ILogger` is already in the host and is used
where a failure would otherwise be invisible.

### R20 — Migrations are applied explicitly, not by `EnsureCreated`

**Decision**: the host runs `db.Database.MigrateAsync()` at startup against the three migrations feature
005 generated.

**Rationale**: 005's R7 chose migrations over `EnsureCreated` and the migrations exist; `EnsureCreated`
would ignore them and create a schema that then cannot be migrated. This is the first process that runs
outside a test, so it is the first place the choice has to be honoured.

### R21 — The OAuth callback carries and checks a `state` value

**Decision**: the connect endpoint generates a random `state`, stores it in the session/anti-forgery
cookie, and the callback rejects a mismatch before calling `ExchangeAsync`.

**Rationale**: `StravaAuthorization.BuildAuthorizeUrl(redirectUri, state)` takes the parameter already —
feature 005 provided the mechanism and had no host to use it from. An unchecked `state` is the standard
OAuth cross-site request forgery hole, and this is the point at which it becomes reachable. It is a
handful of lines and it is the reason the parameter exists.

**Not in scope**: PKCE (Strava's confidential-client flow does not use it here), token refresh on a
schedule, or multi-account handling — `ExchangeAsync` already refuses a second athlete (005 FR-008).

### R22 — "Today" is the athlete's local calendar day, and the history window is clamped to it

**Decision**: the dashboard's notion of today is `TimeProvider.GetLocalNow()` reduced to a `DateOnly`,
and the daily history is built over `firstActivityDay … max(today, lastActivityDay)`.

**Rationale**: feature 002 attributes an activity to "the athlete's local day at the activity's own
recorded offset" (002 FR-003, FR-004 — `DateOnly.FromDateTime(activity.StartedAt.DateTime)`). Asking
for the current ISO week in UTC while activities are bucketed by local day puts a session ridden at
23:00 on a Sunday at UTC+3 into a different week from the one the athlete just finished — the weekly
total and its trend would both be wrong, and wrong only for evening sessions, which is the kind of
defect that survives a long time.

The clamp exists because `DateRange` refuses an end before its start and `TrainingMetricsCalculator`
refuses a history that stops short of the requested range. A single activity dated in the future — a
device with a wrong clock, an offset the athlete's phone got wrong — would otherwise either throw or
silently vanish from the daily totals while still appearing in the recent list. Taking the later of the
two makes the two panels agree.

**Alternatives considered**: UTC throughout (rejected: disagrees with how every stored figure is already
bucketed); a configured timezone (rejected: a setting nobody asked for, for a single-athlete MVP running
on the athlete's own machine).

---

## Summary of what was declined

| Declined | Why | Revisit trigger |
|---|---|---|
| A charting library | US3 accepts a legend; three polylines are a `string.Join` (R8) | zoom, pan, or cross-series interaction |
| A JS interop boundary | nothing needs the browser (R8) | as above |
| `TrainingLoadAnalyzer.Application` | none of project plan 22.2's three conditions moved (R2) | 22.2's conditions |
| `TrainingLoadAnalyzer.Api` + a contracts project | Interactive Server needs no wire format (R1) | the README's WebAssembly path |
| `IActivitySource` in the domain | one implementation, one caller, no second provider (R3) | a second provider |
| An `AthleteProfile` entity | configuration carries one value for one athlete (R4) | a second athlete |
| A mocking library | still nothing to mock (R17) | — |
| The EF Core InMemory provider | 005's R8 evidence stands (R17) | — |
| A cache or stored metrics table | 292–677 ms against a 2-second budget (R16) | a measured load over 2 s |
| A state-change event on the coordinator | a snapshot read satisfies the reload edge case (R13) | a requirement for live update after reload |
| Playwright / Selenium | a browser to assert a `<span>` (R10) | a requirement about real browser behaviour |
| Modifying `StravaActivitySync`'s guard | its own tests still hold within a scope (R13) | a second caller outside the coordinator |
