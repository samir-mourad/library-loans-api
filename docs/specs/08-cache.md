# Parte 08 — Cache Redis

## Objetivo
Usar Redis para acelerar leituras de livro e disponibilidade, invalidando de forma coerente a cada empréstimo, devolução, cancelamento ou alteração do livro — sem que o cache jamais participe de uma decisão de negócio. O PostgreSQL continua sendo a única fonte de verdade.

## Escopo

### 1. O que é cacheado: um snapshot por livro
- Uma única chave por livro: `library:v1:book:{id}` com o `BookResponse` serializado (JSON).
- `GET /books/{id}` devolve o snapshot; `GET /books/{id}/availability` deriva o `AvailabilityResponse` do mesmo snapshot (total, disponíveis, ativo).
- **Por que uma chave só**: as duas leituras dependem dos mesmos campos; uma chave significa uma invalidação por mudança, sem risco de uma das duas ficar para trás.
- Não cacheado: listagem (invalidar páginas e filtros não compensa), históricos, auditoria e livros inexistentes (sem cache negativo — evita "ressuscitar" 404 depois de um cadastro).
- O prefixo `v1` permite mudar o formato do snapshot num deploy sem ler entradas antigas.

### 2. Cache-aside, somente L2
- Leitura: tenta o Redis → acerto devolve; erro ou ausência → lê do PostgreSQL → grava no Redis com TTL → devolve.
- TTL configurável `Cache:BookTtlSeconds` (padrão 60, faixa 1–3600, validado no startup).
- Sem cache em memória por réplica (nem o L1 do `HybridCache`): com 2–11 réplicas, cada L1 ficaria desatualizado depois de uma invalidação feita em outra réplica.

### 3. Invalidação após o commit
Remover a chave (`DEL`) **depois** que a transação commita, em:

| Operação | Handler |
|---|---|
| Empréstimo criado (só resultado novo, não replay) | `IdempotentCreateLoanHandler` |
| Devolução / cancelamento | `LoanCloser` |
| Atualização / desativação do livro | `UpdateBookHandler` / `DeactivateBookHandler` (só quando algo mudou) |

- **Por que depois do commit**: invalidar antes deixaria uma janela em que uma leitura concorrente repopula o cache com o valor ainda não alterado.
- **Corrida residual conhecida** (documentada no ADR): leitor lê o valor antigo do banco → escritor commita e remove a chave → leitor grava o valor antigo. O snapshot fica velho por no máximo um TTL. Aceitável porque a disponibilidade exibida é informativa — o empréstimo sempre decide pelo update condicional no PostgreSQL. Evolução: *delayed double delete* ou versionamento do snapshot.
- Remover em vez de atualizar: o escritor não precisa montar o snapshot, e a próxima leitura busca o valor commitado.

### 4. Fail-open
- Qualquer falha do Redis (`RedisConnectionException`, `RedisTimeoutException`) em leitura, escrita ou invalidação: log de warning estruturado e a operação segue pelo PostgreSQL. Nenhuma requisição falha por causa do cache.
- Coerente com o readiness: Redis indisponível = `Degraded`, réplica continua servindo (ADR §5).
- **Timeouts curtos** num único `IConnectionMultiplexer` compartilhado (`AbortOnConnectFail=false`, connect 2s, sync/async 1s), aplicados por código sobre a connection string. Resolve o ponto em aberto da Parte 02: o health check do Redis passa a usar o mesmo multiplexer e respeita esses timeouts, em vez dos ~5s de padrão.

### 5. Organização
- `Infrastructure/Caching/RedisCache`: `GetAsync<T>`, `SetAsync<T>(ttl)`, `RemoveAsync`, com serialização e fail-open centralizados.
- `Features/Books/BookCache`: chave, TTL e `GetOrLoadAsync(bookId, loader)` / `InvalidateAsync(bookId)`.
- Registro do multiplexer e opções em `Infrastructure/Caching/CachingExtensions`.

### 6. Telemetria
- `OpenTelemetry.Instrumentation.StackExchangeRedis` (spans `GET`/`SET`/`DEL` nos traces). O pacote é *beta* no projeto contrib do OpenTelemetry por causa da convenção semântica ainda instável, mas é a única instrumentação oficial do cliente; fica isolado na telemetria e com versão fixa.
- Métricas de hit/miss/falha do cache entram na Parte 09.

## Fora de escopo
Cache da listagem e dos históricos, cache negativo, *stampede protection* (single-flight) — documentados como evolução.

## Pacotes novos (CPM)
`StackExchange.Redis` (estável mais recente), `OpenTelemetry.Instrumentation.StackExchangeRedis` (beta, fixado).

## Testes obrigatórios (integração, Redis real via Testcontainers)
| Cenário | Esperado |
|---|---|
| Primeiro `GET /books/{id}` | chave `library:v1:book:{id}` criada no Redis com TTL > 0 |
| Snapshot alterado direto no Redis | `GET /books/{id}` e `/availability` devolvem o valor do cache (prova de que a leitura vem do Redis) |
| Emprestar | chave removida; próxima leitura de disponibilidade mostra o valor novo |
| Devolver / cancelar | chave removida; disponibilidade restaurada na leitura seguinte |
| PATCH / DELETE do livro | chave removida; leitura seguinte reflete a mudança |
| Replay idempotente | não remove a chave (nada mudou) |
| Redis inacessível (`WithUnreachable`) | criar livro, ler, emprestar e consultar disponibilidade funcionam normalmente |
| Livro inexistente | 404 e nenhuma chave criada |

## Critérios de aceite
- `dotnet build -c Release` sem warnings; `dotnet test` verde.
- Compose: `redis-cli KEYS 'library:*'` mostra a chave após um GET e ela some após um empréstimo; com `docker compose stop redis` a API continua respondendo; spans Redis visíveis no dashboard.
- ADR §5 atualizado (snapshot único, invalidação pós-commit, corrida residual, timeouts).

## Commits
1. `feat: add redis cache infrastructure with fail-open and short timeouts`
2. `feat: cache book snapshots and invalidate after commit`
3. `test: cover cache hits, invalidation and redis outage`
