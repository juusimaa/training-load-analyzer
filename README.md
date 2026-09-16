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

Early development. The current focus is the **Training Activity Domain** (see
[specs/001-training-activity-domain](specs/001-training-activity-domain/spec.md)): the core
`TrainingActivity` model — identifier, start time with timezone offset, duration, activity type,
optional heart-rate data, and a training-load calculation (measured from heart-rate zones when
available, otherwise an explicit estimate).

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

## Project structure

```
src/
  TrainingLoadAnalyzer.Domain/         Core domain model (no external dependencies)
tests/
  TrainingLoadAnalyzer.Domain.Tests/   Domain unit tests
specs/                                  Spec Kit feature specs, plans, and tasks
training-load-analyzer-plan.md          Preliminary project plan
```

## Getting started

Requires the .NET SDK version pinned in [global.json](global.json).

```bash
dotnet build
dotnet test
```

## Development process

This project follows Spec Kit's spec → plan → tasks → implement workflow, with tests written
before implementation (TDD). See the [specs](specs/) directory for feature specifications and the
[project plan](training-load-analyzer-plan.md) for overall goals and non-goals.

## License

[MIT](LICENSE)
