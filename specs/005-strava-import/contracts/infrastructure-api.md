# Contract: TrainingLoadAnalyzer.Infrastructure public API

**Feature**: 005-strava-import | **Date**: 2026-09-17

This feature's contract is the public surface of a **new assembly**. Features 001–004 documented the
domain library's surface and are unchanged here; guarantees **C1–C45** continue to hold
([001](../../001-training-activity-domain/contracts/domain-api.md),
[002](../../002-training-load-aggregation/contracts/domain-api.md),
[003](../../003-fitness-fatigue-form/contracts/domain-api.md),
[004](../../004-load-trends/contracts/domain-api.md)). This feature adds **C46–C71** and changes no
existing guarantee.

There is still no HTTP endpoint, CLI, or wire format the analyzer itself exposes. Strava's API is
consumed, never served.

Signatures are shown without bodies; semantics are in [data-model.md](../data-model.md) and the
numbered requirements in [spec.md](../spec.md). Research decisions R2, R14 and R21 were answered on
2026-09-17 and are reflected here.

Namespace: `TrainingLoadAnalyzer.Infrastructure`

---

## Connecting

```csharp
public sealed record StravaCredentials(string ClientId, string ClientSecret);

public sealed class StravaAuthorization
{
    public StravaAuthorization(HttpClient http, StravaCredentials credentials, TimeProvider clock);

    /// <summary>
    ///   The URL to send the athlete to. Requests activity:read_all so private activities are
    ///   visible (FR-002). The redirect is caught by Feature 6, not here (research R11).
    /// </summary>
    public Uri BuildAuthorizeUrl(Uri redirectUri, string state);

    /// <summary>
    ///   Exchanges a one-time authorization code for tokens (FR-001). Refuses if the athlete
    ///   differs from one whose activities are already stored (FR-008).
    /// </summary>
    public Task<StravaConnection> ExchangeAsync(string code, CancellationToken cancellationToken);

    /// <summary>
    ///   Renews an expired access token, storing the rotated refresh token (FR-004, research R13).
    ///   Throws ReconnectionRequiredException when Strava rejects the renewal (FR-006).
    /// </summary>
    public Task<StravaConnection> RefreshAsync(
        StravaConnection connection,
        CancellationToken cancellationToken);

    /// <summary>Revokes at Strava and discards the stored credentials (FR-007).</summary>
    public Task DisconnectAsync(CancellationToken cancellationToken);
}
```

- **C46**: `BuildAuthorizeUrl` never transmits a password and never accepts one; the athlete's
  credentials are entered only on Strava's own page (FR-001).
- **C47**: The scope requested always includes `activity:read_all`, so private activities are in scope
  (FR-002, FR-012).
- **C48**: Every successful token response replaces **both** stored tokens. The refresh token returned
  by Strava is persisted before the access token is used for anything (FR-004, research R13).
- **C49**: `RefreshAsync` throws `ReconnectionRequiredException` — never a generic failure, never a
  successful-but-empty result — when the renewal credential is rejected (FR-006).
- **C50**: `ExchangeAsync` refuses, naming the mismatch, when the authorizing athlete differs from the
  one whose activities are stored (FR-008).
- **C51**: No member of this type returns, logs, or includes a token in any exception message (FR-005).
- **C52**: `ExchangeAsync` refuses, naming the withheld access, when Strava's grant does not cover
  private activities, and stores no partial connection. The granted scope is recorded on every
  connection that is accepted (FR-002a).

---

## Storing

```csharp
public sealed class ImportDbContext : DbContext
{
    public ImportDbContext(DbContextOptions<ImportDbContext> options);

    public DbSet<ActivityRow> Activities { get; }
    public DbSet<StravaConnection> Connections { get; }
    public DbSet<SyncState> SyncStates { get; }
}

public sealed class ActivityStore
{
    public ActivityStore(ImportDbContext db);

    /// <summary>Inserts or overwrites by (Provider, ExternalId) — never duplicates (FR-023).</summary>
    public Task UpsertAsync(
        TrainingActivity activity,
        bool heartRateOutstanding,
        CancellationToken cancellationToken);

    /// <summary>Sessions whose start falls inside the range, ascending (FR-031).</summary>
    public Task<IReadOnlyList<TrainingActivity>> InRangeAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken);
}
```

- **C53**: A session read back equals the session written, member for member — the instant, its offset,
  the moving time, the type, and every heart-rate sample (FR-024).
- **C54**: Storing the same activity twice leaves exactly one row (FR-023, FR-030).
- **C55**: Identity is `(Provider, ExternalId)` only. Two activities with identical starts, durations,
  and types are two rows.
- **C56**: A stored heart-rate series is never removed or replaced by any later operation (FR-017b).
- **C57**: `ToDomain()` runs the domain constructors, so a corrupted row throws at the store's boundary
  rather than yielding a wrong figure (research R4, R6).
- **C58**: No load value is computed, stored, or cached anywhere in this assembly (FR-021).

---

## Synchronizing

```csharp
public sealed class StravaActivitySync
{
    public StravaActivitySync(
        StravaApiClient client,
        ActivityStore store,
        ImportDbContext db,
        TimeProvider clock);

    /// <summary>Reads from the stored resume point onward (FR-027).</summary>
    public Task<SyncResult> SyncAsync(CancellationToken cancellationToken);

    /// <summary>Reads the whole history regardless of the resume point (FR-032).</summary>
    public Task<SyncResult> FullResyncAsync(CancellationToken cancellationToken);
}
```

- **C59**: Exactly one entry point per mode. There is no per-activity import method, no "just fetch the
  streams" method, and no way to run reconciliation on its own.
- **C60**: Every return is a `SyncResult`. A sync that imported nothing returns `Completed` with zero
  counts and never a failure (FR-033).
- **C61**: `Removed` is non-empty **only** when `Outcome` is `Completed`. No other outcome can delete a
  stored session (FR-031c). This is the guarantee the whole reconciliation design rests on.
- **C62**: `RetryAfter` is set when and only when `Outcome` is `RateLimited`, and names the next
  quarter-hour boundary rather than a fixed delay (FR-035, research R16).
- **C63**: A second concurrent call returns `Outcome.Refused` rather than blocking or running alongside
  the first (FR-040).
- **C64**: Sessions stored by a sync that stopped early are kept, and the resume point is left
  consistent with what was stored (FR-026, FR-029, FR-035).
- **C65**: One unusable or out-of-scope activity never aborts a sync; it is counted in `Skipped` with
  its reason and the walk continues (FR-011, FR-038).
- **C66**: `SyncResult` contains no credential in any field or message (FR-005, FR-038).
- **C67**: An HTTP 404 from the streams endpoint is treated as "no streams" and never as a failure
  (research R20, FR-017d).
- **C68**: A 429 is never retried. It sets `RateLimited` and stops (FR-034, FR-037, research R16).

---

## What this assembly does not expose

- **C69**: No type in this assembly appears in any signature of `TrainingLoadAnalyzer.Domain`. The
  dependency runs one way only: Infrastructure references Domain, never the reverse (FR-018,
  Principle II).
- **C70**: Nothing here writes to Strava. No member creates, edits, uploads, or deletes an activity,
  and the scope requested does not permit it (FR-002).

---

## Notes on the surface

**Asynchrony is genuine here, unlike in features 001–004.** Every method above that returns `Task`
performs real I/O — a network request or a database write. No method is asynchronous for symmetry.

**`CancellationToken` on every I/O method** is not ceremony: a first import takes minutes and stops at
a rate limit partway through (research R24), so a caller must be able to abandon it. Cancellation mid
sync behaves as `Interrupted` — what was stored is kept, and nothing is removed.

**Collaborators are constructor parameters, not a container.** This feature registers nothing with a DI
container because it has no host ([research.md](../research.md) R3). Feature 6 will register these
types; their shape is already what that requires.

**`IActivitySource` is absent by decision, not by omission** ([research.md](../research.md) R2). The
project boundary is what satisfies Principle II here; the interface arrives in Feature 6, shaped by its
first consumer outside this assembly.

- **C71**: Heart-rate samples outside the range the domain accepts are discarded in the mapper, before
  `HeartRateSeries` is constructed, and the count is reported per activity on `SyncResult`. A series is
  unusable only when fewer than two samples survive (FR-017f, FR-017g). No sample the domain would
  refuse is ever passed to it, and no domain invariant is relaxed.
