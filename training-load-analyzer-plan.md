# Training Load Analyzer — Preliminary Project Plan

## 1. Objective

Build a small but realistic AI-assisted training project in approximately two weeks, with the primary focus on **Spec-Driven Development (GitHub Spec Kit)** and a **strict TDD process**.

The application reads the user's training activities from Strava, maps them into its own domain model, and produces easy-to-interpret training-load metrics and observations.

The primary learning objective is not C# or application-development fundamentals, but rather:

- using Spec Kit in a real development workflow
- guiding an AI coding agent through specifications
- maintaining TDD discipline during AI-assisted implementation
- keeping a clean domain boundary around the external Strava integration
- critically reviewing AI-generated code, tests, and architectural decisions

## 2. Working Title

**Training Load Analyzer**

Alternative names can be considered later. The name is not relevant to the MVP.

## 3. MVP

The first version must be able to:

1. authenticate the user with Strava
2. retrieve the user's running and cycling activities
3. map Strava data into the application's own `TrainingActivity` model
4. persist the required activity data locally
5. synchronize new activities without unnecessarily re-fetching the full history
6. calculate training load at daily and weekly levels
7. calculate Fitness / CTL
8. calculate Fatigue / ATL
9. calculate Form / TSB
10. display how these metrics evolve over time
11. detect significant increases or decreases in training load
12. display recent activities and their training load

## 4. Non-goals

The first version explicitly does **not** include:

- social features
- a multi-user SaaS solution
- payments or subscriptions
- custom user management beyond Strava OAuth
- building a Strava clone
- editing activities or sending activities back to Strava
- Garmin, Intervals.icu, or TrainingPeaks integrations
- AI-generated training plans
- medical or health-related recommendations
- a mobile application
- complex cloud infrastructure
- Kubernetes
- distributed caching
- building a custom MCP server on top of Strava data

Scope should only be expanded once the MVP is complete and the tests are green.

## 5. Proposed Technology Stack

### Backend

- C#
- modern .NET / ASP.NET Core
- ASP.NET Core Web API
- Entity Framework Core
- SQLite during development

SQLite can later be replaced with PostgreSQL if doing so provides meaningful value to the exercise.

### Frontend

Keep it intentionally lightweight.

Primary option:

- Blazor Web App

React is an alternative, but learning a frontend technology is not a primary objective of this project. Blazor keeps the solution C#-focused and reduces context switching.

### Testing

- xUnit
- FluentAssertions or built-in xUnit assertions
- test doubles only when needed
- ASP.NET Core integration tests / `WebApplicationFactory`
- optionally Testcontainers later if SQLite is replaced by PostgreSQL

A mocking library should only be introduced when there is an actual need for one.

## 6. Architectural Principle

The application domain must not depend on Strava.

```text
Strava API
    │
    ▼
Strava integration
    │
    ▼
Activity mapper
    │
    ▼
TrainingActivity
    │
    ▼
Training domain
    │
    ├── Load calculation
    ├── Fitness / CTL
    ├── Fatigue / ATL
    ├── Form / TSB
    ├── Weekly analysis
    └── Load anomaly detection
```

Strava DTOs must not leak into the domain or application layers.

## 7. Possible Solution Structure

```text
TrainingLoadAnalyzer.sln

src/
  TrainingLoadAnalyzer.Domain/
  TrainingLoadAnalyzer.Application/
  TrainingLoadAnalyzer.Infrastructure/
  TrainingLoadAnalyzer.Api/
  TrainingLoadAnalyzer.Web/

tests/
  TrainingLoadAnalyzer.Domain.Tests/
  TrainingLoadAnalyzer.Application.Tests/
  TrainingLoadAnalyzer.Infrastructure.Tests/
  TrainingLoadAnalyzer.Api.Tests/
```

This should not be treated as a mandatory Clean Architecture template. Layers should exist only when they provide useful isolation and testability. Unnecessary abstractions should be avoided.

The five projects above are a sketch, not a commitment. Which of them are actually created, and when, is tracked in section 22.

## 8. Preliminary Domain Model

Core concepts may include:

```text
TrainingActivity
ActivityType
TrainingLoad
DailyTrainingLoad
WeeklyTrainingLoad
Fitness
Fatigue
Form
TrainingLoadTrend
LoadAnomaly
```

Potential domain services:

```text
TrainingLoadCalculator
FitnessCalculator
FatigueCalculator
FormCalculator
WeeklyLoadAnalyzer
LoadSpikeDetector
TrainingConsistencyAnalyzer
```

Not all of these should automatically be implemented. The final model should emerge during the Spec Kit specification and planning phases.

## 9. Training Load Calculation

An important early design decision is to define what "training load" means in this application.

Possible approaches include:

- Strava Relative Effort, when available
- a custom heart-rate-based calculation
- a TRIMP-style calculation
- a simple duration × intensity model

For the MVP, choose one clearly defined method and document its limitations. CTL/ATL/TSB calculations must be based consistently on this load value.

The formulas and time constants should be defined in the specification before implementation so the AI agent does not invent them during coding.

## 10. Strava Integration

The application uses Strava's official API as a normal runtime integration.

The integration layer is responsible for:

- OAuth
- token handling
- activity retrieval
- pagination
- rate-limit handling
- incremental synchronization
- mapping Strava DTOs to domain/application models

A preliminary abstraction could look like:

```csharp
public interface IActivitySource
{
    Task<IReadOnlyCollection<ExternalActivity>> GetActivitiesAsync(
        DateTimeOffset after,
        CancellationToken cancellationToken);
}
```

The exact interface should only be decided based on tests and actual use cases.

## 11. Role of Strava MCP

Strava MCP is not the application's runtime integration.

Its potential role is in the AI-assisted development workflow:

```text
Developer / AI agent
       │
       ├── Spec Kit
       ├── repository
       ├── tests
       └── Strava MCP
                │
                ▼
          real training data
```

MCP can be used, for example, to:

- investigate the structure of real-world data
- identify edge cases
- derive realistic acceptance scenarios
- identify different training-session types
- design representative test data

Real personal Strava data should not be copied directly into test fixtures. Test data should be anonymized and simplified into purpose-built test cases.

## 12. Strict TDD

The project follows:

**RED → GREEN → REFACTOR → VERIFY**

Core principles:

1. Production code is not written without a preceding failing test when the behavior can reasonably be developed using TDD.
2. Tests describe desired behavior rather than the current implementation.
3. During GREEN, write the minimum implementation needed to satisfy the test.
4. Refactoring happens while tests remain green.
5. The AI agent must not implement a feature first and add tests afterwards while calling the process TDD.
6. Test count and coverage percentage are not goals by themselves.
7. Avoid over-testing implementation details.
8. External integrations are tested at an appropriate boundary and kept separate from domain logic.

## 13. Spec Kit Workflow

GitHub Spec Kit is used as the framework for the development process.

Preliminary workflow:

```text
Constitution
     │
     ▼
Specify
     │
     ▼
Clarify
     │
     ▼
Plan
     │
     ▼
Tasks
     │
     ▼
TDD implementation
     │
     ▼
Verify / review
```

### Constitution

The constitution should include at least:

- strict TDD
- domain independence from Strava
- simplicity before abstractions
- no speculative generalization
- testability
- isolation of external integrations
- observability/error handling where meaningful
- the AI agent must follow the specification and must not invent new requirements

## 14. Example TDD Task

Feature:

> Calculate weekly training load.

Tasks should not simply say:

```text
Implement WeeklyLoadCalculator
```

Instead:

```text
RED
Given activities with loads 30, 40 and 50 during the same week
When weekly load is calculated
Then result is 120

GREEN
Implement minimum calculation required to satisfy the test.

RED
Given activities crossing an ISO week boundary
When weekly loads are calculated
Then activities belong to the correct weeks.

GREEN
Implement week boundary handling.

REFACTOR
Improve implementation while keeping all tests green.

VERIFY
Run relevant unit and integration tests.
```

## 15. Preliminary Features

### Feature 1 — Training Activity Domain

- `TrainingActivity`
- activity types
- training-load value
- validation

### Feature 2 — Training Load Aggregation

- daily load
- weekly load
- date-range handling

### Feature 3 — Fitness / Fatigue / Form

- CTL
- ATL
- TSB
- historical evolution

### Feature 4 — Load Trends

- week-over-week load change
- significant increases
- significant decreases

### Feature 5 — Strava Import

- OAuth
- activities
- mapping
- persistence
- incremental synchronization

### Feature 6 — Dashboard

- current Fitness
- Fatigue
- Form
- weekly load
- trend
- recent activities
- simple time-series chart

## 16. Preliminary Two-Week Schedule

### Days 1–2 — Foundation

- repository
- Spec Kit initialization
- constitution
- MVP specification
- domain concept definition
- select the training-load model
- architecture plan
- first tasks

Goal: production code should only be written once the first feature has been sufficiently specified.

### Days 3–4 — Core Domain

Using TDD:

- TrainingActivity
- training load
- daily aggregation
- weekly aggregation

### Days 5–6 — Fitness Model

Using TDD:

- CTL
- ATL
- TSB
- time periods
- missing days
- edge cases

### Day 7 — Domain Review

- refactoring
- evaluate test quality
- compare Spec Kit specifications against implementation
- critically review AI-generated decisions

### Days 8–9 — Strava

- OAuth
- API client
- mapping
- persistence
- incremental synchronization
- integration tests

### Day 10 — Analysis

- weekly trends
- load spike / drop detection
- explanatory analysis text

### Days 11–12 — UI

- dashboard
- trend chart
- recent activities
- sync action

Keep the UI intentionally small.

### Day 13 — End-to-End

- Strava → persistence → domain → dashboard
- error scenarios
- integration tests
- manual exploratory testing

### Day 14 — Review

- refactoring
- documentation
- remove unused code
- evaluate tests
- retrospective on the Spec Kit process
- document where the AI agent succeeded and failed

## 17. Definition of Done

A feature is complete when:

- the specification's acceptance criteria are satisfied
- the feature has been implemented using TDD where appropriate
- all relevant tests are green
- the domain does not unnecessarily depend on infrastructure
- the implementation contains no known unnecessary abstractions
- error scenarios are handled as agreed
- AI-generated code has been reviewed by a human
- specification and implementation are aligned

## 18. Risks

### Scope Creep

This is the largest risk. Training analytics can easily expand into a TrainingPeaks/Intervals.icu clone.

**Mitigation:** Keep the MVP and non-goals visible in Spec Kit.

### Ambiguous Training Load Model

CTL/ATL/TSB are not useful without a consistent training-load metric.

**Mitigation:** Decide and specify the load model before implementing these features.

### Strava Consumes Too Much Time

OAuth, rate limits, and API details may consume time that should be spent on the project's core learning goals.

**Mitigation:** Implement the domain completely independently of Strava first. `IActivitySource` can initially use a fake/in-memory implementation.

### AI Breaks the TDD Process

The agent may generate tests and production code at the same time, or write tests afterwards.

**Mitigation:** constitution + small tasks + explicit separation of RED/GREEN/REFACTOR phases.

### Overengineering

AI coding agents can easily generate repository, service, factory, and abstraction layers without a concrete need.

**Mitigation:** Introduce abstractions only in response to concrete requirements. Put YAGNI explicitly in the constitution.

## 19. Stretch Goals

Only if the MVP is completed early:

- activity classification: easy / long / interval / recovery
- training consistency analysis
- monotony / strain-style metrics
- impact of an individual activity on ATL/CTL
- improved dashboard visualization
- Docker
- PostgreSQL
- GitHub Actions CI
- mutation testing to evaluate TDD test quality
- property-based testing for calculation logic

## 20. Project Success Criteria

The project should not primarily be evaluated by the number of implemented features.

The exercise is successful if, after two weeks, you can evaluate from practical experience:

1. Does Spec Kit help the AI agent make more consistent changes?
2. How well does the AI follow strict TDD?
3. At which stages does a human need to intervene in the agent's decisions?
4. Does specifying behavior before implementation improve the result?
5. What task size works best for the AI agent?
6. Does TDD with AI lead to better domain design or merely more tests?
7. How much useful structure does Spec Kit add, and how much ceremony?

## 21. Next Step

Before the first implementation task:

1. finalize the MVP scope
2. select the training-load calculation model
3. create the Spec Kit constitution
4. create the first feature specification
5. create the technical plan
6. create a TDD-oriented task breakdown

The recommended first implementation feature is **Training Load Aggregation**, because it is pure domain logic and requires neither Strava, a database, nor a UI. This allows the Spec Kit + TDD workflow to be validated as early as possible.

## 22. Open Decisions

Decisions that are not yet made, are needed by a specific feature, and should be made deliberately rather than inherited from a project template. Each records what depends on it and, where there is one, the default to fall back on.

### 22.1 Blazor render mode

**Status:** decided 2026-09-18 — **Interactive Server**. See [specs/006-dashboard/research.md](specs/006-dashboard/research.md) R1.

The Blazor Web App template offers Interactive Server, Interactive WebAssembly, and Interactive Auto. The choice is not a UI detail — it decides how many projects the solution needs.

- **Interactive Server:** components run on the server over a SignalR circuit and inject services directly from DI. The dashboard needs no HTTP API and no shared DTO project. A separate `TrainingLoadAnalyzer.Api` project may not be needed for the MVP at all.
- **Interactive WebAssembly or Auto:** components run in the browser, so the dashboard needs an HTTP API and a shared contracts project referenced by both ends, so the wire shape is agreed in one place. This is a contracts project, not an application layer; the two should not be conflated.

**Default if nothing forces otherwise:** Interactive Server. It has the fewest moving parts, keeps the MVP in one process, and learning a frontend technology is not a project objective.

### 22.2 Whether `TrainingLoadAnalyzer.Application` is created

**Status:** decided for now — not created. **Revisit at:** Feature 5 (Strava import).

For the MVP the application layer amounts to roughly two use cases: synchronize activities from Strava, and produce the dashboard view. Use cases live in a `Features/` folder inside whichever project hosts them until one of the following makes a separate assembly earn its place:

1. the same use case must run from two hosts, for example an HTTP endpoint and a background sync service;
2. both Blazor components and API endpoints must call the same orchestration;
3. the compiler is wanted to prevent orchestration code from reaching for `HttpContext` or other host types.

Testability is deliberately not on that list: a test project can reference the hosting assembly and exercise those classes directly. `WebApplicationFactory` is only needed for HTTP-level tests, which test the endpoint rather than the use case.

Promoting the classes to their own assembly later is a mechanical change to a `.csproj` and some namespaces, and the domain does not move at all. The reasoning is recorded in [specs/002-training-load-aggregation/research.md](specs/002-training-load-aggregation/research.md) R1.

### 22.3 Whether `TrainingLoadAnalyzer.Infrastructure` is created

**Status:** effectively decided — yes, when Feature 5 starts.

This one is not a YAGNI judgement call. Constitution Principle V requires the Strava integration — OAuth, token handling, API access, pagination, rate limits — to be isolated in an infrastructure layer and tested at its own boundary, separately from domain and application logic. Entity Framework Core belongs on the same side of that line. The project is created when Feature 5 needs it, not before.
