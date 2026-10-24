# Parte 02 — Transversal

## Objetivo
Entregar a infraestrutura que todas as features vão usar: modelo de erro consistente, correlação, ator, relógio, logs estruturados, OpenTelemetry e health checks de liveness e readiness. Ao fim desta parte, qualquer endpoint novo já nasce com erro em Problem Details, `correlationId` nos logs/traces e telemetria exportável.

## Escopo

### 1. Organização
- Cada preocupação vive em `src/Library.Api/Infrastructure/<Área>/` e expõe extensões `Add<Área>(this IServiceCollection, IConfiguration)` / `Use<Área>(this WebApplication)`.
- `Program.cs` só compõe essas extensões, sem lógica.
- Áreas: `ErrorHandling`, `Correlation`, `Actor`, `Observability`, `Health`.

### 2. Modelo de erro (Result, não exceção)
- Falhas esperadas de negócio **não** usam exceção. Em `Library.Domain`:
  - `ErrorType` (enum): `Validation`, `NotFound`, `Conflict`, `Unprocessable`.
  - `Error` (record): `Code` (ex.: `book.unavailable`), `Message`, `Type`.
  - `Result` / `Result<T>`: sucesso ou `Error`, sem exceção para controle de fluxo.
- Em `Library.Api/Infrastructure/ErrorHandling`: extensão `Error.ToProblem()` que gera `ProblemHttpResult` com o mapeamento:

  | ErrorType | Status |
  |---|---|
  | Validation | 400 |
  | NotFound | 404 |
  | Conflict | 409 |
  | Unprocessable | 422 |

- Exceções ficam só para o inesperado.

### 3. Problem Details em toda a API
- `AddProblemDetails` com `CustomizeProblemDetails` adicionando em **toda** resposta de erro:
  - `correlationId` (o mesmo do header de resposta);
  - `traceId` (do `Activity` atual);
  - `code`, quando não houver: `validation.failed` para 400 de validação, `route.not_found` para 404 sem rota, `internal.error` para 500.
- `IExceptionHandler` global: loga a exceção com `correlationId` e responde 500 Problem Details **sem** stack trace nem mensagem interna em qualquer ambiente.
- `UseStatusCodePages` para que 404/405 sem corpo também virem Problem Details.
- Validação nativa do .NET 10 para Minimal APIs (`AddValidation()`) habilitada: o erro de validação sai como `ValidationProblemDetails` 400 com `code = validation.failed`.

### 4. Correlação
- Middleware lê o header `X-Correlation-Id`.
- Válido se casar `^[A-Za-z0-9._:-]{1,128}$`. Ausente ou inválido → usa o `TraceId` do `Activity` atual (hex de 32 caracteres).
- O valor é:
  - devolvido no header de resposta `X-Correlation-Id`;
  - exposto via `ICorrelationContext.CorrelationId` (scoped) para auditoria nas próximas partes;
  - aberto como scope de log (`correlationId`);
  - adicionado como tag `correlation.id` no `Activity` da requisição.
- Roda antes de tudo que loga (inclusive do exception handler), para o erro 500 carregar a correlação.

### 5. Ator
- `IActorAccessor.ActorId` (scoped) lê o header `X-Actor-Id`.
- Ausente → `anonymous`. Presente → trim; vazio, > 100 caracteres ou com caracteres de controle → 400 Problem Details `code = actor.invalid`.
- Aberto como scope de log (`actorId`) e tag `enduser.id` no `Activity`.
- Limitação registrada no ADR: sem autenticação no escopo; evolução para claim `sub` de JWT sem mudar os consumidores (`IActorAccessor` é o ponto de troca).

### 6. Relógio
- `TimeProvider.System` registrado como singleton. Nenhum código usa `DateTime.UtcNow`/`Now`; testes substituem por `FakeTimeProvider` (`Microsoft.Extensions.TimeProvider.Testing`) quando precisarem.

### 7. Logs estruturados
- `AddJsonConsole` com `IncludeScopes = true`, `TimestampFormat` ISO-8601 e `UseUtcTimestamp = true`.
- Logs também exportados via OpenTelemetry (`IncludeScopes`, `IncludeFormattedMessage`).
- Mensagens com templates estruturados (`LoggerMessage` source generator), nunca interpolação.

### 8. OpenTelemetry
- `AddOpenTelemetry().ConfigureResource(service.name = "library-api", service.version, deployment.environment)`.
- Traces: ASP.NET Core (excluindo `/health/*`) e HttpClient.
- Métricas: ASP.NET Core, HttpClient, Runtime e o `Meter` da aplicação `Library.Api` (criado aqui, vazio; os instrumentos chegam na Parte 09).
- Exporter OTLP **só se** `OTEL_EXPORTER_OTLP_ENDPOINT` estiver configurado, evitando ruído e tentativas de conexão em testes e máquinas sem coletor.
- EF Core/Npgsql e Redis entram nas Partes 03 e 08, quando essas dependências existirem.

### 9. Health checks
- `GET /health/live`: nenhum check (já existe; mantém).
- `GET /health/ready`: checks com tag `ready`:
  - `postgres`: `AspNetCore.HealthChecks.NpgSql`, timeout 3s, falha → `Unhealthy` (503).
  - `redis`: `AspNetCore.HealthChecks.Redis`, timeout 3s, `failureStatus: Degraded` (continua 200) — o cache é fail-open (ADR 0001 §5).
- Resposta JSON enxuta: `{ "status": "...", "checks": [{ "name", "status", "durationMs" }] }`. Sem mensagens de exceção nem connection strings.
- Requisições de health excluídas de traces e de logs de request para não poluir.

### 10. Configuração
- Options tipadas com validação no startup (`ValidateDataAnnotations` + `ValidateOnStart`):
  - `LoansOptions.DefaultPeriodDays` (1–365), já usada a partir da Parte 06;
  - connection strings `Postgres` e `Redis` obrigatórias fora do ambiente `Testing`. Falha rápida se ausentes.

### 11. Testcontainers (antecipado da Parte 03)
- O teste de readiness precisa de PostgreSQL e Redis reais, então a fixture base de integração nasce aqui:
  - `ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime` sobe `PostgreSqlContainer` e `RedisContainer` (Testcontainers) e injeta as connection strings;
  - compartilhada via `[assembly: AssemblyFixture]` (xUnit v3) para subir os containers uma vez por execução.
- A Parte 03 estende essa fixture com migrations e Respawn.

## Fora de escopo
DbContext, migrations, Respawn (Parte 03); instrumentação EF/Redis (03/08); métricas de negócio (09); probes Kubernetes (10).

## Pacotes novos (CPM)
`OpenTelemetry.Extensions.Hosting`, `OpenTelemetry.Exporter.OpenTelemetryProtocol`, `OpenTelemetry.Instrumentation.AspNetCore`, `OpenTelemetry.Instrumentation.Http`, `OpenTelemetry.Instrumentation.Runtime`, `AspNetCore.HealthChecks.NpgSql`, `AspNetCore.HealthChecks.Redis`, `Testcontainers.PostgreSql`, `Testcontainers.Redis`, `Microsoft.Extensions.TimeProvider.Testing` (testes).

## Testes obrigatórios

### Unitários (`Library.UnitTests`)
- `Result` de sucesso não carrega erro; `Result` de falha carrega o `Error` e não expõe valor.
- `Error` preserva `Code`, `Message` e `Type`.

### Integração (`Library.IntegrationTests`)
| Cenário | Esperado |
|---|---|
| `/health/live` com PostgreSQL inacessível (connection string inválida) | 200 |
| `/health/ready` com containers no ar | 200, checks `postgres` e `redis` `Healthy` |
| `/health/ready` com PostgreSQL inacessível | 503, `postgres` `Unhealthy` |
| `/health/ready` com Redis inacessível | 200, `redis` `Degraded` |
| Requisição com `X-Correlation-Id` válido | mesmo valor no header de resposta |
| Requisição sem `X-Correlation-Id` ou com valor inválido | header de resposta com 32 caracteres hex |
| Endpoint de teste que lança exceção | 500 Problem Details com `code = internal.error`, `correlationId`, `traceId` e sem detalhe da exceção |
| Endpoint de teste que retorna `Error` de cada `ErrorType` | status mapeado (400/404/409/422) e `code` no corpo |
| Rota inexistente | 404 Problem Details com `code = route.not_found` |
| `X-Actor-Id` com 101 caracteres | 400 Problem Details com `code = actor.invalid` |

Endpoints de teste são registrados só no `ApiFactory` (via `IStartupFilter`), nunca no código de produção.

## Critérios de aceite
- `dotnet build -c Release` sem warnings; `dotnet test` verde (requer Docker).
- `docker compose up --build`: `/health/ready` 200; parar o Redis → 200 `Degraded`; parar o PostgreSQL → 503.
- Requisições aparecem como traces no Aspire Dashboard com `correlation.id`; logs JSON no console com `correlationId` e `actorId`.
- ADR 0001 atualizado: modelo de erro (Result) e ator via header.

## Commits
1. `feat: add result and error model with problem details mapping`
2. `feat: add correlation id and actor context`
3. `feat: add structured logging and opentelemetry`
4. `feat: add readiness health checks with testcontainers fixture`
