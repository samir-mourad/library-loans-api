# Library Loans API

API REST em .NET 10 para catálogo e empréstimos de uma biblioteca, projetada para rodar em múltiplas réplicas com integridade sob concorrência, idempotência, auditoria de negócio e cache coerente.

> Em construção — ver [roadmap](docs/specs/00-roadmap.md). Decisões em [ADR 0001](docs/adr/0001-architecture-overview.md). Enunciado em [docs/challenge.md](docs/challenge.md).

## 1. Pré-requisitos e execução
- .NET SDK 10.0.x
- Docker Desktop (ou Docker Engine + Compose v2)

```bash
cp .env.example .env
docker compose up --build
```

- API: http://localhost:8080 — `GET /health/live`
- Aspire Dashboard (traces, métricas, logs): http://localhost:18888

## 2. Migrations e testes
```bash
dotnet test
```
_TODO (Parte 03): migrations._

## 3. Variáveis de ambiente
| Variável | Descrição |
|---|---|
| `ConnectionStrings__Postgres` | Connection string do PostgreSQL |
| `ConnectionStrings__Redis` | Connection string do Redis |
| `Loans__DefaultPeriodDays` | Prazo padrão do empréstimo em dias (padrão 14) |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | Endpoint OTLP para traces/métricas/logs |

## 4. Decisões de concorrência, idempotência, cache e auditoria
Resumo em [ADR 0001](docs/adr/0001-architecture-overview.md). _TODO (Parte 11): versão final aqui._

## 5. Limitações conhecidas e evolução para produção
_TODO (Parte 11)._
