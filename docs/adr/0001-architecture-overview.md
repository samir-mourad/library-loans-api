# ADR 0001 — Visão geral da arquitetura

- **Status:** aceito
- **Data:** 2026-10-24

## Contexto
API REST de catálogo e empréstimos executada em 2 a 11 réplicas. Requisitos centrais: nenhum empréstimo duplicado do último exemplar sob concorrência, idempotência na criação de empréstimo, trilha de auditoria de negócio, cache Redis coerente, observabilidade e operação em Kubernetes. O enunciado prioriza corretude e clareza sobre quantidade de padrões.

## Decisões

### 1. Estrutura: monólito modular enxuto
- `Library.Domain` (regras puras, testáveis sem infraestrutura) + `Library.Api` (endpoints, handlers, EF Core, Redis) organizado por feature.
- Minimal APIs, handlers como classes simples. Sem MediatR/CQRS formal: não resolvem problema concreto aqui.
- **Trade-off:** menos isolamento entre camadas de aplicação e infraestrutura; aceitável no escopo, e o domínio continua isolado.

### 2. Concorrência: atualização condicional atômica
```sql
UPDATE books SET available_copies = available_copies - 1
WHERE id = @id AND is_active AND available_copies > 0;
```
- Executada via `ExecuteUpdateAsync`, na mesma transação do insert do empréstimo, do evento de auditoria e do registro de idempotência. 0 linhas afetadas → 409 `book.unavailable`.
- Defesa em profundidade: `CHECK (available_copies BETWEEN 0 AND total_copies)`.
- **Por que funciona em Read Committed:** a segunda transação espera o lock da linha e o PostgreSQL reavalia o `WHERE` sobre a versão commitada (EvalPlanQual); ela vê `available_copies = 0` e não atualiza. Sem Serializable, sem retry.
- Devolução/cancelamento usam a mesma técnica (`UPDATE loans ... WHERE status = 'Active'`), impedindo incremento duplo do estoque.
- Alteração de quantidade: `available += (novo - total)` só se o resultado for ≥ 0; senão 409.
- **Alternativas descartadas:** token otimista `xmin` (muitos retries sob contenção); Serializable (retry obrigatório em 40001); `SELECT ... FOR UPDATE` (dois round-trips com lock); lock distribuído no Redis (tornaria o cache fonte de verdade; Redlock tem garantias frágeis); um registro por exemplar com `SKIP LOCKED` (mais fiel ao mundo físico, mas não exigido — evolução possível).

### 3. Idempotência: PostgreSQL, mesma transação
- Tabela `idempotency_keys(key PK, request_hash, status_code, response_body jsonb, created_at)`.
- Fluxo: `INSERT ... ON CONFLICT DO NOTHING` da chave no início da transação. Inseriu → executa e grava a resposta. Conflito → retorna a resposta armazenada com header `Idempotent-Replayed: true`.
- Requisições simultâneas com a mesma chave bloqueiam no índice único até a primeira commitar — corretas sem lock extra.
- Sem header → 400. Mesma chave, payload diferente → 422. Rejeições de negócio (409) são armazenadas; 5xx não (permite retry).
- **Por que não Redis:** perderia atomicidade com o empréstimo.

### 4. Auditoria: explícita, transacional, append-only
- Tabela `audit_events(id, entity_type, entity_id, action, actor, occurred_at, correlation_id, data jsonb)`.
- Escrita pelos handlers com intenção de negócio (`BookCreated`, `LoanReturned`, ...), na mesma transação.
- **Por que não interceptor do EF:** captura mudança técnica sem intenção, e `ExecuteUpdate` não passa pelo change tracker — o decremento atômico ficaria invisível.
- Trigger bloqueia `UPDATE`/`DELETE` em `audit_events`.
- Ator via header `X-Actor-Id` (sem autenticação no escopo — limitação documentada; evolução: claim `sub` de JWT). Correlação via `X-Correlation-Id` ou TraceId do `Activity`.

### 5. Cache: cache-aside Redis, somente L2, fail-open
- Cacheados: `GET /books/{id}` e `GET /books/{id}/availability`. Listagem não (invalidação de paginação/filtros não compensa).
- Invalidação por remoção da chave **após o commit**, TTL curto (~60s) como rede de segurança para a corrida de repopulação com valor antigo.
- Sem cache L1 em memória: com várias réplicas, cada uma ficaria desatualizada.
- Redis indisponível → leitura vai ao PostgreSQL (log + métrica). Por isso o readiness depende só do PostgreSQL; Redis é reportado como `Degraded`.

### 6. Catálogo
- **Soft delete**: `DELETE /books/{id}` marca `is_active = false`, é idempotente (repetir devolve 204 sem novo evento) e mantém o livro legível. Bloqueia novos empréstimos e alterações; devoluções e histórico permanecem. Nada é apagado fisicamente.
- **ISBN** normalizado para ISBN-13 (ISBN-10 equivalente colide na unicidade) e imutável após a criação. Unicidade garantida pelo índice único; a violação `23505` vira 409, sem "consultar antes de inserir".
- **Escritas administrativas com lock pessimista**: `PATCH`/`DELETE` carregam o livro com `SELECT ... FOR UPDATE` dentro de uma transação. Garante diff exato para a auditoria, nenhum update perdido e serialização com os decrementos atômicos dos empréstimos — a regra "total não pode ficar abaixo do emprestado" vive no domínio e vale sob concorrência. O caminho quente (empréstimo) segue com update condicional, sem leitura prévia. Token `xmin` descartado: todo empréstimo altera a linha do livro e geraria conflitos espúrios no PATCH.
- **Paginação por offset** (`page`, `pageSize` ≤ 100) em livros e auditoria. Evolução: keyset por `(occurred_at, id)` para a trilha de auditoria, que cresce sem limite.
- **Relógio com precisão de microssegundo** (precisão do `timestamptz`), para que o valor devolvido logo após a escrita seja idêntico ao lido depois.

### 7. Operação
- Migrations versionadas; aplicadas por EF migration bundle (imagem `migrator`, self-contained, não-root) em serviço do Compose / `Job` do Kubernetes — nunca por réplicas no startup, em nenhum ambiente, evitando corrida entre pods e acoplamento entre deploy de schema e de código. O EF Core 9+ adquire lock de migração, protegendo execuções concorrentes do bundle.
- Sem `EnableRetryOnFailure` no EF: a estratégia de retry conflita com as transações explícitas do empréstimo; falhas transitórias chegam ao cliente, que repete com segurança via `Idempotency-Key`.
- `DbContext` com snake_case e enums como texto; uma única configuração de provider (`LibraryDbContextOptions`) compartilhada por app, tooling e testes.
- Pool de conexões: `Maximum Pool Size=8` por réplica (11 × 8 = 88 < `max_connections` padrão 100). Evolução: PgBouncer.
- OpenTelemetry (traces, métricas, logs) via OTLP; Aspire Dashboard localmente.
- `/health/live` sem checks de dependência; `/health/ready` verifica PostgreSQL.

### 8. Testes
- xUnit v3 com `Assert` nativo (sem bibliotecas de asserção), Testcontainers (PostgreSQL/Redis reais), `WebApplicationFactory`. Containers sobem e são migrados uma vez por execução.
- Isolamento por dados em vez de reset do banco (sem Respawn): cada teste cria e verifica apenas seus próprios registros, mantendo a execução paralela. Um teste falha se o modelo divergir das migrations (`HasPendingModelChanges`).
- Teste concorrente usa duas instâncias da aplicação contra o mesmo banco para simular réplicas.

### 9. Modelo de erro: Result, não exceção
- Falhas esperadas de negócio são valores (`Error` com `Code`, `Message`, `ErrorType`; `Result`/`Result<T>`) no `Library.Domain`. Exceções ficam para o inesperado.
- Na borda HTTP, `Error.ToProblem()` mapeia `Validation`/`NotFound`/`Conflict`/`Unprocessable` para 400/404/409/422 em Problem Details com `code`.
- Toda resposta de erro recebe `correlationId` e `traceId`; respostas sem `code` explícito recebem um padrão (`validation.failed`, `route.not_found`, `internal.error`).
- 500 nunca expõe mensagem ou stack trace; a exceção completa vai só para o log, com `correlationId`.
- **Por quê:** exceção como controle de fluxo esconde o contrato do método e custa caro; `Result` torna explícito o que cada operação pode recusar.

### 10. Correlação e ator
- `X-Correlation-Id` aceito se casar `^[A-Za-z0-9._:-]{1,128}$`; senão usa o TraceId. Devolvido no response, em scope de log, tag do trace e disponível via `ICorrelationContext` para a auditoria.
- `X-Actor-Id` resolve `IActorAccessor` (`anonymous` se ausente; 400 `actor.invalid` se vazio, > 100 caracteres ou com caracteres de controle). Sem autenticação no escopo; `IActorAccessor` é o ponto de troca para o `sub` de um JWT.

### 11. Dados pessoais fora da trilha de auditoria
- A trilha é append-only (triggers), portanto nada gravado nela pode ser apagado. Gravar PII ali tornaria impossível atender um pedido de eliminação (LGPD art. 18).
- `UserCreated` registra apenas o fato (`{ "registered": true }`); o `entity_id` identifica o usuário. Nome e e-mail vivem só em `users`, que pode ser anonimizada sem quebrar a trilha. Logs registram apenas `userId`.
- E-mail normalizado para minúsculas e único (índice único; violação `23505` → 409).
- Evolução: endpoint de anonimização (substitui nome/e-mail por marcadores e registra `UserAnonymized`).

## Consequências
- Correção entre 2 e 11 réplicas depende exclusivamente do PostgreSQL (atomicidade, constraints, idempotência); as réplicas são stateless.
- O desenho evita dependências comerciais (MediatR, FluentAssertions) e infraestrutura extra (broker, lock distribuído).
