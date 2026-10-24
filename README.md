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

- API: http://localhost:8080 — `GET /health/live`, `GET /health/ready`
- Aspire Dashboard (traces, métricas, logs): http://localhost:18888

Para rodar a API fora do Compose (`dotnet run --project src/Library.Api`), defina as connection strings por variável de ambiente ou user-secrets; a aplicação falha no startup se estiverem ausentes.

## 2. Migrations e testes
Os testes de integração sobem PostgreSQL e Redis via Testcontainers, então exigem Docker em execução.

```bash
dotnet test --solution Library.slnx
```
_TODO (Parte 03): migrations._

## 3. Variáveis de ambiente
| Variável | Descrição |
|---|---|
| `ConnectionStrings__Postgres` | Connection string do PostgreSQL (obrigatória) |
| `ConnectionStrings__Redis` | Connection string do Redis (obrigatória) |
| `Loans__DefaultPeriodDays` | Prazo padrão do empréstimo em dias, 1–365 (padrão 14) |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | Endpoint OTLP; se ausente, a telemetria não é exportada |

Cabeçalhos aceitos em todas as requisições:

| Cabeçalho | Uso |
|---|---|
| `X-Correlation-Id` | Correlação ponta a ponta (gerado se ausente/inválido; sempre devolvido) |
| `X-Actor-Id` | Ator registrado na auditoria (`anonymous` se ausente) |

## 4. Decisões de concorrência, idempotência, cache e auditoria
Resumo em [ADR 0001](docs/adr/0001-architecture-overview.md). _TODO (Parte 11): versão final aqui._

## 5. Limitações conhecidas e evolução para produção
_TODO (Parte 11)._
