<!--
Sync Impact Report
- Version change: 1.0.0 → 1.1.0 (MINOR: new Technology Constraints subsection; materially
  expanded guidance in Principles II and IV; no principle removed or redefined)
- Modified principles:
  - II. Domain Independence from Strava: the abstraction example is now language-neutral
    (`IActivitySource` is kept as the .NET example, and a protocol/interface is named as the
    equivalent). The rule is unchanged.
  - IV. Testability: now says explicitly that a mocking facility that ships with a language or test
    runner (e.g. Python's `unittest.mock`, a test runner's built-in mock functions) counts as a
    mocking library. The rule is unchanged, and so is its bar for introduction.
- Added sections:
  - Technology Constraints → "Alternative-stack experiment (feature 009)": allows Python/FastAPI and
    React/TypeScript for feature 009 only, gives the documented reason the frontend constraint asks
    for, and fixes the independence, coexistence and merge rules.
- Removed sections: none
- Existing constraints reorganized, not changed: the original bullets now sit under a
  "Reference implementation" heading, with their wording unchanged.
- Deferred TODOs: none
- Downstream impact: specs/009-python-react-stack/spec.md lists this amendment as a blocking
  dependency for /speckit-plan. It is now satisfied.
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
owned by the domain/application layer and implemented by infrastructure (e.g., `IActivitySource`
in the .NET implementation, or an equivalent protocol/interface in another language).
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
non-deterministic external boundary), not by default. A mocking facility that ships with the
language or test runner (e.g., Python's `unittest.mock`, a test runner's built-in mock functions)
counts as a mocking library under this rule, even though nothing has to be installed to use it.
Preference MUST be given to designs that are naturally testable through their public behavior
(pure functions, explicit inputs/outputs) over designs that require extensive test-double
scaffolding.
Rationale: Excessive mocking hides design problems and produces tests that verify
implementation details rather than behavior. Where mocking is built in, it costs nothing to
reach for, so the rule has to say explicitly that it still applies.

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

### Reference implementation

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

### Alternative-stack experiment (feature 009)

- Scope: this subsection applies only to the implementation built under feature 009
  (`specs/009-python-react-stack`) and to branches derived from `new_stack`. Everywhere else,
  the Reference implementation constraints apply unchanged.
- Backend: Python with FastAPI, with SQLite for local storage in the implementation's own file.
  It MUST NOT read or write the reference implementation's store.
- Frontend: React with TypeScript, kept intentionally lightweight on the same terms as the
  reference frontend. The documented reason the Reference implementation's frontend constraint
  requires is this: comparing the two stacks is the stated objective of feature 009.
- Testing: pytest for the backend. Service-interface integration tests MUST run the application
  in-process (the equivalent of `WebApplicationFactory`), not against a separately started
  server. The frontend test tooling is chosen in the feature's plan, subject to Principles III
  and IV.
- Independence: the two implementations MUST NOT share production code, storage or runtime
  components. The reference implementation MUST stay buildable, and its suite MUST keep passing,
  while the experiment is in progress, because it is the experiment's parity reference.
- The Strava MCP and test-data rules of the Reference implementation apply unchanged.
- Merge rule: the alternative stack MUST NOT be merged into `main`, and MUST NOT replace the
  reference implementation, without a further amendment of this constitution that decides which
  stack is canonical.

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

**Version**: 1.1.0 | **Ratified**: 2026-09-16 | **Last Amended**: 2026-09-23
