# Research: Re-implementation on an Alternative Stack

**Feature**: 009-python-react-stack | **Date**: 2026-09-23 | **Plan**: [plan.md](./plan.md)

Each entry has the form **Decision / Rationale / Alternatives considered**. Entries R1–R4 found
places where "the same as the reference" cannot be taken literally. Each needs an amendment to
the specification (Principle VII). The developer approved all of them on 2026-09-23, and they are
now spec
[Amendment 1](./spec.md#amendment-1--precision-reference-deviations-and-stack-specific-surfaces-2026-09-23).
The summary at the end of this file is kept as the record of what was proposed.

Findings marked **(probed)** were checked by running code on this machine: a throwaway .NET 10
console program and Python 3.14, in the session scratchpad. They were not inferred from
documentation.

---

## R1. Numeric parity: `decimal` loads

**Finding (probed).** The reference computes loads, daily and weekly totals and trend changes in
.NET `decimal`, a 96-bit integer coefficient with a scale of 0–28. Division rounds to fit:
`1m/60m = 0.0166666666666666666666666667`. Addition can overflow the coefficient and silently
drop a digit: `(1m/60m) * 3600m = 60.000000000000000000000000120`. Python's `decimal.Decimal`
at any fixed precision rounds in different places, so a one-second heart-rate gap (1/60 of a
minute) produces values that differ from the reference in about the 27th significant digit.

**Decision.** Use `decimal.Decimal` in a dedicated context: precision 34 (IEEE decimal128),
`ROUND_HALF_EVEN`, set once in the domain package and used by every load computation. Treat load
parity as equality to within **1e-20 points**, not bit-for-bit (Amendment 1(a)).

**Rationale.** 1e-20 is about 18 orders of magnitude below the 0.1-point display resolution and
below double precision. The metrics convert the daily load to `double` before smoothing, so
every Fitness, Fatigue and Form figure is identical either way, and so is every displayed
figure, except at an exact display tie (R2). Hand-computed expectations from the reference's
tests (60-second gaps, whole minutes) remain exact in both stacks.

**Alternatives considered.**
- *Emulate .NET `decimal` in Python* (96-bit coefficient, scale ≤ 28, re-rounded after every
  operation). Rejected. It makes the domain mirror another runtime's internals rather than the
  specification, and the tests would describe .NET's arithmetic rather than the behaviour 001 and
  002 specify (Principle I).
- *`fractions.Fraction` (exact rationals).* This is more exact than the reference, which satisfies
  002 FR-022 better than .NET does. Rejected because exact rationals grow without bound over a
  multi-year history of one-second samples, and because they still differ from the reference at
  the 28th digit, so parity would need a tolerance anyway.

## R2. Numeric parity: display formatting

**Finding (probed).** `double.ToString("0.0", InvariantCulture)` in .NET 10 first formats the
double to **15 significant digits**, then rounds **half away from zero**. Python's `f"{x:.1f}"`
rounds the exact binary value half to even. The two disagree on 5 of 13 probe values:

| value | .NET | Python `:.1f` | Python emulation |
| --- | --- | --- | --- |
| 45.25 | 45.3 | 45.2 | 45.3 |
| 0.15 | 0.2 | 0.1 | 0.2 |
| 12.349999999999999 | 12.4 | 12.3 | 12.4 |
| −0.25 | −0.3 | −0.2 | −0.3 |
| −13.25 | −13.3 | −13.2 | −13.3 |

Three more details apply (probed):
- A negative double that rounds to zero keeps its sign: `(-0.04).ToString("0.0")` gives `-0.0`.
  A negative **`decimal`** does not: `(-0.04m).ToString("0.0")` gives `0.0`. The metric figures
  are doubles and the load figures are decimals, so both behaviours appear on the page.
- The percentage format `+0;-0;0` rounds half away from zero, and it uses the zero section when
  the value rounds to zero: −0.4 % gives `0%`, 0.5 % gives `+1%`.
- The week-change caption prefixes `+` when the change is `>= 0`, then formats it as a decimal.
  So −0.04 renders as `0.0`, with no sign at all.

**Decision.** Port `Display` as a pure Python module with four functions:
- **`metric(float | None)`**: format to 15 significant digits (`format(x, ".15g")`), then
  `Decimal.quantize(Decimal("0.1"), ROUND_HALF_UP)`, keeping the sign.
- **`points(Decimal | None)`**: `quantize(…, ROUND_HALF_UP)` on the exact decimal, with the
  sign of a result equal to zero dropped.
- **`percent`**: the proportion ×100, `quantize(Decimal("1"), ROUND_HALF_UP)`, a sign when
  non-zero, then `%`.
- **`duration` and `day`**: as the reference.

All formatting happens in the backend, and the API carries finished strings (see R7). The emulation
matched .NET on every probe value. Its tests pin the table above.

**Rationale.** The display is where parity is visible (SC-002). One pure formatting module in
one language, tested against values the reference was actually run on, is the smallest thing that
closes the gap. Formatting in TypeScript too would need a third rounding emulation, on top of
JavaScript's own `toFixed` quirks.

**Alternatives considered.** Python's native formatting (rejected by the table above).
Formatting in the frontend (rejected: a second implementation of the same rules).

**Residual.** A display tie that R1's arithmetic places on opposite sides in the two stacks. This
needs an exact `.x5` true value where .NET's own rounding error is negative, and it is accepted
under Amendment 1(a).

## R3. Where the reference departs from its own specification

The spec's Overview says parity is with features 001–008 *as specified*, and that the reference
decides only what those features leave open. Reading the reference found three places where it
does not do what its specification says:

1. **Token renewal is never used.** `StravaActivitySync` sends the stored access token as it is.
   `StravaAuthorization.RefreshAsync` exists and is tested (005 FR-004), but nothing in `src/`
   calls it. Strava access tokens last six hours, so a sync more than six hours after connecting
   gets a 401, ends `ReconnectionRequired`, and the athlete is shown "Strava connection required."
2. **The in-flight sync state never reaches the tab that clicked.** This comes from reading the
   code; no test covers it either way. `Dashboard.razor` assigns `syncStatus` only when
   `SyncCoordinator.RunAsync` returns. The clicking tab therefore keeps showing an enabled "Sync
   Activities" button until the sync ends, and "Syncing…" appears only in tabs opened afterwards.
   006 US5 scenario 1 specifies a loading indicator, and 009 US3 scenario 4 specifies a button that
   cannot be pressed while a sync runs.
3. **Connect outcomes are not explained.** `/strava/callback` redirects to
   `/?connect=declined|scope|mismatch`, and nothing reads that parameter. 005 FR-002a says the
   athlete MUST be told what to approve. Feature 008 already recorded this as "a recorded gap, not
   a preserved message" (`InformationPreservationTests`).

**Decision (Amendment 1(b)).** The specification wins wherever meeting it needs **no new
athlete-facing wording**. The new implementation renews the token before a sync (1), and shows
the existing "Syncing…" / "Syncing activities…" state in the clicking tab (2). Where meeting the
specification would need wording the reference never defined (3), the reference's behaviour
stands. That follows the precedent feature 008 set for this exact gap. Every deviation is listed
in the parity report (plan, Phase 2 "Parity reference").

**Rationale.** Copying (1) on purpose would ship a known defect that locks the athlete out every
six hours, in a feature whose Overview says the specification governs. (2) uses strings that
already exist. (3) would need new content, which Principle VII sends back to a specification.

**Alternatives considered.**
- *Copy the reference exactly, bugs included.* This maximises side-by-side sameness, but
  contradicts the spec's own definition of parity.
- *Fix all three.* (3) would invent wording.

**Follow-up (outside this feature).** (1) and (2) are defects in the reference. Run
`/speckit-bug-assess` on each against `main`, so the two stacks converge.

## R4. Surfaces that exist only because of the reference's stack

| Surface in the reference | Why it exists | Decision (Amendment 1(c)) |
| --- | --- | --- |
| `ReconnectModal` ("Rejoining the server…", "Rejoin failed… trying again in N seconds.", "Failed to rejoin…", "The session has been paused by the server.", Retry/Resume) | A Blazor Server circuit dropped its connection | **Not ported.** There is no circuit. A failed request to the application is covered by 009 FR-016's unavailable notice. |
| `#blazor-error-ui` ("An unhandled error has occurred." · "Reload" · "🗙") | An unhandled exception on the circuit | **Ported with the same wording** as the React error boundary's fallback. |
| `/Error` page: "Error." / "An error occurred while processing your request." / "Request ID: …" | ASP.NET exception-handler page | **Ported with the same wording.** The request ID is the backend's request identifier. |
| `/Error` page "Development Mode" paragraph, naming the `ASPNETCORE_ENVIRONMENT` variable | ASP.NET's template text | **Not ported.** It names another stack's configuration, and copying it would be false. No replacement text is added. |
| Rail max heart rate showing **`0 bpm`** while the history loads | `Dashboard.razor` falls back to `0` when `view` is null | **Render `—` instead.** 008's edge-case list asks for "a placeholder rather than an empty line or a zero". A client that fetches its data shows the loading state for longer than a prerendered circuit does, so the `0` would become visible. |

## R5. Backend framework and runtime

**Decision.** Python 3.13, managed with **uv**. FastAPI, served by **Uvicorn with one worker**.
Endpoints are **synchronous** (`def`, which FastAPI runs on its threadpool), use `httpx.Client`
for Strava, and use stdlib `sqlite3`.

**Rationale.** The constitution fixes Python/FastAPI. Python 3.13 is the newest release on this
machine whose ecosystem is settled (3.14 is also installed and is the fallback). Synchronous code
matches the reference's control flow line for line: one sync walk, one database connection per
unit of work, blocking I/O. That keeps the port reviewable, which is the point of the comparison.
One worker is **required**, not just convenient: the sync guard and the last-sync status live in
process memory, as they do in the reference's singleton `SyncCoordinator` (006 FR-012a). Running
several workers would break both, so the run commands pin `--workers 1`.

**Alternatives considered.** Async endpoints with `httpx.AsyncClient` and `aiosqlite`: this adds
a dependency and an event-loop discipline for no benefit at one athlete and one sync at a time.
Gunicorn: rejected, because multiple workers break the guard.

## R6. Storage

**Decision.** Stdlib `sqlite3`, with the schema written as SQL in the persistence module and
versioned with `PRAGMA user_version` through an ordered list of migration scripts. There is no
ORM. The file is the new implementation's own, at `TLA_DATABASE_PATH` (default
`alt-stack/backend/training-load.db`, git-ignored by the existing `*.db` rule), and never the
reference's (009 FR-022).

**Rationale.** Three tables, four queries (upsert by identity, range by start, latest start, the
earliest outstanding series inside the measured window). An ORM would be the largest dependency
in the backend for the smallest part of it (Principle III). Instants are stored as **integer UTC
microseconds** plus an **offset in minutes**, so range queries and ordering run in SQL. This is
the same reasoning as the reference's ticks column (005 research R5), at Python's own resolution.

**Alternatives considered.** SQLAlchemy Core or SQLModel: rejected as above. Storing ISO-8601
text: rejected, because ordering and range queries would depend on string formatting.

## R7. Where the dashboard's view is built, and what crosses the wire

**Decision.** The backend builds the complete `DashboardView`: the 180-day metrics and daily-load
series, the current week and its trend, the recent seven, and the rail's values. It sends it as
JSON **with every displayed string already formatted** (R2). Each day also carries its raw
`fitness`, `fatigue`, `form` and `load` as numbers, for **chart geometry only**. The frontend:
- slices the trailing 30, 90 or 180 days for the selected window, with no request (008 FR-005,
  research R6);
- computes SVG geometry from the raw numbers with a port of `MetricsChart`;
- renders strings as they arrive.

**Rationale.** It follows the reference's split: `DashboardViewBuilder` and `Display` are pure
server code, and the components only lay out what they are given. Geometry has to be client-side,
because it depends on the chosen window (the metric band is taken over the windowed series), and
switching windows must not reload (009 US3 scenario 2). Sending finished strings means rounding
is implemented once. The hover readout needs no request, because every value it shows is already
in the payload (009 FR-015).

**Alternatives considered.**
- *Backend sends geometry for all three windows.* About three times the payload, and it moves a
  presentation concern server-side for no gain.
- *Frontend formats numbers.* Rejected in R2.

## R8. Chart geometry parity

**Finding (probed).** The reference's `Number()` is `Math.Round(value, 2)`, rounding half to
even, followed by the shortest round-trip string. .NET's `Math.Round(2.675, 2)` gives `2.68`,
where the correctly rounded answer, and Python's, is `2.67`. So coordinates can differ by 0.01.

**Decision.** The TypeScript geometry port rounds with `Math.round(v * 100) / 100` and prints
with `String()`. Geometry parity is **numeric, within 0.01 view-box units** (a 1000 × 300 plot):
the parity test parses each coordinate list and compares element by element (see
[contracts/parity.md](./contracts/parity.md)). This is presentation geometry, not a figure, so it
is not a computation under 009 FR-002, and no amendment is needed.

**Alternatives considered.** Emulating `Math.Round`'s scaling algorithm in TypeScript: rejected,
since a 0.01-unit difference in a 1000-unit view box is invisible.

## R9. Sync over HTTP

**Decision.**
- `POST /api/sync` runs the sync **inside the request** and returns the final `SyncStatus` when
  it ends, like `SyncCoordinator.RunAsync`.
- A second `POST` while one is running returns immediately with the running status. That is the
  coordinator's refusal (it returns `Status`), not the inner `Refused` outcome.
- `GET /api/sync/status` returns the process-held status, for a tab that loads or reloads
  mid-sync.
- The clicking tab shows the running state from the moment it sends the request (R3(2)), then
  re-reads `GET /api/dashboard` when the response arrives.

There is **no polling**: a tab that loaded mid-sync shows "Syncing activities…" until it is
reloaded. That is what the reference does too.

**Rationale.** This is the smallest shape that meets 006 FR-010/FR-012, 009 FR-014 and US3
scenarios 4–5. A background job with polling or server-sent events would add a lifecycle the
reference does not have (Principle III), and a sync of a current history is bounded at 10
seconds (SC-005). A first import stops at Strava's rate limit, so the request is bounded there
too.

**Alternatives considered.** Returning 202 and polling, or server-sent events: rejected as above.
WebSockets: rejected for the same reason.

## R10. Token renewal (implements R3(1))

**Decision.** Before its first request, the sync renews the access token when it has expired or
expires within the next 60 seconds. It uses the existing refresh path (005 FR-004), and stores
both tokens together, since Strava rotates the refresh token (005 C48). If Strava rejects the
refresh token, the sync ends with `ReconnectionRequired` and stores nothing, as a 401 does in the
reference.

**Rationale.** The 60-second margin covers clock skew and a long first page. Renewing on a 401 and
retrying would work too, but it spends one request of a rate-limited budget to learn something
the stored expiry time already says.

## R11. Frontend stack

**Decision.** React 19 with TypeScript (strict), built with **Vite**. There is no router library:
there are two routes (`/`, and anything else as the not-found page), chosen by `location.pathname`.
There is no state library (component state plus one fetch module), and no charting or UI library:
the SVG is hand-drawn, as in 008. The design system's stylesheet is **copied** from
`src/TrainingLoadAnalyzer.Web/wwwroot/Theme/broadsheet.css` into
`alt-stack/frontend/src/theme/broadsheet.css`, with a provenance header. Each `.razor.css` file
becomes a plain `.css` file imported by the matching component. Light and dark remain a
`prefers-color-scheme` media query, with no JavaScript (008 FR-021).

**Rationale.** The reference is deliberately dependency-free on the page (008 removed MudBlazor),
and parity in NFR-001 (bytes before first paint) is part of SC-008's comparison. Copying rather
than referencing keeps 009 FR-004 (no shared production code). 008 NFR-002 (tokens live in one
place) holds within each implementation.

**Alternatives considered.** Next.js or Remix: rejected, because server rendering would duplicate
the backend. Tailwind or CSS modules: rejected, because the design is already expressed as a
token stylesheet plus per-component rules, and a second styling system would be a translation
layer. Recharts or visx: rejected for the reason 008 rejected MudChart, which lost dash patterns
and exact geometry.

## R12. Testing

**Decision.**
- **Backend:** pytest. Domain tests call pure functions. Integration tests drive the Strava client
  against **`httpx.MockTransport`**, a transport stub that answers recorded, anonymized JSON, the
  equivalent of the reference's `StubHttpMessageHandler`. Service-interface tests use FastAPI's
  **`TestClient` in-process**, as the constitution's Alternative-stack section requires. No
  `unittest.mock`: the clock, the Strava transport and the database path are constructor or
  factory inputs (Principle IV as amended). If a genuine need for a mock arises, it is recorded in
  Complexity Tracking before it is used.
- **Frontend:** Vitest, with **React Testing Library** and **jsdom**. Components are pure
  functions of their props. The fetch module is passed in as a plain object, so no test needs
  `vi.fn`/`vi.mock` (same rule). The theme tests port `PaletteContrastTests`,
  `ColourDisciplineTests`, `ResponsiveRulesTests` and `InteractiveControlTests`, parsing the copied
  stylesheet as the reference's tests do.
- **Locale (SC-007):** both suites run a second time under `LANG=fi_FI.UTF-8`, `LC_ALL=fi_FI.UTF-8`
  (and the Node `--icu-data-dir` default). Nothing may format with the ambient locale.
- **Browser automation (Playwright):** not introduced. The reference verifies layout and contrast
  by parsing CSS plus human review, and SC-004/SC-006 are checked the same way plus the quickstart's
  manual pass.

**Rationale.** These are the tools the ecosystem's frameworks document for exactly these jobs, and
each replaces a named reference tool one for one (xUnit → pytest, `WebApplicationFactory` →
`TestClient`, bUnit → Testing Library), which keeps SC-008's comparison like for like.

## R13. Parity reference: golden files from the reference itself

**Decision.** A small **.NET console tool, `parity/generator/`**, references the reference's
`Domain` and `Web` projects, reads purpose-built fixtures from `parity/fixtures/`, and writes
golden outputs to `parity/golden/`. The outputs are:
- loads, daily and weekly totals, metrics and trends (for SC-001);
- the `DashboardView` plus every `Display`/`SyncMessage` string, and the rendered component text
  via ASP.NET Core's `HtmlRenderer` (for SC-002);
- the stored sessions, sync state and `SyncResult` after replaying a recorded Strava response set
  through the reference's sync, with an in-memory SQLite database and a stub handler (for SC-003).

The Python and TypeScript suites read the goldens and assert against them, within the tolerances
in R1, R2 and R8. The goldens are committed, and regenerated only deliberately
(`dotnet run --project parity/generator`).

**Rationale.** FR-022a requires parity against shared fixtures, not real stores. Asserting
against output the reference actually produced is the only way SC-001–SC-003 test "the same" and
not "what someone believes the reference does". The tool touches no reference production code or
tests (009 FR-003), and it is a test harness, not shared production code (009 FR-004).

**Alternatives considered.**
- *Porting the reference's hand-written expectations only.* This is still done, as the TDD
  tests. On its own it cannot catch a behaviour neither suite asserts.
- *Running both apps and diffing HTTP output.* The reference has no HTTP API.

## R14. Configuration and secrets

**Decision.** The backend reads environment variables at startup:

| Variable | Required | Notes |
| --- | --- | --- |
| `TLA_ATHLETE_MAXIMUM_HEART_RATE` | yes | Positive integer; otherwise the process exits with the reference's two messages, reworded only for the variable name |
| `TLA_STRAVA_CLIENT_ID` | yes to connect | — |
| `TLA_STRAVA_CLIENT_SECRET` | yes to connect | — |
| `TLA_DATABASE_PATH` | no | Defaults to `training-load.db` in the backend directory |

For local use, they go in a git-ignored `alt-stack/backend/.env` (the existing `.env` rule
applies), loaded by `uv run --env-file .env`. No settings library is used.

**Rationale.** 009 FR-018 and FR-019. `uv run --env-file` does the loading without a
`python-dotenv` dependency.

## R15. Ports, proxying and the Strava callback

**Decision.**
- **Development:** Vite on `localhost:5173` proxies `/api`, `/connect` and `/strava/callback` to
  Uvicorn on `localhost:8000`, with `changeOrigin: false`. The browser only ever sees `:5173`, and
  the backend builds the callback URI from the `Host` header, as the reference does.
- **Run:** after `npm run build`, Uvicorn also serves `frontend/dist` (the SPA fallback returns
  `index.html` for non-API paths, so `/not-found` and unknown paths reach the React not-found
  page), on one port.

Strava's callback domain stays `localhost`, the setting already configured for the reference, so
nothing changes at Strava.

**Rationale.** The OAuth state cookie is `SameSite=Lax` and set by the backend. Keeping one origin
in both modes means the cookie is sent back on the callback, exactly as in the reference.

## R16. Clock and "today"

**Decision.** The backend takes a clock object with `now_local()` and `now_utc()` (the equivalent
of `TimeProvider`), using the server's local time zone as the reference does (`GetLocalNow`). The
browser's clock is never used for a figure. Tests pass a fixed clock at `2026-09-18` in
`UTC+03:00`, the reference's `FixedLocalClock`.

## R17. Offsets that are not whole minutes

**Finding.** The reference builds `DateTimeOffset` from `utc_offset` seconds. .NET offsets must
be whole minutes (and within ±14 h), so an offset such as 19800.5 s, or any non-minute value,
makes the constructor throw `ArgumentException`. The mapper catches that and skips the activity as
`UnusableByDomain`. Python's `timezone` would accept it silently.

**Decision.** The Python mapper refuses offsets that are not whole minutes, or that lie outside
±14 h, and skips the activity as `UnusableByDomain`. It keeps the reference's handling of a
fractional-but-whole value (`10800.0`, from `fix/strava-utc-offset-decimal`), which is accepted.

---

## Amendment 1 — approved 2026-09-23

Approved by the developer as proposed, and added to [spec.md](./spec.md#amendments).

- **(a) Load precision (R1, R2).** 009 FR-006's "exactly for loads, totals and absolute changes"
  becomes "to within 1e-20 points". A displayed figure may differ from the reference only at an
  exact rounding tie that the two stacks' arithmetic places on opposite sides. Any such case found
  is listed in the parity report.
- **(b) Specification over reference (R3).** Where the reference departs from features 001–008
  and meeting the specification needs no new athlete-facing wording, the new implementation
  follows the specification: token renewal (005 FR-004), and the in-flight sync state in the
  clicking tab (006 US5 sc1). Where it would need new wording (connect outcomes, 005 FR-002a), the
  reference's behaviour stands and the gap stays recorded. Every such case is listed in the parity
  report.
- **(c) Stack-specific surfaces (R4).**
  - The circuit-reconnect modal is not ported.
  - The unhandled-error notice and the `/Error` page are ported with their wording, but without
    the "Development Mode" paragraph that names `ASPNETCORE_ENVIRONMENT`.
  - The rail shows `—` rather than `0 bpm` while the history loads.
