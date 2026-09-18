# Training Load Analyzer

A small, spec-driven .NET project that reads an athlete's training activities from Strava, maps
them into its own domain model, and produces easy-to-interpret training-load metrics (fitness,
fatigue, form) over time.

This is primarily a learning project. The main focus is not C# or application-development
fundamentals, but:

- using [GitHub Spec Kit](https://github.com/github/spec-kit) in a real development workflow
- guiding an AI coding agent through specifications
- maintaining strict TDD discipline during AI-assisted implementation
- keeping a clean domain boundary around the external Strava integration
- critically reviewing AI-generated code, tests, and architectural decisions

## Status

All six MVP features are implemented. The application connects a Strava account, imports and
incrementally syncs activities, and shows a dashboard with current Fitness, Fatigue and Form, the
current ISO week's load and its comparison with last week's, a 180-day chart of the three metrics,
and the seven most recent sessions with each load marked measured or estimated.

| Feature | Spec |
|---|---|
| 1. Training activity domain | [001](specs/001-training-activity-domain/spec.md) |
| 2. Training load aggregation | [002](specs/002-training-load-aggregation/spec.md) |
| 3. Fitness, fatigue and form | [003](specs/003-fitness-fatigue-form/spec.md) |
| 4. Load trends | [004](specs/004-load-trends/spec.md) |
| 5. Strava import | [005](specs/005-strava-import/spec.md) |
| 6. Dashboard | [006](specs/006-dashboard/spec.md) |

## Planned scope (MVP)

1. Authenticate the user with Strava
2. Retrieve the user's running and cycling activities
3. Map Strava data into the application's own `TrainingActivity` model
4. Persist the required activity data locally
5. Synchronize new activities without re-fetching the full history
6. Calculate training load at daily and weekly levels
7. Calculate Fitness (CTL), Fatigue (ATL), and Form (TSB)
8. Display how these metrics evolve over time
9. Detect significant increases or decreases in training load
10. Display recent activities and their training load

Out of scope for now: social features, multi-user SaaS, payments, non-Strava integrations,
AI-generated training plans, medical/health recommendations, a mobile app, and complex cloud
infrastructure.

## Architectural notes

**UI render mode**: The dashboard (Feature 6) uses Blazor **Interactive Server** to keep the MVP
scope minimal and maintain focus on TDD discipline. This means Blazor components run on the server
and communicate via SignalR, with no separate HTTP API needed.

**Future refactoring path**: If scalability or separation of concerns requires it, the UI can be
refactored to **Interactive WebAssembly** (with an HTTP API and shared contracts project) using the
same Spec Kit TDD process. This is a deferred decision: validate the dashboard works first, then
introduce architectural complexity only if needed (YAGNI principle).

## Project structure

```
src/
  TrainingLoadAnalyzer.Domain/               Core domain model (no external dependencies)
  TrainingLoadAnalyzer.Infrastructure/       Strava integration and SQLite persistence
  TrainingLoadAnalyzer.Web/                  Blazor Interactive Server dashboard
tests/
  TrainingLoadAnalyzer.Domain.Tests/         Domain unit tests
  TrainingLoadAnalyzer.Infrastructure.Tests/ Integration tests at the Strava/SQLite boundary
  TrainingLoadAnalyzer.Web.Tests/            Read-model, component (bUnit) and endpoint tests
scripts/
  compliance-006.sh                          The dashboard's constitution checks, as commands
specs/                                        Spec Kit feature specs, plans, and tasks
training-load-analyzer-plan.md                Preliminary project plan
```

The domain has **zero** package references and zero project references, and the dependency runs one
way only: `Web → Infrastructure → Domain`.

## Getting started

Requires the .NET SDK version pinned in [global.json](global.json).

```bash
dotnet build
dotnet test
```

## Running the dashboard

Four settings are required. The secret and the maximum heart rate belong in user secrets, never in a
tracked file.

```bash
cd src/TrainingLoadAnalyzer.Web
dotnet user-secrets init
dotnet user-secrets set "Athlete:MaximumHeartRate" "190"
dotnet user-secrets set "Strava:ClientId"     "<your client id>"
dotnet user-secrets set "Strava:ClientSecret" "<your client secret>"
```

`ConnectionStrings:Import` has a working default in `appsettings.json` and names a local SQLite file.
At <https://www.strava.com/settings/api>, set the application's **Authorization Callback Domain** to
`localhost`.

```bash
dotnet run --project src/TrainingLoadAnalyzer.Web
```

Then open the printed URL, connect a Strava account, and sync. **The application refuses to start
without `Athlete:MaximumHeartRate`** — every measured training load is computed from it, and starting
against a default nobody chose would produce confidently wrong figures with nothing on screen to
suggest it.

The first sync of a multi-year history **will** stop at Strava's rate limit and report when to retry.
That is by design, not a fault: roughly 1,200 activities cost more requests than one fifteen-minute
window allows.

## Verifying a change

```bash
dotnet test                # the whole suite
./scripts/compliance-006.sh   # the dashboard's constitution checks

# The suite must pass identically under a comma-decimal locale. An SVG coordinate built without
# the invariant culture renders points="0,45,3 1,5,12,25" — valid-looking markup, wrong geometry,
# no exception, and correct on an en-US machine.
DOTNET_SYSTEM_GLOBALIZATION_PREDEFINED_CULTURES_ONLY=false LANG=fi_FI.UTF-8 dotnet test
```

## Development process

This project follows Spec Kit's spec → plan → tasks → implement workflow, with tests written
before implementation (TDD). See the [specs](specs/) directory for feature specifications and the
[project plan](training-load-analyzer-plan.md) for overall goals and non-goals.

The project's own evaluation of that process — what Spec Kit and strict TDD were worth here, where
the AI agent held the line and where it did not — is in the
[retrospective](RETROSPECTIVE.md).

## License

[MIT](LICENSE)
