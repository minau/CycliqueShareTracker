# AGENTS.md

## Project intent
Personal financial analysis website for TotalEnergies.
MVP 1 only: daily data ingestion, indicators, scoring, protected dashboard, PostgreSQL, Docker.

## Architecture
Keep a clean modular architecture:
- Web
- Application
- Domain
- Infrastructure
- Tests

## Constraints
- No broker integration
- No live trading
- No real-time streaming
- No machine learning
- No unnecessary abstractions
- Keep it simple and maintainable
- Use environment variables for secrets
- Prefer explicit code over clever code

## Quality
- Add unit tests for scoring logic
- Handle provider failures gracefully
- Keep deployment compatible with a small OVH VPS