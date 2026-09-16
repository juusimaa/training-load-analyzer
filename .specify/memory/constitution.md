<!--
Sync Impact Report
- Version change: [none] → 1.0.0 (initial ratification)
- Modified principles: n/a (initial adoption)
- Added sections:
  - Core Principles: I. Strict TDD (NON-NEGOTIABLE), II. Domain Independence from Strava,
    III. Simplicity Before Abstraction (YAGNI), IV. Testability, V. Isolation of External
    Integrations, VI. Observability & Deliberate Error Handling, VII. Specification Adherence
  - Technology Constraints
  - Development Workflow
  - Governance
- Removed sections: none (template placeholders only)
- Deferred TODOs: none
- Template note: this project defines 7 core principles rather than the template's default 5,
  per explicit user instruction; two extra principle sections were added following the same
  structure as the resolved scaffold.
-->

# Training Load Analyzer Constitution

## Core Principles

### I. Strict TDD (NON-NEGOTIABLE)
Development follows RED → GREEN → REFACTOR → VERIFY. Production code MUST NOT be written
without a preceding failing test whenever the behavior can reasonably be developed test-first.
Tests MUST describe desired behavior, not the current implementation. During GREEN, only the
minimum implementation needed to pass the test MUST be written. Writing production code first
and adding tests afterward MUST NOT be described as TDD, regardless of resulting coverage.
Test count and coverage percentage are not goals in themselves and MUST NOT be used as proxies
for correctness or completeness.
Rationale: This project's primary purpose is to evaluate whether strict TDD discipline can be
maintained through AI-assisted implementation; relaxing this principle would invalidate the
exercise itself.

### II. Domain Independence from Strava
The domain layer MUST NOT depend on Strava or any other external activity provider. Strava DTOs,
API shapes, and vendor-specific concepts MUST NOT leak into the domain or application layers.
All external activity data MUST cross into the domain only through an explicit abstraction
(e.g., `IActivitySource`) owned by the domain/application layer and implemented by
infrastructure.
Rationale: Keeping the domain free of Strava enables independent testing of training-load logic
with fakes/in-memory data and protects the core learning objective from third-party API churn.

### III. Simplicity Before Abstraction (YAGNI)
Abstractions (repositories, services, factories, interfaces, layers) MUST only be introduced in
response to a concrete, current need. Speculative generalization for hypothetical future
requirements MUST NOT be introduced. An abstraction with a single implementation and no proven
need for substitution or isolation is treated as a defect and MUST be removed or justified in
writing.
Rationale: AI coding agents tend to over-engineer; this principle exists specifically to counter
that tendency and keep the codebase reviewable by a solo developer.

### IV. Testability
Code MUST be designed so its behavior can be verified without excessive mocking. A mocking
library MUST only be introduced when a genuine need exists (e.g., isolating a slow or
non-deterministic external boundary), not by default. Preference MUST be given to designs that
are naturally testable through their public behavior (pure functions, explicit inputs/outputs)
over designs that require extensive test-double scaffolding.
Rationale: Excessive mocking hides design problems and produces tests that verify
implementation details rather than behavior.

### V. Isolation of External Integrations
Strava OAuth, token handling, API access, pagination, and rate-limit handling MUST be isolated
within an infrastructure/integration layer. This layer MUST be tested at its own boundary
(e.g., via integration tests against recorded/fake responses) and MUST be kept separate from
domain and application logic tests.
Rationale: External integration concerns (network, auth, rate limits) have different failure
modes and test strategies than domain logic; conflating them slows down the test suite and
obscures failures.

### VI. Observability & Deliberate Error Handling
Errors and edge cases that affect correctness (e.g., sync failures, API errors, malformed
activity data) MUST be handled deliberately and MUST NOT be silently swallowed. Error handling
and logging MUST be proportionate to actual risk in this project — comprehensive observability
infrastructure (structured logging pipelines, metrics dashboards, tracing) MUST NOT be built
speculatively.
Rationale: Correctness-relevant failures need visibility, but this is a two-week learning
project, not a production service; observability tooling beyond what aids debugging is scope
creep.

### VII. Specification Adherence
The AI coding agent MUST follow the current feature specification and MUST NOT invent new
requirements, scope, or abstractions beyond what is specified. When a specification is
ambiguous or silent on a needed behavior, the ambiguity MUST be resolved by clarifying or
amending the specification (e.g., via `/speckit-clarify`) rather than by the agent deciding
silently in code.
Rationale: This project exists to evaluate Spec-Driven Development; an agent that improvises
around gaps in the spec defeats the purpose of the exercise and produces undocumented behavior.

## Technology Constraints

- Backend: C#, modern .NET / ASP.NET Core, ASP.NET Core Web API, Entity Framework Core, SQLite
  during development (may later be replaced by PostgreSQL if it provides meaningful value).
- Frontend: Blazor Web App, kept intentionally lightweight. A different frontend technology
  MUST NOT be introduced without a documented reason, since learning a frontend framework is
  not a project objective.
- Testing: xUnit, with FluentAssertions or built-in xUnit assertions. ASP.NET Core integration
  tests use `WebApplicationFactory`. Testcontainers MAY be introduced only if SQLite is replaced
  by PostgreSQL.
- Strava MCP is a development-time aid only (investigating real data shapes, deriving edge
  cases and test scenarios). It MUST NOT be treated as the application's runtime integration,
  and real personal Strava data MUST NOT be copied directly into test fixtures — test data MUST
  be anonymized and simplified into purpose-built cases.

## Development Workflow

- Spec Kit governs the feature lifecycle: Constitution → Specify → Clarify → Plan → Tasks →
  TDD implementation → Verify/review. Features MUST NOT skip directly to implementation without
  a specification and, where behavior is non-trivial, a plan and task breakdown.
- Tasks MUST be expressed as RED/GREEN/REFACTOR/VERIFY steps tied to concrete example scenarios
  (given/when/then) rather than as bare implementation instructions (e.g., not "implement
  WeeklyLoadCalculator" but the specific failing scenarios it must satisfy).
- A feature is considered complete only when: its specification's acceptance criteria are
  satisfied; it was implemented using TDD where behavior was reasonably testable first; all
  relevant tests are green; the domain has no unnecessary dependency on infrastructure; no
  known unnecessary abstractions remain; agreed error scenarios are handled; the AI-generated
  code has been reviewed by a human; and the specification and implementation are aligned.
- MVP scope and non-goals are defined in the project plan and feature specifications, not in
  this constitution; this document governs engineering process and principles, not feature
  scope.

## Governance

This constitution supersedes ad hoc process decisions for this project. Any conflict between
this document and a specification, plan, or task list MUST be resolved in favor of the
constitution unless the constitution itself is amended first.

Amendments require: the change to be written into this file, a version bump per the policy
below, and the Sync Impact Report at the top of this file to be updated to reflect the change.
Because this is a solo project, amendment "approval" means the developer has deliberately
reviewed and accepted the change (not merged silently by the AI agent).

Versioning policy (semantic versioning applied to governance):
- MAJOR: backward-incompatible removal or redefinition of a principle.
- MINOR: a new principle or section added, or materially expanded guidance.
- PATCH: wording clarifications, typo fixes, non-semantic refinements.

Compliance review: each feature's completion review (see Development Workflow) MUST include an
explicit check against these principles, in particular Strict TDD (I), Domain Independence from
Strava (II), and Simplicity Before Abstraction (III), since these are the principles most at
risk of erosion during AI-assisted implementation.

**Version**: 1.0.0 | **Ratified**: 2026-09-16 | **Last Amended**: 2026-09-16
