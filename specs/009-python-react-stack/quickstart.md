# Quickstart: Validating the alternative stack

**Feature**: 009-python-react-stack | **Date**: 2026-09-23

This is a run-and-verify guide. It proves the feature end to end. It does not describe the
implementation: see [plan.md](./plan.md), [data-model.md](./data-model.md) and the
[contracts](./contracts/).

## Prerequisites

- **uv**, with Python 3.13 available (`uv python install 3.13` if it is not).
- **Node 22+ and npm.**
- **The .NET SDK** pinned by the repository. It is needed only to regenerate the parity goldens
  and to run the reference side by side.
- **Strava.** For a live connection, the Strava API application already configured for the
  reference (callback domain `localhost`).

## 1. Configure

Create `alt-stack/backend/.env`. It is git-ignored by the existing `.env` rule.

```dotenv
TLA_ATHLETE_MAXIMUM_HEART_RATE=190
TLA_STRAVA_CLIENT_ID=<client id>
TLA_STRAVA_CLIENT_SECRET=<client secret>
# TLA_DATABASE_PATH=training-load.db   (default; never the reference's file)
```

## 2. Run the suites

```bash
cd alt-stack/backend  && uv sync && uv run pytest
cd alt-stack/frontend && npm ci && npm test
```

**Expected:** both pass. The backend suite includes:
- the domain tests;
- the Strava integration tests, against `httpx.MockTransport`;
- the `TestClient` tests of every route in [http-api.md](./contracts/http-api.md);
- the parity tests, which read `parity/golden/`.

The frontend suite includes:
- the component tests;
- the geometry parity tests;
- the ported theme tests: contrast, colour discipline, responsive rules, interactive controls.

**Locale check (SC-007):** run both suites a second time under a comma-decimal locale. The results
must be identical.

```bash
LANG=fi_FI.UTF-8 LC_ALL=fi_FI.UTF-8 uv run pytest
LANG=fi_FI.UTF-8 LC_ALL=fi_FI.UTF-8 npm test
```

**Reference still green (009 FR-003):** from the repository root, run `dotnet test`.

## 3. Startup refusal (006 FR-015)

```bash
cd alt-stack/backend
env -u TLA_ATHLETE_MAXIMUM_HEART_RATE uv run uvicorn tla.main:app --factory --workers 1
```

**Expected:** a non-zero exit with the "is not configured" message from
[http-api.md §5](./contracts/http-api.md#5-startup-refusal), before anything binds a port.

## 4. Run the application

**Development mode** runs two processes. Open <http://localhost:5173>.

```bash
cd alt-stack/backend  && uv run --env-file .env uvicorn tla.main:app --factory --workers 1 --port 8000
cd alt-stack/frontend && npm run dev
```

**Run mode** uses one process and one port. Open <http://localhost:8000>.

```bash
cd alt-stack/frontend && npm run build
cd alt-stack/backend  && uv run --env-file .env uvicorn tla.main:app --factory --workers 1 --port 8000
```

`--workers 1` is required: the sync guard and the last-sync status live in the process (research
R5).

## 5. Walk the athlete journeys

| # | Do | Expect |
| --- | --- | --- |
| 1 | Open the page with an empty store | "No activities recorded", "Connect your Strava account to bring your training in.", and a **Connect Strava** link. The rail shows today, `ISO week YYYY-Www`, `190 bpm` and "from configuration". |
| 2 | Connect Strava, approving every scope | Back on `/`. The empty state now reads "Your Strava account is connected, but nothing has been imported yet. Sync to bring your training in." |
| 3 | Connect again, but untick "View data about your private activities" | Back on `/?connect=scope`, and the page renders normally. That no explanation appears is the recorded gap (research R3(3)). |
| 4 | Press **Sync Activities** | The button is immediately disabled and reads "Syncing…", and the message reads "Syncing activities…". On a multi-year history, the first sync ends with "Rate limited by Strava. Available again at HH:mm." (005 FR-035). |
| 5 | While 4 runs, open a second tab | The second tab shows the running state. Pressing sync there changes nothing. |
| 6 | Sync again after the retry time, until it is up to date | "{n} activities imported.", then "Already up to date." The figures, chart and recent list update without a reload, and "Last checked" shows the time of the last attempt. |
| 7 | Wait more than 6 hours after connecting, then sync | The sync completes, because the token is renewed (research R10), rather than asking to reconnect. |
| 8 | Switch the window to 30, then 90, then 180 | The heading, lines, bars and axis change with no network request (check the devtools Network tab). |
| 9 | Point at the chart | The readout shows the date, Fitness, Fatigue, Form and Load, with no network request per movement. |
| 10 | Stop the backend, then reload or sync | "Data unavailable" with the reference paragraph. No zeros appear (009 FR-016). |
| 11 | Open `/nowhere` | "Not Found" and "Sorry, the content you are looking for does not exist.", in the broadsheet layout. |

## 6. Visual and accessibility pass (SC-006)

Check each surface in §5 in both of the following ways:
- **Viewport widths:** in devtools device mode at 320, 768, 1280 and 2560 px, and at 200 % zoom.
- **Appearances:** with the OS in light, then dark, switching while the page is open.

**Expected:** 008 SC-004 – SC-010 hold:
- no horizontal scroll;
- the rail stacks below the tablet breakpoint;
- visible focus on every control when tabbing;
- 48 × 48 px targets;
- the appearance follows the OS setting without a reload.

## 7. Side by side with the reference (sanity check only)

Run the reference (`dotnet run --project src/TrainingLoadAnalyzer.Web`), with its own store, next
to the new implementation, and compare the two dashboards by eye. Small differences are
**expected**: the two stores were synced at different times, so the 180-day measured window
differs (009 FR-022a). The parity test is §2, not this.

## 8. Regenerate the goldens (only after a deliberate reference change)

```bash
dotnet run --project parity/generator
git diff parity/golden
```

**Expected:** only the changes the reference change explains. Review them before committing.
