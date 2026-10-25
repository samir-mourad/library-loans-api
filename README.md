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
- Documentação interativa (Development): http://localhost:8080/scalar — OpenAPI em `/openapi/v1.json`
- Aspire Dashboard (traces, métricas, logs): http://localhost:18888

Para rodar a API fora do Compose (`dotnet run --project src/Library.Api`), defina as connection strings por variável de ambiente ou user-secrets; a aplicação falha no startup se estiverem ausentes.

## 2. Migrations e testes
As migrations do EF Core ficam em `src/Library.Api/Infrastructure/Persistence/Migrations` e são aplicadas por um *migration bundle* (imagem `migrator`), nunca no startup da API. No Compose, o serviço `migrator` roda e termina antes da `api` subir.

```bash
dotnet tool restore
dotnet ef migrations add <Nome> --project src/Library.Api --output-dir Infrastructure/Persistence/Migrations
docker compose run --rm migrator
```

Fora do Compose, aplique localmente com `dotnet ef database update --project src/Library.Api` (usa `ConnectionStrings__Postgres`).

Os testes de integração sobem PostgreSQL e Redis via Testcontainers, então exigem Docker em execução.

```bash
dotnet test --solution Library.slnx
```

## 3. Variáveis de ambiente
| Variável | Descrição |
|---|---|
| `ConnectionStrings__Postgres` | Connection string do PostgreSQL (obrigatória) |
| `ConnectionStrings__Redis` | Connection string do Redis (obrigatória) |
| `Loans__DefaultPeriodDays` | Prazo padrão do empréstimo em dias, 1–365 (padrão 14) |
| `Cache__BookTtlSeconds` | TTL do snapshot de livro no Redis, 1–3600 (padrão 60) |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | Endpoint OTLP; se ausente, a telemetria não é exportada |

Cabeçalhos aceitos em todas as requisições:

| Cabeçalho | Uso |
|---|---|
| `X-Correlation-Id` | Correlação ponta a ponta (gerado se ausente/inválido; sempre devolvido) |
| `X-Actor-Id` | Ator registrado na auditoria (`anonymous` se ausente) |
| `Idempotency-Key` | Obrigatório em `POST /loans`; repetir a chave devolve a resposta original (`Idempotent-Replayed: true`) |

### Métricas de negócio (meter `Library.Api`, exportado via OTLP)

| Métrica | Tipo | Significado |
|---|---|---|
| `library.loans.created` | Counter | Empréstimos criados |
| `library.loans.unavailable` | Counter | Pedidos rejeitados por falta de exemplar |
| `library.loans.idempotent_replays` | Counter | Pedidos respondidos a partir de uma `Idempotency-Key` já usada |
| `library.loans.request.duration` | Histogram (s) | Latência do `POST /loans` |

Localmente, veja em http://localhost:18888 → Métricas → `library-api` → `Library.Api`.

## 4. Decisões de concorrência, idempotência, cache e auditoria
Resumo em [ADR 0001](docs/adr/0001-architecture-overview.md). _TODO (Parte 11): versão final aqui._

## 5. Limitações conhecidas e evolução para produção
_TODO (Parte 11)._

## 6. Kubernetes
Manifests em [`deploy/k8s/`](deploy/k8s) (YAML puro; nenhum cluster é necessário para avaliar):

| Arquivo | Conteúdo |
|---|---|
| `configmap.yaml` | Configuração não secreta (ambiente, prazo do empréstimo, TTL do cache, OTLP) |
| `secret.example.yaml` | Formato do Secret com valores `CHANGE_ME` — nunca versionar valores reais |
| `migration-job.yaml` | `Job` que aplica as migrations com a imagem `migrator` |
| `deployment.yaml` | 2 réplicas, requests/limits, liveness `/health/live`, readiness `/health/ready` |
| `service.yaml` | `ClusterIP` porta 80 → 8080 |

As imagens `library-api:1.0.0` e `library-migrator:1.0.0` são placeholders — publique as imagens dos targets `runtime` e `migrator` do Dockerfile no seu registry e ajuste as tags.

```bash
kubectl create secret generic library-api-secrets --from-literal=ConnectionStrings__Postgres="<postgres>" --from-literal=ConnectionStrings__Redis="<redis>"
```

```bash
kubectl apply -f deploy/k8s/configmap.yaml
```

```bash
kubectl apply -f deploy/k8s/migration-job.yaml
```

```bash
kubectl wait --for=condition=complete job/library-api-migrations --timeout=120s
```

```bash
kubectl apply -f deploy/k8s/deployment.yaml -f deploy/k8s/service.yaml
```

**Probes**: liveness não consulta dependências — uma queda do PostgreSQL ou do Redis nunca reinicia o processo. Readiness retorna 503 só sem PostgreSQL (o pod sai do balanceamento); sem Redis o status é `Degraded` (200) e o pod continua servindo, porque o cache é fail-open.

## 7. Correção entre 2 e 11 réplicas
As réplicas não guardam estado que influencie decisões; toda coordenação acontece no PostgreSQL. Por isso escalar de 2 para 11 não muda o comportamento:

- **Último exemplar**: o empréstimo decide com um único `UPDATE ... SET available_copies = available_copies - 1 WHERE is_active AND available_copies > 0`. Em Read Committed, a transação concorrente espera o lock da linha e o PostgreSQL reavalia o `WHERE` sobre a versão commitada — exatamente um vence, de qualquer réplica. `CHECK (available_copies BETWEEN 0 AND total_copies)` é a última defesa. Coberto por teste com dois hosts e 20 requisições simultâneas.
- **Empréstimo duplicado**: índice único parcial `(book_id, user_id) WHERE status = 'Active'`.
- **Idempotência**: a chave é inserida na mesma transação do empréstimo; a mesma chave em réplicas diferentes bloqueia na PK até a primeira commitar e então recebe a resposta gravada.
- **Devolução/cancelamento**: lock da linha do empréstimo; uma segunda devolução simultânea recebe 409 e o estoque sobe uma vez. Locks sempre na ordem empréstimo → livro, sem deadlock.
- **Cache**: Redis compartilhado entre réplicas, invalidado após cada commit, sem cache em memória local; o Redis nunca participa de uma decisão.
- **Conexões**: `Maximum Pool Size=8` por réplica → 11 × 8 = 88 conexões, abaixo do `max_connections` padrão (100) do PostgreSQL. Acima disso, PgBouncer.
- **Migrations**: executadas uma vez pelo `Job` (com o lock de migração do EF Core), nunca por cada réplica no startup.
- **Auditoria e correlação**: gravadas na transação de cada mudança, com `correlationId` da requisição — a trilha é a mesma não importa qual réplica atendeu.
