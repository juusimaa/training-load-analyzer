# Data Model: Strava Import

**Feature**: 005-strava-import | **Date**: 2026-09-17

What this feature stores, what it passes around, and what it deliberately does not create. Every type
here lives in `TrainingLoadAnalyzer.Infrastructure`. **The domain project gains nothing** — no type, no
property, no package reference, no project reference. Rationale for each shape is in
[research.md](./research.md); the requirements are in [spec.md](./spec.md).

Research decisions R2, R14 and R21 were answered on 2026-09-17 and are reflected throughout.

---

## Stored: `ActivityRow`

The persistence shape of one imported session. Not a domain type, and never returned to a caller — it
converts to and from `TrainingActivity` at the boundary (R4).

### Columns

| Column | Type | Why |
|--------|------|-----|
| `Provider` | `string` | Part of identity. `"Strava"` throughout this feature (FR-014, FR-023) |
| `ExternalId` | `string` | Strava's activity id, verbatim, unparsed (FR-014) |
| `StartedAtUtcTicks` | `long` | The instant, queryable and orderable in SQL (R5, FR-015) |
| `StartedAtOffsetMinutes` | `short` | The athlete's offset from UTC at that moment (R5, FR-015) |
| `MovingTimeTicks` | `long` | Moving time, never elapsed (R5, FR-016) |
| `Type` | `string` | `Running` or `Cycling`, as text (FR-010) |
| `HeartRateJson` | `string?` | `[[ticks, bpm], …]`, or SQL `NULL` when no series is held (R6, FR-017) |
| `HeartRateOutstanding` | `bool` | A series was owed and could not be fetched (FR-017d, FR-017e) |

**Primary key**: `(Provider, ExternalId)` — composite, natural, and exactly FR-023's statement of
identity. Nothing about a session's start, duration, or type participates in it, so two activities that
happen to coincide are two rows (an explicit edge case in the spec).

**Index**: `StartedAtUtcTicks`. Every query this feature makes is a range over it — the resume point
(FR-027), the reconciliation span (FR-031), the measured window (FR-017a).

### Why two columns for one instant

`DateTimeOffset` round-trips through SQLite exactly, including the offset — that was verified. What it
cannot do is be compared or ordered: `OrderBy(x => x.StartedAt)` throws `NotSupportedException` and
`Where(x => x.StartedAt > cutoff)` fails to translate, because SQLite stores it as text. Ticks plus
offset minutes restores both, and reconstructs exactly. Full detail and the verified failure messages
are in [research.md](./research.md) R5.

### `HeartRateJson` null versus `HeartRateOutstanding`

Three states, and the two columns are needed to tell them apart:

| `HeartRateJson` | `HeartRateOutstanding` | Meaning |
|---|---|---|
| `NULL` | `false` | No series exists to fetch — Strava reported none, or the activity is outside the measured window. Settled; load is estimated. |
| `NULL` | `true` | A series was owed and the fetch failed. Holds the resume point back so the next sync retries (FR-017d, FR-017e). |
| text | `false` | The series is held. Load is measured, and FR-017b forbids ever undoing this. |

A single nullable column could not distinguish the first two, and the difference decides whether the
sync comes back for it.

### Conversions

`From(TrainingActivity, provider, outstanding)` and `ToDomain()`. `ToDomain()` runs the real
`TrainingActivity` and `HeartRateSeries` constructors, so the domain's invariants are re-checked on
every load and a corrupted row is caught at the boundary — on the sync's own call stack, where it can
be reported per row, rather than from inside EF Core's materializer (R4, R6).

---

## Stored: `StravaConnection`

The standing permission to read one athlete's data. **At most one row exists** (FR-008).

| Column | Type | Why |
|--------|------|-----|
| `AthleteId` | `long` | Which Strava athlete. What FR-008 compares against |
| `AccessToken` | `string` | Secret. Never logged, never in a summary (FR-005) |
| `RefreshToken` | `string` | Secret. **Replaced on every token response** (R13, FR-004) |
| `ExpiresAtUtcTicks` | `long` | From Strava's `expires_at`, epoch seconds, converted |
| `GrantedScopes` | `string` | What Strava actually granted. A grant missing private-activity access is refused outright (FR-002a, R14) |
| `ConnectedAtUtcTicks` | `long` | When the athlete authorized |

**The client id and client secret are not here.** They belong to the application registration, not to
the athlete, and live in environment variables (R12). A copied database file must not leak the
application's identity along with the athlete's data.

**`RefreshToken` is the one field where a mistake is unrecoverable.** Strava rotates it on every
successful token request and invalidates the old one immediately; an implementation that keeps the
original works until the first rotation and then locks the athlete out permanently, presenting as
FR-006's reconnection-required failure. See [research.md](./research.md) R13.

---

## Stored: `SyncState`

What the next sync needs in order to be incremental (FR-025). One row, alongside the connection.

| Column | Type | Why |
|--------|------|-----|
| `ResumePointUtcTicks` | `long` | Where the next sync starts reading (FR-027, FR-028) |
| `LastSyncStartedAtUtcTicks` | `long?` | When the last sync ran (FR-025) |
| `LastOutcome` | `string` | How it ended — the values of `SyncOutcome` below (FR-025, FR-039) |

**The resume point is stored, and it is also derivable.** FR-025 requires it to be persisted, so it is
a column. Its value is recomputed at the end of every sync as

```text
min( latest successfully stored activity start,
     earliest activity whose series is still outstanding )   −   7 days
```

and written back. Storing the result of that computation rather than deriving it on read keeps one
durable answer to "where do I start", which is what FR-025 asks for; recomputing it from the activities
each time keeps it honest against FR-029, which forbids it advancing over anything not durably stored.

On a first sync, with no activities at all, it is the Unix epoch.

The second term is FR-017e. An activity stored without the series it was owed holds the resume point
behind itself so the next sync re-reads it, which is why no separate queue of outstanding work exists
(R18). It is self-limiting: once that activity ages past the 180-day measured window it no longer
qualifies under FR-017 and stops holding anything back, so a series that never arrives degrades to
estimated load instead of wedging the sync.

---

## Returned: `SyncResult`

What one sync did (FR-038). Returned to the caller, never stored, and **carrying no credentials**.

| Member | Type | Requirement |
|--------|------|-------------|
| `Imported` | `int` | Sessions newly stored |
| `Updated` | `int` | Existing sessions overwritten (FR-030) |
| `Removed` | `IReadOnlyList<RemovedActivity>` | Reconciled away, each by identifier (FR-031d) |
| `Skipped` | `IReadOnlyList<SkippedActivity>` | Each with its reason (FR-011, FR-038) |
| `SeriesOutstanding` | `int` | Series owed but not retrieved (FR-017d) |
| `DiscardedSamples` | `IReadOnlyList<DiscardedSamples>` | Implausible samples dropped, per activity (FR-017f, FR-017g) |
| `Outcome` | `SyncOutcome` | Whether it finished, and why not (FR-038, FR-039) |
| `RetryAfter` | `DateTimeOffset?` | Present when a limit stopped it (FR-035) |

### `SyncOutcome` (enum)

| Value | When | Requirement |
|-------|------|-------------|
| `Completed` | Every page read; reconciliation ran | FR-038 |
| `RateLimited` | A read limit was reached; `RetryAfter` is set | FR-035 |
| `Interrupted` | Connection or server failure after bounded retries | FR-037 |
| `ReconnectionRequired` | Strava rejected the renewal credential | FR-006 |
| `Refused` | A sync was already running | FR-040 |

`Completed` is the **only** value under which reconciliation may delete anything (FR-031c). That is the
single most consequential line in this data model: every other outcome means the set of activity ids
seen is partial, and deleting against a partial set removes training the athlete actually did.

A sync that found nothing new is `Completed` with zero counts, never a failure (FR-033).

### `SkippedActivity` and `RemovedActivity`

`SkippedActivity(string ExternalId, SkipReason Reason)` where `SkipReason` is `SportOutOfScope`
(FR-009, FR-010) or `UnusableByDomain` (FR-011 — a non-positive moving time or a missing start, the
cases `TrainingActivity`'s constructor refuses).

`RemovedActivity(string ExternalId, RemovalReason Reason)` where `RemovalReason` is `DeletedAtSource`
or `SportNowOutOfScope` (FR-031e).

`DiscardedSamples(string ExternalId, int Count)` records implausible heart-rate samples dropped during
mapping (FR-017f). It is not a skip — the activity was imported, and usually with measured load — but it
is a change to what Strava reported, so FR-017g makes it reportable rather than silent.

**Where the discarding happens matters.** It is done in the mapper, on the raw stream arrays, *before*
`HeartRateSeries` is constructed. The domain's invariants are therefore untouched: it still refuses
exactly what feature 001 made it refuse, and Infrastructure simply does not hand it samples it would
refuse. A series is unusable, and the activity falls to FR-017d, only when fewer than two samples
survive.

---

## Passed around: the Strava-side types

These model Strava's responses. They exist only inside Infrastructure, are never stored in this form,
and never cross into the domain (FR-018).

- **`StravaActivitySummary`** — the fields FR-014 to FR-017 need: `id`, `sport_type`, `start_date`,
  `utc_offset`, `moving_time`, `has_heartrate`, `manual`, `private`. Note that `utc_offset` and
  `has_heartrate` appear only in Strava's example payloads and not in any published schema, so all
  four heart-rate-adjacent fields must be treated as optional ([research.md](./research.md) R15).
  `start_date_local` is **not** used: it carries a trailing `Z` while holding local wall-clock time,
  which is a trap. The offset comes from `utc_offset` (FR-015).
- **`StravaStreamSet`** — `time` and `heartrate` as integer arrays. A missing stream is an **absent
  key**, not a null, so this must be key-checked rather than indexed (R20).
- **`StravaTokens`** — `access_token`, `refresh_token`, `expires_at`, and `scope`. The last is what
  R14 turns on.
- **`RateLimitStatus`** — the four values parsed from `X-ReadRateLimit-Limit` and
  `X-ReadRateLimit-Usage`, matched case-insensitively. Carries the next quarter-hour boundary, because
  Strava's fifteen-minute windows reset at :00, :15, :30 and :45 rather than fifteen minutes after the
  request (R16).

### Mapping `sport_type` (FR-010)

| Strava `sport_type` | Becomes |
|---|---|
| `Run`, `TrailRun`, `VirtualRun` | `ActivityType.Running` |
| `Ride`, `GravelRide`, `MountainBikeRide`, `VirtualRide` | `ActivityType.Cycling` |
| anything else | skipped, `SkipReason.SportOutOfScope` |

Read from `sport_type`, never from `type`. The legacy `type` field is lossy by Strava's own
documentation — a `TrailRun` reports `type: "Run"` and a `GravelRide` reports `type: "Ride"` — so
branching on it would silently collapse distinctions this table depends on. `EBikeRide` and
`EMountainBikeRide` are both named in the spec's ignored list so their exclusion reads as a decision.

Treadmill runs need no special case: Strava has no treadmill sport type, and they arrive as `Run` with
`trainer: true` (FR-010b).

---

## Types deliberately *not* created

Each of these is a shape a reviewer might expect, with the reason it is absent and the trigger that
would bring it back.

| Not created | Why | Revisit when |
|---|---|---|
| `IActivitySource` | Both sides would live in Infrastructure; it would decouple nothing (R2, resolved) | Something outside Infrastructure needs activity data — Feature 6 |
| `IActivityRepository` | One caller, one implementation, in the same assembly (Principle III) | Feature 6 reads stored sessions from another project |
| `TrainingLoadAnalyzer.Application` | No host exists, so no use case has two callers. Project plan 22.2 revisited and unchanged | Feature 6, alongside the render-mode decision in 22.1 |
| A provider-neutral `ExternalActivity` | One provider. The `Provider` column exists for identity (FR-023), not as a seam | A second provider is actually specified |
| A retry/resilience policy type | FR-037 is three attempts with backoff — about fifteen lines (R17) | A second failure policy is needed |
| A stored "sync running" flag | Single process by the spec's Assumptions; an in-process lock suffices and needs no crash recovery (R22) | A scheduler or a second host appears |
| An outstanding-series work queue | The resume point already does it, self-limitingly (R18, FR-017e) | Series retrieval stops being tied to the measured window |
| A `TrainingActivity` change to fit EF Core | Would let persistence dictate a domain signature (R4) | Never — this is the inversion Principle II exists to prevent |

---

## Requirement trace

All 55 functional requirements, against what implements them.

| Requirements | Implemented by |
|---|---|
| FR-001, FR-002 | Authorize-URL construction and the scope requested |
| FR-002a | `StravaConnection.GrantedScopes`, compared against what was requested before the connection is accepted |
| FR-003, FR-012 | `StravaConnection` (stored); `activity:read_all` is what makes private activities visible |
| FR-004, FR-006 | Token renewal on the connection, with `SyncOutcome.ReconnectionRequired` — and R13's rotation rule |
| FR-005 | No credential on `SyncResult`, in logs, or in any tracked file; client secret in the environment (R12) |
| FR-007 | Disconnect discards `StravaConnection`; revocation via Strava's `/oauth/revoke` (R11) |
| FR-008 | `StravaConnection.AthleteId`, compared before a second authorization is accepted |
| FR-009, FR-010, FR-010a, FR-010b | The `sport_type` table above; `SkipReason.SportOutOfScope` |
| FR-011 | `SkipReason.UnusableByDomain` — the cases `TrainingActivity`'s constructor refuses |
| FR-013 | No filter on `manual`; manual activities import, and their 404 from streams is not a failure (R20) |
| FR-014, FR-023 | `(Provider, ExternalId)` composite key |
| FR-015 | `StartedAtUtcTicks` + `StartedAtOffsetMinutes`, from `start_date` and `utc_offset` |
| FR-016 | `MovingTimeTicks`, from `moving_time` |
| FR-017 – FR-017c | The measured window, `has_heartrate`, `HeartRateJson`, and omitting `resolution` (R20) |
| FR-017d, FR-017e | `HeartRateOutstanding` plus the resume point's second term |
| FR-017f, FR-017g | Sample filtering in the mapper, before `HeartRateSeries` is built; `DiscardedSamples` on the result |
| FR-018 | Strava types confined to Infrastructure; the domain project is untouched |
| FR-019 | `From(...)` is a pure function of the summary and the streams |
| FR-020 | Deserialization ignores unknown members |
| FR-021 | No load column anywhere in this model |
| FR-022, FR-024 | `ActivityRow` and its conversions; asserted both ways (R8) |
| FR-025, FR-026 | `SyncState`; a page is stored in one transaction |
| FR-027 – FR-029 | The resume point, its seven-day look-back, and its recomputation |
| FR-030, FR-032 | Upsert on the composite key; full resync ignores the resume point |
| FR-031 – FR-031e | Reconciliation over a span read to completion; `RemovedActivity` |
| FR-033 | `Completed` with zero counts |
| FR-034 – FR-036 | `RateLimitStatus`, `SyncOutcome.RateLimited`, `RetryAfter` at the quarter-hour |
| FR-037 | Bounded retry on connection failures and 5xx only; never on 429 (R16, R17) |
| FR-038, FR-039 | `SyncResult` and `SyncOutcome` |
| FR-040 | `SyncOutcome.Refused` behind an in-process lock (R22) |
| FR-041 | The domain project and its 204 tests are untouched |
| FR-042 | `HttpMessageHandler` stub plus in-memory SQLite (R8, R9) |
| FR-043 | Purpose-built fixtures; no real personal data |
