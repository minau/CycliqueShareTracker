# AGENTS.md

## 1) Project Overview
Cyclique Share Tracker is a **personal** financial analysis web application focused on TotalEnergies as a cyclical stock.

The objective is to ingest daily market/fundamental inputs, compute transparent indicators, and produce actionable **entry signals** for manual decision-making.

This repository is **not** a public trading platform and does **not** execute trades automatically. Signal generation is the current scope; automated execution is out of scope.

## 2) Architecture Guidelines
Use a clean layered architecture and keep responsibilities strict:

- **Web**
  - ASP.NET Core UI/API endpoints, authentication boundary, request/response models.
  - No business/scoring logic in controllers/pages.
- **Application**
  - Use cases, orchestration, service coordination, DTO mapping.
  - Defines service contracts (e.g., `ISignalService`, `IDataProvider`).
- **Domain**
  - Core business rules, scoring logic, signal interpretation, domain entities/value objects.
  - Must remain framework-agnostic.
- **Infrastructure**
  - PostgreSQL persistence, external provider clients, repository implementations, scheduled ingestion, Docker wiring.

Rules:
- Enforce separation of concerns between layers.
- Depend inward (Web -> Application -> Domain; Infrastructure implements Application/Domain contracts).
- Use interfaces for replaceable boundaries (`IDataProvider`, `ISignalService`, repositories, clock/time abstractions when useful).

## 3) Coding Principles
- Prefer simple, explicit code over clever patterns.
- Avoid over-engineering and speculative abstractions.
- Prioritize readability and maintainability over brevity.
- Do not introduce dependencies unless they solve a concrete project need.
- Use dependency injection consistently via ASP.NET Core DI.
- Handle failures gracefully, especially provider/network/data parsing errors.

## 4) Financial Logic Constraints
- No machine learning.
- No black-box or opaque scoring logic.
- Scoring must be deterministic, explainable, and traceable to clear inputs.
- Keep scoring formulas straightforward and documented near implementation.
- Any scoring change must be covered by deterministic tests.

## 5) Data Handling Rules
- Daily data only (no tick/intraday/realtime streams).
- Data provider integrations must be abstracted behind interfaces.
- Do not tightly couple business logic to one external API schema.
- Treat missing/partial/stale data as expected: validate, degrade safely, and surface meaningful errors.

## 6) Security & Configuration
- Never hardcode secrets, credentials, or tokens.
- Use environment variables for all sensitive configuration.
- Keep authentication simple for a single-user context.
- Dashboard and private endpoints must stay protected.

## 7) Testing Guidelines
Focus on fast, deterministic unit tests.

Minimum required unit-test coverage:
- Scoring logic
- Signal mapping/classification

Testing rules:
- Tests must be deterministic and isolated.
- Avoid flaky tests (time/network/random-dependent behavior).
- Keep tests focused on business behavior, not framework internals.

## 8) Deployment Constraints
- Must run reliably on a small OVH VPS.
- Must work with Docker Compose.
- Keep CPU/RAM/storage usage modest.
- Avoid heavy infrastructure and avoid microservice decomposition.

## 9) What NOT to do (Very Important)
Do **not** add or introduce:
- Broker integration
- Automatic trade execution
- Real-time streaming
- WebSockets
- Microservices
- Complex multi-user auth systems
- Unnecessary abstractions/frameworks

Also:
- Do not refactor broad areas without clear, immediate value.
- Do not break MVP scope for speculative future needs.

## 10) Future Extensions (Awareness Only)
Potential later additions (not current scope):
- Alerts (email/Telegram)
- Backtesting
- Multi-asset support
- Improved scoring models (still explainable)

## 11) Review Guidelines
When reviewing PRs, explicitly flag:
- Risky or unexplainable financial logic changes
- Missing null/error/failure handling
- Tight coupling to provider or framework details
- Violations of layer boundaries/modularity
- Design choices that make future extension harder

## 12) Style
- Keep naming consistent across layers and folders.
- Use clear, intention-revealing class/method names.
- Avoid magic numbers; if used in scoring, document why the constant exists.
- Keep files cohesive and focused on one responsibility.
