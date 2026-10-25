# Parte 09 — Métricas de negócio

## Objetivo
Cumprir exatamente o que o enunciado pede em métricas, compatíveis com OpenTelemetry: empréstimos criados, rejeições por indisponibilidade, operações idempotentes repetidas e latência do endpoint de empréstimo. Nada além disso.

## Escopo

### 1. Métricas (`Meter` `Library.Api`, via `IMeterFactory`)
Classe `LoanMetrics` (singleton) com os quatro instrumentos e um método de negócio para cada.

| Requisito do enunciado | Instrumento | Tipo | Unidade |
|---|---|---|---|
| Empréstimos criados | `library.loans.created` | Counter | `{loan}` |
| Rejeições por indisponibilidade | `library.loans.unavailable` | Counter | `{loan}` |
| Operações idempotentes repetidas | `library.loans.idempotent_replays` | Counter | `{request}` |
| Latência do endpoint de empréstimo | `library.loans.request.duration` | Histogram | `s` |

- Sem tags: cada instrumento mede uma coisa só.
- Contadores incrementados só após o commit (criado) ou após a decisão (indisponível, replay) — nada desfeito é contado.
- A latência cobre todo `POST /loans` que chega ao handler, medida com o `TimeProvider`.
- Registro em `AddObservability`; o meter `Library.Api` já é exportado desde a Parte 02.

### 2. Logs
Nenhuma mudança: os logs estruturados de empréstimo (criado, rejeitado, encerrado, replay) com `loanId`/`bookId`/`userId` e `correlationId` no scope já atendem o requisito.

## Fora de escopo
Qualquer métrica, tag, log ou atributo de trace além dos acima.

## Pacotes novos (CPM)
`Microsoft.Extensions.Diagnostics.Testing` (testes — `MetricCollector<T>`).

## Testes obrigatórios (integração)
Cada teste usa um host próprio (`WithWebHostBuilder`), com seu próprio `IMeterFactory`, para que as medições não se misturem com as dos testes paralelos.

| Cenário | Esperado |
|---|---|
| Empréstimo criado | `library.loans.created` = 1; `library.loans.request.duration` com 1 medição |
| Livro sem exemplares | `library.loans.unavailable` = 1 |
| Mesma `Idempotency-Key` repetida | `library.loans.idempotent_replays` = 1; `library.loans.created` continua 1 |

## Critérios de aceite
- `dotnet build -c Release` sem warnings; `dotnet test` verde.
- Compose: após alguns empréstimos, as métricas `library.loans.*` aparecem no Aspire Dashboard.
- README: lista das quatro métricas.

## Commits
1. `feat: add loan business metrics`
2. `test: cover loan business metrics`
