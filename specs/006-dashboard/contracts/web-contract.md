# Contract: `TrainingLoadAnalyzer.Web`

**Feature**: 006-dashboard | **Date**: 2026-09-18 | **Plan**: [../plan.md](../plan.md)

The surface this feature exposes: three HTTP routes, four configuration keys, the read model tests
bind to, and the components' parameters. Guarantees continue feature 005's numbering, which ended at
C71.

`TrainingLoadAnalyzer.Domain` and `TrainingLoadAnalyzer.Infrastructure` gain **no public member** in
this feature. Their contracts ([001](../../001-training-activity-domain/contracts/domain-api.md),
[002](../../002-training-load-aggregation/contracts/domain-api.md),
[005](../../005-strava-import/contracts/infrastructure-api.md)) are unchanged.

---

## 1. HTTP routes

| Route | Render mode | Purpose |
|---|---|---|
| `GET /` | Interactive Server | the dashboard (FR-001 – FR-013) |
| `GET /connect` | endpoint | FR-016 — redirect to Strava's consent screen |
| `GET /strava/callback` | endpoint | FR-016 — complete the exchange |

### `GET /`

| Condition | Response |
|---|---|
| history exists | 200, all five sections (FR-001 – FR-010) |
| connected, no activities | 200, empty state offering a sync (FR-011) |
| not connected | 200, empty state linking to `/connect` (FR-011, **FR-017**) |
| the read fails | 200, "Data unavailable" — never a 500 (R19, edge case) |

**C72** — `GET /` issues no outbound network request under any of those conditions (**FR-013**). The
only outbound request this application makes is a sync the athlete asked for, or the token exchange on
`/strava/callback`.

**C73** — `GET /` responds 200 with a usable page whether or not Strava is reachable, whether or not an
account is connected, and whether or not the stored credential is still valid (**SC-006**).

### `GET /connect`

Generates a random `state`, persists it in the athlete's session cookie, and redirects (302) to
`StravaAuthorization.BuildAuthorizeUrl(redirectUri, state)`.

**C74** — the `state` is freshly generated per request from a cryptographic source and is never
predictable or reused.

### `GET /strava/callback?code={code}&state={state}&scope={scope}`

| Condition | Response |
|---|---|
| `state` matches the stored value, exchange succeeds | 302 to `/` |
| `state` missing or mismatched | 400, exchange **not attempted** |
| Strava returned `error=access_denied` | 302 to `/` with a declined-consent message |
| `InsufficientScopeException` | the connection is refused, naming the withheld access (005 FR-002a) |
| a different athlete is already connected | the message from `ExchangeAsync`, unchanged (005 FR-008) |

**C75** — the `state` is checked **before** `ExchangeAsync` is called, so a forged callback never
reaches the token endpoint (**R21**).

**C76** — no route, redirect, log line, error page or query string in this feature contains an access
token, a refresh token, an authorization code or the client secret (005 FR-005, C51, C66).

---

## 2. Configuration

| Key | Required | Used by |
|---|---|---|
| `Athlete:MaximumHeartRate` | **yes** | every measured load (**FR-014**) |
| `Strava:ClientId` | yes | `StravaCredentials` |
| `Strava:ClientSecret` | yes | `StravaCredentials` |
| `ConnectionStrings:Import` | yes | `ImportDbContext` |

**C77** — startup **fails** when `Athlete:MaximumHeartRate` is absent, non-numeric or not positive. The
application does not start and then display figures computed from a default (**FR-015**).

**C78** — no secret is committed. `Strava:ClientSecret` comes from user secrets or the environment, as
feature 005 established (005 R12).

---

## 3. The read model

```csharp
namespace TrainingLoadAnalyzer.Web.Features.Dashboard;

public sealed record RecentActivity(
    DateOnly Day,
    ActivityType Type,
    TimeSpan MovingTime,
    TrainingLoad Load);

public sealed record DashboardView
{
    public required DateOnly AsOf { get; init; }
    public required bool IsStravaConnected { get; init; }
    public bool IsUnavailable { get; init; }

    public IReadOnlyList<DailyTrainingMetrics> Metrics { get; init; }
    public WeeklyTrainingLoad? CurrentWeek { get; init; }
    public WeeklyLoadTrend? Trend { get; init; }
    public IReadOnlyList<RecentActivity> Recent { get; init; }

    public DailyTrainingMetrics? Current { get; }
    public bool HasActivities { get; }
    public bool HasEnoughHistoryForChart { get; }
}

public static class DashboardViewBuilder
{
    /// <summary>
    ///   The dashboard's figures for <paramref name="today"/>. Pure: the same arguments always
    ///   yield an equal view, on any date, with no read of the clock or of storage.
    /// </summary>
    public static DashboardView Build(
        IReadOnlyList<TrainingActivity> activities,
        DateOnly today,
        int maximumHeartRate,
        bool isStravaConnected);
}
```

**C79** — `Build` is pure. It reads no clock, no storage, no environment and no static mutable state,
and the order of `activities` does not affect the result (continuing 002 C13). "Equal" here means every
value the view carries, asserted member by member: `DashboardView` is a record, and a record compares
its collection members by reference, so two separate builds are never `Equals` however identical their
contents. Giving the record a structural equality nobody needs, in order to satisfy a test, would be
the wrong way round.

**C80** — `Current` is `Metrics[^1]` and is derived on every read, so the headline tiles and the chart's
final point can never disagree (**SC-002**).

**C81** — `Build` never throws for any history a running application can hold, including: no
activities, one activity, a history shorter than a week, a history shorter than 30 days, a history with
long gaps, and an activity dated in the future (**FR-011**, US1 sc2, US2 sc3, US3 sc2, R19, R22).

**C82** — `Trend` is null when, and only when, the history does not reach the ISO week before the
current one. `Build` establishes this by checking the precondition, never by catching
`TrainingLoadTrendCalculator`'s exception (R19).

**C83** — `Metrics` covers at most 180 days ending on `today`, one entry per day, gap-free and
ascending — it is `TrainingMetricsCalculator`'s output unmodified (**FR-006**).

**C84** — `Recent` holds at most 7 entries, ordered newest first by `StartedAt`, and every entry's
`Load` carries its `Provenance` (**FR-007**, **FR-008**, **SC-004**, US4 sc4).

**C85** — no Strava type appears anywhere in `DashboardView`, `RecentActivity` or
`DashboardViewBuilder` (Principle II).

### `DashboardReader`

```csharp
public sealed class DashboardReader(
    IDbContextFactory<ImportDbContext> factory,
    TimeProvider clock,
    AthleteSettings settings,
    ILogger<DashboardReader> logger)
{
    public Task<DashboardView> ReadAsync(CancellationToken cancellationToken);
}
```

**C86** — `ReadAsync` creates its own `ImportDbContext` per call through the factory and disposes it,
so no change-tracking state is shared between dashboard loads or between circuits (**R12**).

**C87** — `ReadAsync` returns a view with `IsUnavailable` set rather than propagating an exception when
the stored data cannot be read — a corrupt row included (**R19**, edge case). It catches named exception
types, never `Exception`, and **never** `OperationCanceledException`: a cancelled request is not a
corrupt database. Each occurrence is logged, because the athlete's "Data unavailable" says what happened
and nothing about why (Principle VI).

**C88** — `ReadAsync` derives `today` from `TimeProvider.GetLocalNow()`, matching how feature 002
attributes an activity to a calendar day (**R22**).

---

## 4. The chart

```csharp
public sealed record ChartSeries(string Label, string Points, string CssClass);

public static class MetricsChart
{
    public static IReadOnlyList<ChartSeries> Plot(
        IReadOnlyList<DailyTrainingMetrics> metrics,
        double width,
        double height);
}
```

**C89** — every coordinate is formatted with `CultureInfo.InvariantCulture`, so the rendered `points`
attribute is identical on every machine regardless of the operating system's locale (**R9**).

**C90** — each returned series contains exactly one point per entry in `metrics`, in the same order,
with none skipped or duplicated (**SC-003**).

**C91** — `Plot` returns three series — Fitness, Fatigue and Form — or an empty list when `metrics` is
empty. It never returns a series with a malformed `points` attribute.

**C92** — the vertical scale covers the minimum and maximum across all three series, so a negative Form
is plotted rather than clipped.

---

## 5. Sync

```csharp
namespace TrainingLoadAnalyzer.Web.Features.Sync;

public sealed record SyncStatus
{
    public static readonly SyncStatus Never;
    public bool IsRunning { get; init; }
    public SyncResult? Result { get; init; }
    public string? Failure { get; init; }
    public DateTimeOffset? FinishedAt { get; init; }
}

public static class SyncMessage
{
    public static string For(SyncStatus status);
}

public sealed class SyncCoordinator   // registered as a singleton
{
    public SyncStatus Status { get; }
    public Task<SyncStatus> RunAsync(CancellationToken cancellationToken);
}
```

**C93** — `SyncCoordinator` is a singleton, so its `Status` outlives any one circuit and is what a
reloaded page reads (**FR-012**, **FR-012a**, edge case "sync in progress when the page refreshes").
Nothing is written to browser session storage or local storage, and no JavaScript interop exists to
write it with (**FR-012a**, research R23).

**C94** — at most one sync runs at a time across the whole process. A second request while one is in
flight returns the running status and starts nothing, restoring the guarantee 005 FR-040 intended
(**R13**).

**C95** — `RunAsync` never throws. Every ending — completed, rate-limited, interrupted, reconnection
required, refused, or no account connected — is a `SyncStatus` the page can display (Principle VI, 005
FR-039).

**C96** — `RunAsync` resolves `StravaActivitySync` inside a DI scope it creates and disposes per run, so
the sync's `ImportDbContext` is never a circuit-lifetime one (**R12**, **R13**).

**C97** — `SyncMessage.For` is pure and total: every `SyncStatus` has a message, and no message
contains a credential (C76).

**C98** — the sync button calls `SyncCoordinator`, never `StravaActivitySync` directly. It is the
coordinator's only caller in this feature (the revisit trigger recorded in R13).

---

## 6. Components

| Component | Parameters | Requirements |
|---|---|---|
| `Dashboard.razor` (`@page "/"`) | — | FR-001 – FR-013 |
| `MetricTile.razor` | `Label`, `Value` (`double?`), `Basis` (`LoadBasis`), `IsReliable` | FR-001 – FR-003, US1 sc2 |
| `WeeklyLoadPanel.razor` | `Week` (`WeeklyTrainingLoad?`), `Trend` (`WeeklyLoadTrend?`) | FR-004, FR-005, FR-005a |
| `MetricsChartView.razor` | `Metrics`, `HasEnoughHistory` | FR-006, US3 |
| `RecentActivityList.razor` | `Activities` (`IReadOnlyList<RecentActivity>`) | FR-007, FR-008 |
| `SyncPanel.razor` | `Status`, `OnSync` (`EventCallback`) | FR-009, FR-010 |

**C99** — every numeric figure a component renders is formatted against a fixed culture, so SC-002's
worked example "45.3" reads as `45.3` on every machine (**R9**, the spec's amended *Display rounding*
assumption).

**C100** — a missing figure renders as `—`, never as `0`, `NaN` or an empty element (US1 sc2, US2 sc3,
US3 sc2).

**C101** — a `Measured` load and an `Estimated` load are distinguishable in the rendered markup, not
only by styling a screen reader would miss (**FR-008**, US4 sc2, sc3).

**C102** — no component reads `ImportDbContext`, `ActivityStore`, `StravaActivitySync` or `TimeProvider`
directly. Components take a `DashboardView` and a `SyncStatus`; the reader and the coordinator are the
only things that touch the layers below.

**C103** — no component computes a training figure. Rounding for display is the only arithmetic any of
them performs (**FR-005**'s "MUST NOT apply a threshold rule of its own", Principle II).
