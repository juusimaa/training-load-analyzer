# Quickstart: Dashboard

**Feature**: 006-dashboard | **Date**: 2026-09-18 | **Plan**: [plan.md](./plan.md)

How to scaffold this feature, run it, and check that it does what
[spec.md](./spec.md) says. Shapes and signatures are in
[data-model.md](./data-model.md) and
[contracts/web-contract.md](./contracts/web-contract.md); this file is the run-and-verify guide.

**Baseline before any work starts**: `dotnet test` → **309 passing** (204 domain, 105 infrastructure),
0 failed. Every check below assumes that number only grows.

---

## 1. Scaffolding

One non-TDD task, done once and named as such (see the Principle I note in
[plan.md](./plan.md#constitution-check)). Every task after it starts RED.

```bash
cd /Users/jouniuusimaa/Code/training-load-analyzer

# The host. Interactive Server (research R1), no sample pages, no HTTPS redirect for local use.
dotnet new blazor -n TrainingLoadAnalyzer.Web -o src/TrainingLoadAnalyzer.Web \
    -int Server -e --no-https
dotnet add src/TrainingLoadAnalyzer.Web \
    reference src/TrainingLoadAnalyzer.Infrastructure

# Its test project.
dotnet new xunit3 -n TrainingLoadAnalyzer.Web.Tests -o tests/TrainingLoadAnalyzer.Web.Tests
dotnet add tests/TrainingLoadAnalyzer.Web.Tests reference src/TrainingLoadAnalyzer.Web
dotnet add tests/TrainingLoadAnalyzer.Web.Tests package bunit --version 2.11.3
dotnet add tests/TrainingLoadAnalyzer.Web.Tests package Microsoft.AspNetCore.Mvc.Testing

dotnet sln add src/TrainingLoadAnalyzer.Web tests/TrainingLoadAnalyzer.Web.Tests
```

**Three edits the templates do not make**, each one a probe finding (research R10):

1. `tests/TrainingLoadAnalyzer.Web.Tests.csproj` must use **`Microsoft.NET.Sdk.Razor`**, not
   `Microsoft.NET.Sdk`, or `.razor` components cannot be rendered.
2. **Delete** the `<Content Include="xunit.runner.json" …/>` item the other two test projects have.
   The Razor SDK includes content by default and the duplicate fails the build with `NETSDK1022`.
3. Test classes derive from **`BunitContext`**, not `TestContext` — bUnit's `TestContext` is ambiguous
   with `Xunit.TestContext` under xUnit v3 (`CS0104`). Every bUnit 1.x tutorial shows the old name.

Verify the scaffold before writing a test:

```bash
dotnet build && dotnet test   # still 309, plus whatever the templates generated
```

---

## 2. Configuration

Four keys ([contracts §2](./contracts/web-contract.md#2-configuration)). The secret and the maximum
heart rate go in user secrets, never in a tracked file.

```bash
cd src/TrainingLoadAnalyzer.Web
dotnet user-secrets init
dotnet user-secrets set "Athlete:MaximumHeartRate" "190"
dotnet user-secrets set "Strava:ClientId"     "<your client id>"
dotnet user-secrets set "Strava:ClientSecret" "<your client secret>"
```

`ConnectionStrings:Import` belongs in `appsettings.Development.json` — it names a local file and holds
no secret.

At <https://www.strava.com/settings/api>, the application's **Authorization Callback Domain** must be
`localhost`.

**Check FR-015 — the application refuses to start without a maximum heart rate:**

```bash
dotnet user-secrets remove "Athlete:MaximumHeartRate"
dotnet run --project src/TrainingLoadAnalyzer.Web    # must fail at startup, naming the key
dotnet user-secrets set "Athlete:MaximumHeartRate" "190"
```

A start that succeeds here is a defect: the dashboard would show TRIMP figures computed from a default
nobody chose.

---

## 3. Running it

```bash
dotnet run --project src/TrainingLoadAnalyzer.Web
```

Open the printed URL.

| Step | Expected | Requirement |
|---|---|---|
| First load, nothing connected | empty state, a link to connect | FR-011, **FR-017** |
| Follow the link | Strava's consent screen | **FR-016** |
| Approve | back at `/`, connected, still no activities | FR-016 |
| Click **Sync Activities** | a loading state, then a count | FR-009, **FR-010** |
| First sync of a multi-year history | it **will** stop at Strava's rate limit and report when to retry — this is correct (005 R24, spec SC-005) | FR-010 |
| Click **Sync Activities** again after the retry time | it resumes rather than restarting | 005 FR-027 |
| Reload the page | the same figures, and the last sync's outcome still shown | **FR-012** |

---

## 4. Verifying the user stories

Each is independently runnable, in priority order. Automated coverage is named alongside so the
manual pass is a confirmation rather than the only evidence.

### US1 — current training status (P1)

- **Automated**: `DashboardViewBuilderTests` — a hand-built history whose CTL/ATL/TSB are already
  asserted by feature 003's tests, checked through the view; plus the no-history case.
- **Manual**: three tiles show Fitness, Fatigue and Form to one decimal.
- **Check SC-002 by eye**: the Fitness tile and the chart's last Fitness point are the same number. They
  are the same object (contract C80), so a disagreement means the derivation was replaced by a stored
  field.
- **Check US1 sc2**: with an empty database the tiles read `—`, not `0.0`.

### US2 — weekly load and trend (P1)

- **Automated**: current-week total; a history of exactly one ISO week leaving `Trend` null; the
  classification matching what `TrainingLoadTrendCalculator` returns for the same weeks.
- **Manual**: the weekly figure equals the sum of this week's activity loads in the recent list — as far
  as the list reaches.
- **Check FR-005a**: on any day but Sunday, the *classification* reads as in-progress while the change
  figures are still shown (research R14). A "significant increase" badge on a Tuesday is a defect.
- **Check FR-005**: no threshold number appears anywhere in `TrainingLoadAnalyzer.Web`:

  ```bash
  grep -rn "0\.15\|0\.05\|\b50m\b" src/TrainingLoadAnalyzer.Web --include=*.cs --include=*.razor
  ```

  Must print nothing. The thresholds are feature 004's and live in the domain.

### US3 — the time-series chart (P2)

- **Automated**: `MetricsChartTests` — point count equals day count (**SC-003**); three series; an empty
  input yields no malformed attribute; negative Form is inside the plotted range.
- **The culture check, which is the one that matters** (research R9):

  ```bash
  DOTNET_SYSTEM_GLOBALIZATION_PREDEFINED_CULTURES_ONLY=false \
  LANG=fi_FI.UTF-8 dotnet test --project tests/TrainingLoadAnalyzer.Web.Tests
  ```

  The suite must pass identically. A `points` attribute built without `InvariantCulture` renders
  `points="0,45,3 1,5,12,25"` on a `fi-FI` machine — valid-looking markup, silently wrong geometry,
  no exception. This is the developer's own locale.

- **Manual**: hover or read the legend; confirm three distinguishable lines and that Form dips below
  the axis after a heavy block.
- **Check US3 sc2**: with under 30 days of history, the "Not enough data to show trends (30+ days
  required)" message appears instead of a two-point line.

### US4 — recent activities (P2)

- **Automated**: `RecentActivityListTests` (bUnit) — seven rows, newest first, measured and estimated
  distinguishable in the markup; fewer than seven activities shows all of them.
- **Manual**: the top row is the most recent session; its date, type, moving time and load match Strava.
- **Check FR-008**: a ride with heart-rate data and one without are visibly different. Older than 180
  days, every activity is estimated — that is feature 005's measured window, not a defect.

### US5 — manual sync (P3)

- **Automated**: `SyncMessageTests` covers all five scenarios as pure assertions over `SyncStatus`;
  `SyncCoordinatorTests` covers the one-at-a-time guard and the no-connection failure against
  `StubHttpMessageHandler` and `SqliteFixture`, both of which feature 005 already wrote.
- **Manual**: click sync with the network disabled — an interrupted message, not a crashed page.
- **Check the refresh edge case**: start a sync of a large history, reload the page while it runs. The
  reloaded page must say a sync is running. A fresh "ready to sync" state means the status is living in
  the circuit instead of the singleton (contract C93).
- **Check FR-040's restored guarantee**: open two browser tabs and click sync in both. The second must
  report that one is already running, not start a second walk (contract C94, research R13).

---

## 5. Constitution checks

Run these before the completion review. They are commands, not intentions.

**Principle II — the domain is untouched, and so is the integration layer:**

```bash
git diff --stat main -- src/TrainingLoadAnalyzer.Domain src/TrainingLoadAnalyzer.Infrastructure
```

Must print nothing. This feature adds no member to either project
([contracts §intro](./contracts/web-contract.md)).

**Principle II — no Strava vocabulary in the read model:**

```bash
grep -rni "strava" src/TrainingLoadAnalyzer.Web/Features/Dashboard
```

Must print nothing. `Features/Sync/` and the connect endpoints are allowed to name Strava; the
dashboard's figures are not.

**Principle II — the domain still has no dependencies:**

```bash
grep -c "PackageReference\|ProjectReference" src/TrainingLoadAnalyzer.Domain/*.csproj   # 0
```

**Contract C102 — components do not reach past the reader:**

```bash
grep -rn "ImportDbContext\|ActivityStore\|StravaActivitySync\|TimeProvider" \
    src/TrainingLoadAnalyzer.Web/Components
```

Must print nothing.

**Contract C103 — components compute no training figures:**

```bash
grep -rn "TrainingLoadAggregator\|TrainingMetricsCalculator\|TrainingLoadTrendCalculator" \
    src/TrainingLoadAnalyzer.Web/Components
```

Must print nothing. All three belong in `DashboardViewBuilder`.

**Contract C76 / C78 — no credential is committed or rendered:**

```bash
grep -rni "client_secret\|AccessToken\|RefreshToken" \
    src/TrainingLoadAnalyzer.Web/Components src/TrainingLoadAnalyzer.Web/appsettings*.json
git check-ignore src/TrainingLoadAnalyzer.Web/*.db && echo "db ignored"
```

**Principle I — the whole suite:**

```bash
dotnet test        # 309 + this feature's, 0 failed
```

**SC-001 — the two-second budget**, measured rather than assumed. Research R16 measured 292–677 ms in
process for a 3-year-9-month, 953-activity history. Browser timing should land in the same order:

```bash
curl -o /dev/null -s -w "%{time_total}\n" http://localhost:5000/
```

Repeat three times and take the warm figure. Above 2 seconds reopens R16; a projection does not.

---

## 6. What a reviewer should look at first

1. **Is `DashboardViewBuilder` pure?** If it reads a clock or a database, the arithmetic is no longer
   testable at domain speed and contract C79 is broken.
2. **Is `Current` still derived?** A stored `Fitness` field on `DashboardView` would let the tiles and
   the chart disagree, which is exactly what SC-002 forbids.
3. **Is there any threshold number in `Web`?** That is feature 004's rule leaking into the UI (R5).
4. **Is `SyncCoordinator` registered as a singleton?** As anything else, the page-refresh edge case and
   the two-tab case both fail silently (R13).
5. **Is every coordinate `InvariantCulture`?** The failure mode is an invisible one on an `en-US`
   machine (R9).
