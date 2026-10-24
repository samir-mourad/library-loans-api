# Parte 01 — Fundação

## Objetivo
Esqueleto que compila, testa, sobe via Docker Compose e registra as decisões arquiteturais, para que as próximas partes sejam apenas features.

## Escopo
- `global.json` (SDK 10.0.x), `Library.slnx`, `Directory.Build.props` (nullable, warnings como erro, analyzers), `Directory.Packages.props` (CPM).
- Projetos: `Library.Domain`, `Library.Api`, `Library.UnitTests`, `Library.IntegrationTests`.
- `GET /health/live` sem checks de dependência.
- Dockerfile multi-stage, usuário não-root, porta 8080.
- Docker Compose: PostgreSQL 17, Redis 7, Aspire Dashboard, API. Credenciais via `.env` (não versionado; `.env.example` versionado).
- CI no GitHub Actions: restore, build, test, docker build.
- Docs: `CLAUDE.md`, `README.md` (esqueleto), ADR 0001, roadmap.

## Fora de escopo
Banco/migrations (Parte 03), OpenTelemetry e readiness (Parte 02), qualquer endpoint de negócio.

## Critérios de aceite
- `dotnet build -c Release` sem warnings.
- `dotnet test` com 2 testes smoke verdes (unitário + `/health/live` 200).
- `docker compose up --build` → `GET http://localhost:8080/health/live` = 200; dashboard em `http://localhost:18888`.
- `.env`, `bin/`, `obj/` fora do Git.

## Commit
`chore: bootstrap solution, docker compose, CI and architecture docs`
