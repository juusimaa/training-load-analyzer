# Phase 1 Data Model: Dashboard

**Feature**: 006-dashboard | **Date**: 2026-09-18 | **Plan**: [plan.md](./plan.md)

This feature stores **nothing new**. There is no entity, no table, no migration and no schema change:
every figure on the dashboard is derived on read from activities feature 005 already persists, using
calculators features 001–004 already built.

What it adds is a **read model** — the shape the page renders — and the two small pieces of host state
that outlive a render. Everything here lives in `TrainingLoadAnalyzer.Web`. Nothing is added to
`TrainingLoadAnalyzer.Domain` or `TrainingLoadAnalyzer.Infrastructure`.

---

## 1. What already exists and is reused unchanged

| Type | From | Used for |
|---|---|---|
| `TrainingActivity` | Domain (001) | the stored history, read once per dashboard load |
| `TrainingLoad(decimal Points, LoadProvenance Provenance)` | Domain (001) | FR-007, **FR-008** — the two parts are inseparable by construction |
| `DailyTrainingLoad` | Domain (002) | the continuous daily series the metrics are computed from |
| `WeeklyTrainingLoad(IsoWeek, Points, ActivityCount, Basis)` | Domain (002) | **FR-004** — the current ISO week's total |
| `DailyTrainingMetrics(Day, Fitness, Fatigue, IsReliable, FitnessBasis, FatigueBasis)` + derived `Form` | Domain (003) | **FR-001 – FR-003**, **FR-006** |
| `WeeklyLoadTrend` + derived `AbsoluteChange`, `RelativeChange`, `Classification` | Domain (004) | **FR-005**, **FR-005a** |
| `ActivityStore.InRangeAsync` | Infrastructure (005) | the single read (research R15) |
| `StravaActivitySync.SyncAsync` → `SyncResult` / `SyncOutcome` | Infrastructure (005) | **FR-009**, **FR-010** |
| `StravaAuthorization.BuildAuthorizeUrl` / `ExchangeAsync` | Infrastructure (005) | **FR-016** |
| `ImportDbContext.Connections` | Infrastructure (005) | **FR-017** — whether an account is connected |

**No method is added to `ActivityStore`** and no query is added for the recent-activities list: the
whole history is already in memory, so the seven most recent are a sort and a `Take` (research R15).

---

## 2. The read model

### `RecentActivity`

One row of FR-007's list.

```csharp
public sealed record RecentActivity(
    DateOnly Day,
    ActivityType Type,
    TimeSpan MovingTime,
    TrainingLoad Load);
```

| Field | Requirement | Note |
|---|---|---|
| `Day` | FR-007 | the athlete's local day at the activity's own offset, matching how 002 buckets it |
| `Type` | FR-007 | `Running` or `Cycling` — the only two that exist |
| `MovingTime` | FR-007 | formatted for display by the component, not here |
| `Load` | FR-007, **FR-008** | carries `Points` **and** `Provenance` together |

`Load` is the domain's `TrainingLoad`, not a decimal plus a flag. Feature 001 made the two
inseparable specifically so a consumer "cannot accidentally drop it" (001 C: SC-007), which is exactly
FR-008's requirement. Flattening it here would reintroduce the mistake that type was shaped to prevent.

### `DashboardView`

Everything one dashboard load displays.

```csharp
public sealed record DashboardView
{
    public required DateOnly AsOf { get; init; }
    public required bool IsStravaConnected { get; init; }
    public bool IsUnavailable { get; init; }

    public IReadOnlyList<DailyTrainingMetrics> Metrics { get; init; } = [];
    public WeeklyTrainingLoad? CurrentWeek { get; init; }
    public WeeklyLoadTrend? Trend { get; init; }
    public IReadOnlyList<RecentActivity> Recent { get; init; } = [];

    // Derived, never stored.
    public DailyTrainingMetrics? Current => Metrics.Count == 0 ? null : Metrics[^1];
    public bool HasActivities => Recent.Count > 0;
    public bool HasEnoughHistoryForChart => Metrics.Count >= MinimumChartDays;
}
```

| Member | Requirement | Absence means |
|---|---|---|
| `AsOf` | R22 | — (always present) |
| `IsStravaConnected` | FR-017, FR-018, US5 sc5 | — |
| `IsUnavailable` | edge case "metric calculations fail" | the page shows "Data unavailable", not a crash (R19) |
| `Metrics` | FR-006 | no history at all |
| `Current` | **FR-001 – FR-003** | no history — tiles show "—" (US1 sc2) |
| `CurrentWeek` | FR-004 | no history; a week with no training is present with zero points, not absent |
| `Trend` | FR-005 | fewer than two ISO weeks of history — the indicator shows "—" (US2 sc3) |
| `Recent` | FR-007, FR-008 | no activities |
| `HasEnoughHistoryForChart` | US3 sc2 | fewer than 30 days — the "Not enough data" message |

**`Current` is `Metrics[^1]`, derived rather than stored.** The three headline tiles and the chart's
last point are therefore the same figures by construction and cannot be made to disagree — the same
device `DailyTrainingMetrics.Form` and `WeeklyLoadTrend.AbsoluteChange` already use. SC-002 holds
structurally rather than by maintenance.

`HasActivities` is derived from `Recent` for the same reason: there is no flag that can say "no
activities" while seven of them are listed.

### `DashboardViewBuilder`

```csharp
public static class DashboardViewBuilder
{
    public static DashboardView Build(
        IReadOnlyList<TrainingActivity> activities,
        DateOnly today,
        int maximumHeartRate,
        bool isStravaConnected);
}
```

A pure static function: the same activities, day and maximum always produce an equal view, with no
read of the clock, of storage, or of any ambient state. This is what makes almost every requirement in
this feature testable at the speed of the 204 domain tests (research R11), and it continues the
convention of `TrainingLoadAggregator`, `TrainingMetricsCalculator` and `TrainingLoadTrendCalculator`.

**What it computes, in order:**

| Step | Rule | Source |
|---|---|---|
| 1 | `historyStart` = the earliest activity's local day; `historyEnd` = `max(today, latest activity's day)` | R22 |
| 2 | no activities → an empty view with `IsStravaConnected` carried through | FR-011, US1 sc2 |
| 3 | `daily` = `AggregateDaily(activities, DateRange(historyStart, historyEnd), maximumHeartRate)` | 002 |
| 4 | `chartStart` = `max(historyStart, today − 179 days)`; `Metrics` = `Calculate(daily, DateRange(chartStart, today))` | **FR-006**, chart window |
| 5 | `weekly` = `AggregateWeekly(activities, DateRange(historyStart, historyEnd), maximumHeartRate)`; `CurrentWeek` = the entry whose `IsoWeek` is `IsoWeek.For(today)` | **FR-004** |
| 6 | `Trend` = `TrainingLoadTrendCalculator.Calculate(weekly, DateRange(thisMonday, today))`, **only if** `IsoWeek.For(historyStart).Monday <= thisMonday − 7 days` | **FR-005**, R19 |
| 7 | `Recent` = the 7 activities with the latest `StartedAt`, newest first | **FR-007** |

**Step 6's guard is the one worth reading twice.** `TrainingLoadTrendCalculator` throws when the
history cannot reach the week before the requested range — it refuses to invent a rest week rather than
report a meaningless comparison (004 FR-023). An athlete whose first activity is in the current week
triggers exactly that. The builder therefore *checks the precondition* rather than catching the
exception: "is there a previous week to compare against?" is a question with an answer, not an error
(research R19).

Step 4's window is 180 days inclusive of today, hence `today − 179`.

### `MetricsChart`

The SVG geometry for FR-006, kept out of the component so it can be asserted directly.

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

| Guarantee | Requirement |
|---|---|
| Exactly three series — Fitness, Fatigue, Form | FR-006 |
| Each series has exactly one point per day in `metrics`, in order, none skipped or duplicated | **SC-003** |
| Every coordinate is formatted with `CultureInfo.InvariantCulture` | **research R9** |
| The vertical scale spans the minimum and maximum across all three series, so negative Form is visible | FR-003 — Form is routinely negative |
| `metrics` empty → an empty list, never a malformed `points` attribute | US3 sc2 |

R9 is the reason this is a separate function rather than an expression inside the `.razor` file. On a
`fi-FI` machine the naïve version rendered the points `(0, 45.3)` and `(1.5, 12.25)` as
`points="0,45,3 1,5,12,25"` — silently wrong geometry, no exception. Putting the formatting in one
tested place is what stops it coming back.

---

## 3. Host state

### `SyncStatus`

What the coordinator remembers between circuits.

```csharp
public sealed record SyncStatus
{
    public static readonly SyncStatus Never = new();

    public bool IsRunning { get; init; }
    public SyncResult? Result { get; init; }
    public string? Failure { get; init; }
    public DateTimeOffset? FinishedAt { get; init; }
}
```

| Member | Requirement |
|---|---|
| `IsRunning` | FR-010 loading state; edge case "sync in progress when the page refreshes" |
| `Result` | FR-010 — the counts and the outcome, straight from feature 005 |
| `Failure` | US5 sc5 — `SyncAsync` throws `InvalidOperationException` when no account is connected |
| `FinishedAt` | FR-012 — "last synced at" survives a reload |

`Result` is feature 005's `SyncResult` unchanged. It already carries `Imported`, `Updated`, `Removed`,
`Skipped`, `SeriesOutstanding`, `Outcome` and `RetryAfter`, and it is documented as carrying no
credential in any field or message (005 FR-005, C66) — which is what makes it safe to put on a page.

### `SyncMessage`

```csharp
public static class SyncMessage
{
    public static string For(SyncStatus status);
}
```

A pure function, so every one of US5's five scenarios is a plain xUnit assertion with no rendering and
no Strava:

| Status | Message | Scenario |
|---|---|---|
| `IsRunning` | "Syncing…" | US5 sc1 |
| `Completed`, `Imported > 0` | "N activities imported" | US5 sc2 |
| `Completed`, `Imported == 0` | "Already up to date" | US5 sc3 |
| `RateLimited` | "Rate limited. Available again at {RetryAfter}" | US5 sc4 |
| `Interrupted` | "Sync interrupted. Try again." | US5 sc4 |
| `ReconnectionRequired` | "Strava connection required" + connect link | US5 sc5, FR-018 |
| `Refused` | "A sync is already running." | 005 FR-040 |
| `Failure` set | "Strava connection required" + connect link | US5 sc5 |

### `SyncCoordinator`

```csharp
public sealed class SyncCoordinator   // singleton
{
    public SyncStatus Status { get; }
    public Task<SyncStatus> RunAsync(CancellationToken cancellationToken);
}
```

A singleton holding one `SemaphoreSlim` and the current `SyncStatus`, creating a DI scope per run and
resolving the scoped `StravaActivitySync` inside it.

It is a singleton because the specification requires state that outlives a circuit — a page refresh is
a *new* circuit (edge case), FR-012 wants the state restored on reload, and FR-010's counts must
survive long enough to be read. Restoring feature 005's FR-040 guarantee is a by-product: the probe in
research R13 showed that `StravaActivitySync` cannot be registered as a singleton at all, and that as a
scoped service two circuits get two semaphores.

### `AthleteSettings`

```csharp
public sealed record AthleteSettings(int MaximumHeartRate);
```

Bound from `Athlete:MaximumHeartRate` at startup. **FR-015**: startup fails when the value is missing
or not positive, so `CalculateTrainingLoad`'s own `ArgumentOutOfRangeException` for a non-positive
maximum is unreachable in the running application (research R4, R19).

---

## 4. Types deliberately *not* created

| Not created | Why | Revisit trigger |
|---|---|---|
| Any EF entity, table or migration | every figure is derived on read; nothing about the dashboard is durable | a requirement to record what was displayed |
| A cached or precomputed metrics table | measured 292–677 ms against SC-001's 2 seconds, on a history far heavier than real (R16) | a measured load over 2 seconds |
| `IDashboardReader` / `IDashboardService` | one implementation, one caller; the arithmetic is already a pure function that needs no substitution (R11) | a second reader |
| `IActivitySource` in the domain | revisited from 005 R2 and declined — one provider, and both sides would sit on the same side of the boundary (R3) | a second activity provider |
| `AthleteProfile` entity | 002 declined it once; configuration carries one value for one athlete (R4) | a second athlete, or editing without a restart |
| A `TrendDirection` / `TrendArrow` enum in Web | `TrendClassification` already has exactly the four states, and a parallel enum is where the two rules would drift apart (R5) | — |
| A DTO mirroring `SyncResult` | it is already free of credentials and of Strava DTOs, and copying it adds a mapping to keep correct | the WebAssembly refactoring, which needs a wire shape |
| A chart-point record (`(double X, double Y)`) | `ChartSeries` carries the formatted attribute; an intermediate record would be visible only to `MetricsChart` | a second consumer of the geometry |
| An `ISyncCoordinator` | one implementation, and its tests drive the real one against a stubbed Strava (R17, R18) | — |
| A state-change event on the coordinator | a snapshot read satisfies the reload edge case; the button's own `await` covers live feedback (R13) | a requirement for live update after reload |
| `TrainingLoadAnalyzer.Application` | none of project plan 22.2's three conditions moved (R2) | 22.2's conditions |

---

## 5. Requirement trace

| Requirement | Where it lives |
|---|---|
| FR-001, FR-002, FR-003 | `DashboardView.Current` → `DailyTrainingMetrics.Fitness` / `.Fatigue` / `.Form` |
| FR-004 | `DashboardView.CurrentWeek` → `WeeklyTrainingLoad.Points` |
| FR-005, FR-005a | `DashboardView.Trend` → `WeeklyLoadTrend.AbsoluteChange`, `.RelativeChange`, `.Classification` |
| FR-006 | `DashboardView.Metrics` + `MetricsChart.Plot` |
| FR-007 | `DashboardView.Recent` |
| FR-008 | `RecentActivity.Load.Provenance` |
| FR-009 | `SyncCoordinator.RunAsync` |
| FR-010 | `SyncStatus` + `SyncMessage.For` |
| FR-011 | `DashboardView.HasActivities` = false, with `IsStravaConnected` deciding the guidance shown |
| FR-012 | `SyncCoordinator` singleton — state outlives the circuit |
| FR-013 | `DashboardReader` reads only `ImportDbContext`; nothing else makes a network call |
| FR-014, FR-015 | `AthleteSettings`, validated at startup |
| FR-016 | the two connect endpoints — see [contracts/web-contract.md](./contracts/web-contract.md) |
| FR-017 | `DashboardView.IsStravaConnected` = false |
| FR-018 | `SyncStatus.Result.Outcome == ReconnectionRequired` → `SyncMessage` |
| SC-001 | measured in research R16; no design element needed |
| SC-002 | `Current` derived from `Metrics[^1]`; fixed-culture formatting (R9) |
| SC-003 | `MetricsChart.Plot`'s one-point-per-day guarantee |
| SC-004 | `DashboardView.Recent` ordering |
| SC-005 | `SyncCoordinator.RunAsync` — bound applies to an incremental sync (R7) |
| SC-006 | `DashboardReader` touches no network; `IsUnavailable` and `SyncStatus.Failure` carry the messaging |
| SC-007 | no JavaScript beyond Blazor's own; no charting library (R8) |
