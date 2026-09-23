# Training Load Analyzer: alternative stack (feature 009)

The same application as the .NET reference in `src/`, rebuilt as a Python/FastAPI backend and a
React/TypeScript frontend so the two stacks can be compared like for like
([spec](../specs/009-python-react-stack/spec.md),
[plan](../specs/009-python-react-stack/plan.md),
[parity report](../specs/009-python-react-stack/parity-report.md)).

It lives next to the reference and shares nothing with it at runtime: not the code, not the
database, not the process (009 FR-003, FR-004). Parity is proved against golden files the
reference generates itself, in [`../parity/`](../parity/README.md).

## Prerequisites

- [uv](https://docs.astral.sh/uv/), with Python 3.13 (`uv python install 3.13` if needed)
- Node 22 or later, and npm
- The .NET SDK, only to regenerate the parity goldens or to run the reference

## Configure

Create `alt-stack/backend/.env`. **Never commit it**: the repository's `.env` rule keeps it
git-ignored.

```dotenv
TLA_ATHLETE_MAXIMUM_HEART_RATE=190
TLA_STRAVA_CLIENT_ID=<client id>
TLA_STRAVA_CLIENT_SECRET=<client secret>
# TLA_DATABASE_PATH=training-load.db   (the default, in alt-stack/backend)
```

| Variable | Required | Notes |
| --- | --- | --- |
| `TLA_ATHLETE_MAXIMUM_HEART_RATE` | yes | A positive whole number. Without it the server refuses to start, before it binds a port. |
| `TLA_STRAVA_CLIENT_ID`, `TLA_STRAVA_CLIENT_SECRET` | to connect Strava | The Strava application already set up for the reference, with callback domain `localhost` |
| `TLA_DATABASE_PATH` | no | Defaults to `alt-stack/backend/training-load.db` (git-ignored by the `*.db` rule) |

**Do not point `TLA_DATABASE_PATH` at the reference's database.** This implementation has its own
schema and its own file (009 FR-022).

## Run

The server must run with **exactly one worker** (`--workers 1`): the one-sync-at-a-time guard and
the last sync status live in the server process, as they do in the reference's singleton
coordinator (research R5). More workers would allow two syncs at once and lose the status.

**Development**: two processes. Open <http://localhost:5173>.

```bash
cd alt-stack/backend  && uv sync && uv run --env-file .env uvicorn tla.main:app --factory --workers 1 --port 8000
cd alt-stack/frontend && npm ci && npm run dev
```

Vite proxies `/api`, `/connect` and `/strava/callback` to the backend and keeps the `Host` header,
so the Strava callback returns to `:5173` and the state cookie is sent back (research R15).

**Run mode**: one process. Open <http://localhost:8000>.

```bash
cd alt-stack/frontend && npm run build
cd alt-stack/backend  && uv run --env-file .env uvicorn tla.main:app --factory --workers 1 --port 8000
```

## Test

```bash
cd alt-stack/backend  && uv run pytest
cd alt-stack/frontend && npm test
```

Nothing may depend on the machine's locale (009 SC-007). Run both suites again under a
comma-decimal locale; the results must be identical:

```bash
cd alt-stack/backend  && LANG=fi_FI.UTF-8 LC_ALL=fi_FI.UTF-8 uv run pytest
cd alt-stack/frontend && npm run test:fi
```

The backend's locale test sets `fi_FI.UTF-8` itself, because Python ignores the environment until
`locale.setlocale` is called. It is skipped, with the reason shown, if that locale is not installed.

## Where things are

```text
alt-stack/
├── backend/src/tla/
│   ├── domain/        # loads, aggregation, metrics, trends: pure, no I/O, no Strava
│   ├── strava/        # Strava's shapes, the API and OAuth clients, the mapper
│   ├── persistence/   # SQLite schema and stores (stdlib sqlite3)
│   ├── sync/          # the sync walk, authorization and renewal, the coordinator
│   ├── dashboard/     # the view builder, the reader, display formatting
│   ├── api/           # FastAPI routes and the SPA fallback
│   └── main.py        # the app factory
└── frontend/src/
    ├── components/    # the Broadsheet page, ported from the reference's .razor files
    ├── chart/         # the chart geometry, ported from MetricsChart.cs
    └── theme/         # the reference's stylesheet, copied with a provenance header
```

The parity fixtures and goldens are in [`../parity/`](../parity/README.md). To regenerate the
goldens after a deliberate change to the reference, run `dotnet run --project parity/generator`.
