# Library Loans API

API REST em .NET 10 para catálogo e empréstimos de uma biblioteca, projetada para rodar em múltiplas réplicas com integridade sob concorrência, idempotência, auditoria de negócio e cache coerente.

Decisões detalhadas em [ADR 0001](docs/adr/0001-architecture-overview.md) · enunciado em [docs/challenge.md](docs/challenge.md) · trabalho dividido em partes em [docs/specs](docs/specs/00-roadmap.md).

**Stack**: .NET 10 (Minimal APIs) · PostgreSQL 17 + EF Core/Npgsql · Redis 7 · OpenTelemetry · xUnit v3 + Testcontainers · Docker Compose · Kubernetes.

## 1. Pré-requisitos e execução
- .NET SDK 10.0.x
- Docker Desktop (ou Docker Engine + Compose v2)

```bash
cp .env.example .env
```

```bash
docker compose up --build
```

O Compose sobe PostgreSQL, Redis, o Aspire Dashboard, o `migrator` (aplica as migrations e termina) e a API.

- API: http://localhost:8080 — `GET /health/live`, `GET /health/ready`
- Documentação interativa (Development): http://localhost:8080/scalar — OpenAPI em `/openapi/v1.json`
- Aspire Dashboard (traces, métricas, logs): http://localhost:18888

Para rodar a API fora do Compose (`dotnet run --project src/Library.Api`), defina as connection strings por variável de ambiente ou user-secrets; a aplicação falha no startup se estiverem ausentes.

## 2. Migrations e testes
As migrations do EF Core ficam em `src/Library.Api/Infrastructure/Persistence/Migrations` e são aplicadas por um *migration bundle* (imagem `migrator`), nunca no startup da API. No Compose, o serviço `migrator` roda e termina antes da `api` subir.

```bash
dotnet tool restore
```

```bash
dotnet ef migrations add <Nome> --project src/Library.Api --output-dir Infrastructure/Persistence/Migrations
```

```bash
docker compose run --rm migrator
```

Fora do Compose, aplique localmente com `dotnet ef database update --project src/Library.Api` (usa `ConnectionStrings__Postgres`).

Os testes de integração sobem PostgreSQL e Redis reais via Testcontainers, então exigem Docker em execução:

```bash
dotnet test --solution Library.slnx
```

Cobertura relevante: regras de domínio (unitários); fluxos HTTP completos; **teste concorrente do último exemplar** (dois hosts, 20 requisições simultâneas); idempotência sequencial e concorrente; preservação de histórico após devolução/cancelamento; cache e queda do Redis; auditoria append-only; métricas.

## 3. Variáveis de ambiente
| Variável | Descrição |
|---|---|
| `ConnectionStrings__Postgres` | Connection string do PostgreSQL (obrigatória) |
| `ConnectionStrings__Redis` | Connection string do Redis (obrigatória) |
| `Loans__DefaultPeriodDays` | Prazo padrão do empréstimo em dias, 1–365 (padrão 14) |
| `Cache__BookTtlSeconds` | TTL do snapshot de livro no Redis, 1–3600 (padrão 60) |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | Endpoint OTLP; se ausente, a telemetria não é exportada |

Nenhum segredo é versionado: o Compose lê credenciais do `.env` (fora do Git; `.env.example` só tem placeholders) e o Kubernetes, de um Secret.

Cabeçalhos aceitos em todas as requisições:

| Cabeçalho | Uso |
|---|---|
| `X-Correlation-Id` | Correlação ponta a ponta (gerado se ausente/inválido; sempre devolvido) |
| `X-Actor-Id` | Ator registrado na auditoria (`anonymous` se ausente) |
| `Idempotency-Key` | Obrigatório em `POST /loans`; repetir a chave devolve a resposta original (`Idempotent-Replayed: true`) |

## Endpoints
| Método | Rota | Descrição |
|---|---|---|
| POST | `/books` | Cadastra livro (ISBN-10/13 validado e normalizado para ISBN-13, único) |
| GET | `/books` | Lista com `search`, `isbn`, `includeInactive`, `page`, `pageSize` |
| GET | `/books/{id}` | Consulta livro (inclusive inativo) |
| PATCH | `/books/{id}` | Atualização parcial de título, autor e quantidade |
| DELETE | `/books/{id}` | **Desativa** o livro (soft delete) |
| GET | `/books/{id}/availability` | Disponibilidade atual |
| GET | `/books/{id}/history` | Histórico de empréstimos do livro |
| POST | `/users` | Cadastra leitor (e-mail único) |
| GET | `/users/{id}` | Consulta leitor |
| GET | `/users/{id}/loans` | Histórico de empréstimos do leitor (filtro `status`) |
| POST | `/loans` | Empresta um exemplar — **exige `Idempotency-Key`** |
| GET | `/loans/{id}` | Consulta empréstimo |
| POST | `/loans/{id}/return` | Devolve |
| POST | `/loans/{id}/cancel` | Cancela, com `reason` opcional |
| GET | `/audit-events` | Trilha de auditoria (`entityType`, `entityId`, `action`, `from`, `to`) |
| GET | `/health/live` · `/health/ready` | Liveness e readiness |

**Ajustes em relação à lista sugerida no enunciado:**
- `DELETE /books/{id}` desativa em vez de remover, é idempotente (repetir devolve 204) e o livro continua consultável — o histórico nunca é apagado.
- `PATCH /books/{id}` é parcial (campo ausente = inalterado); o ISBN é imutável depois do cadastro.
- `POST /loans` exige o cabeçalho `Idempotency-Key` (400 se ausente).
- Adicionados `GET /users/{id}` e `GET /loans/{id}`; `POST /loans/{id}/cancel` aceita `reason`.

Erros seguem Problem Details (RFC 9457) com `code` estável (ex.: `book.unavailable`), `correlationId` e `traceId`: 400 validação, 404 inexistente, 409 conflito de regra/estado, 422 `Idempotency-Key` reutilizada com outro pedido.

## Métricas de negócio
Meter `Library.Api`, exportado via OTLP:

| Métrica | Tipo | Significado |
|---|---|---|
| `library.loans.created` | Counter | Empréstimos criados |
| `library.loans.unavailable` | Counter | Pedidos rejeitados por falta de exemplar |
| `library.loans.idempotent_replays` | Counter | Pedidos respondidos a partir de uma `Idempotency-Key` já usada |
| `library.loans.request.duration` | Histogram (s) | Latência do `POST /loans` |

Logs são JSON estruturado com `correlationId`, `actorId` e identificadores de negócio (`loanId`, `bookId`, `userId`), sem dados pessoais. Localmente, tudo aparece no Aspire Dashboard (http://localhost:18888).

## 4. Decisões de concorrência, idempotência, cache e auditoria
**Concorrência.** O empréstimo é decidido por um único `UPDATE books SET available_copies = available_copies - 1 WHERE id = @id AND is_active AND available_copies > 0`. Em Read Committed, a transação concorrente espera o lock da linha e o PostgreSQL reavalia o `WHERE` sobre a versão commitada, então só uma leva o último exemplar — sem Serializable, sem retry, sem lock distribuído. `CHECK (available_copies BETWEEN 0 AND total_copies)` é a defesa final e um índice único parcial impede dois empréstimos ativos do mesmo livro pelo mesmo leitor. Devolução, cancelamento e alterações do livro travam a linha (`SELECT ... FOR UPDATE`) e aplicam a regra no domínio, garantindo diff exato para a auditoria e que o estoque nunca suba duas vezes. *Trade-off*: token otimista (`xmin`) e Serializable foram descartados por gerarem retries sob contenção; lock distribuído no Redis tornaria o cache fonte de verdade.

**Idempotência.** A `Idempotency-Key` é gravada no PostgreSQL na mesma transação do empréstimo (`INSERT ... ON CONFLICT DO NOTHING`). Uma repetição recebe a resposta gravada (inclusive rejeições de negócio); a mesma chave com outro pedido recebe 422; requisições simultâneas com a mesma chave esperam a primeira commitar e fazem replay. Erros inesperados fazem rollback da chave, permitindo retry. *Trade-off*: Redis seria mais rápido, mas perderia a atomicidade entre "chave gravada" e "empréstimo criado".

**Cache.** Cache-aside no Redis com um snapshot por livro (serve livro e disponibilidade), TTL de 60s e invalidação após cada commit que altera o livro. Sem cache em memória por réplica. Qualquer falha do Redis é ignorada (fail-open) com timeouts curtos; o readiness só depende do PostgreSQL. *Trade-off*: existe uma corrida residual em que um valor antigo pode ficar até um TTL no cache — aceitável porque a disponibilidade exibida é informativa e o empréstimo sempre decide no PostgreSQL.

**Auditoria.** Eventos de negócio explícitos (`BookCreated`, `LoanReturned`...) gravados pelos handlers na mesma transação da mudança, com entidade, id, ação, ator, timestamp UTC, `correlationId` e o diff/snapshot em `jsonb`. Triggers no banco bloqueiam UPDATE, DELETE e TRUNCATE na trilha. Dados pessoais ficam fora dela (LGPD: a trilha não pode ser apagada). *Trade-off*: um interceptor do EF seria automático, mas perderia a intenção de negócio e não enxergaria o `UPDATE` atômico do empréstimo.

## 5. Limitações conhecidas e evolução para produção
- **Sem autenticação**: o `X-Actor-Id` é confiado. Evolução: JWT, com o `sub` como ator e no escopo da idempotência.
- **Chaves de idempotência sem limpeza**. Evolução: job agendado removendo registros com mais de 24h (há índice em `created_at`).
- **Paginação por offset**. Evolução: keyset em `(occurred_at, id)` para a auditoria, que cresce sem limite.
- **Corrida residual do cache** (valor antigo por até um TTL). Evolução: *delayed double delete* ou snapshot versionado.
- **Exemplares como contador**, sem rastrear o exemplar físico. Evolução: tabela de exemplares com `SELECT ... FOR UPDATE SKIP LOCKED`.
- **Sem anonimização de usuário**. Evolução: endpoint que anonimiza nome/e-mail e registra `UserAnonymized`.
- **Crescimento de `audit_events`**. Evolução: particionamento mensal por `occurred_at`.
- **Conexões**: 11 réplicas × pool de 8 = 88, abaixo do limite padrão do PostgreSQL. Acima disso, PgBouncer.
- **Kubernetes mínimo**. Evolução: HPA (2–11), PodDisruptionBudget, Ingress com TLS, NetworkPolicy e imagens publicadas em registry.
- **Sem dashboards e alertas**. Evolução: SLO de p99 do `POST /loans` e alerta sobre a taxa de `library.loans.unavailable`.

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

- **Último exemplar**: update condicional atômico (descrito na seção 4) — exatamente um vence, de qualquer réplica. Coberto por teste com dois hosts e 20 requisições simultâneas.
- **Empréstimo duplicado**: índice único parcial `(book_id, user_id) WHERE status = 'Active'`.
- **Idempotência**: a mesma chave em réplicas diferentes bloqueia na chave primária até a primeira commitar e então recebe a resposta gravada.
- **Devolução/cancelamento**: lock da linha do empréstimo; uma segunda devolução simultânea recebe 409 e o estoque sobe uma vez. Locks sempre na ordem empréstimo → livro, sem deadlock.
- **Cache**: Redis compartilhado entre réplicas, invalidado após cada commit, sem cache em memória local; o Redis nunca participa de uma decisão.
- **Conexões**: `Maximum Pool Size=8` por réplica → 11 × 8 = 88 conexões, abaixo do `max_connections` padrão (100) do PostgreSQL.
- **Migrations**: executadas uma vez pelo `Job` (com o lock de migração do EF Core), nunca por cada réplica no startup.
- **Auditoria e correlação**: gravadas na transação de cada mudança, com o `correlationId` da requisição — a trilha é a mesma não importa qual réplica atendeu.
