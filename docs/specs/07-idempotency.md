# Parte 07 — Idempotência

## Objetivo
`POST /loans` passa a exigir `Idempotency-Key`. Repetir a mesma requisição com a mesma chave — em sequência, em paralelo ou em réplicas diferentes — nunca cria dois empréstimos nem reduz a disponibilidade duas vezes, e devolve a resposta produzida na primeira vez.

## Escopo

### 1. Contrato
- Header `Idempotency-Key` obrigatório em `POST /loans`: 1–100 caracteres `[A-Za-z0-9._:-]` (recomendado: UUID gerado pelo cliente).
  - Ausente → 400 `idempotency.key_missing`.
  - Malformado → 400 `idempotency.key_invalid`.
- Mesma chave + mesma requisição → a resposta original (status e corpo), com header `Idempotent-Replayed: true`.
- Mesma chave + requisição diferente → 422 `idempotency.key_reused`.
- Documentado no OpenAPI.

### 2. Armazenamento: PostgreSQL, mesma transação do empréstimo
Tabela `idempotency_keys`:

| coluna | tipo |
|---|---|
| `scope` | text (`POST /loans`) |
| `key` | text |
| `request_hash` | char(64) — SHA-256 da requisição canônica |
| `status_code` | int, nulo até concluir |
| `response_body` | jsonb, nulo até concluir |
| `created_at` | timestamptz |

PK `(scope, key)`; índice em `created_at` (para limpeza).

- **Por que no PostgreSQL e não no Redis**: o registro da chave e o empréstimo precisam commitar ou falhar juntos. Com Redis haveria janela entre "empréstimo criado" e "chave gravada" (ou o inverso) em que um retry duplicaria o empréstimo ou perderia a resposta.

### 3. Fluxo (`IdempotentCreateLoanHandler`, decorando o `CreateLoanHandler`)
Uma transação:
1. `INSERT INTO idempotency_keys (...) VALUES (...) ON CONFLICT DO NOTHING`.
2. **Inseriu** → esta requisição é dona da chave:
   - cria um savepoint e executa o `CreateLoanHandler` (reserva atômica, loan, auditoria);
   - sucesso → grava `201` + `LoanResponse` no registro;
   - rejeição de negócio (404/409, inclusive `loan.duplicate_active`) → volta ao savepoint (desfaz qualquer trabalho parcial, limpa o change tracker) e grava o erro no registro;
   - commit.
3. **Não inseriu** → a chave já existe e está commitada:
   - hash diferente → 422 `idempotency.key_reused`;
   - hash igual → replay da resposta armazenada.
- **Concorrência com a mesma chave**: a segunda transação bloqueia no `INSERT` (índice único da PK) até a primeira commitar e então cai no caminho de replay. Não existe estado "em andamento" exposto ao cliente nem retry interno. Se a primeira der rollback, a segunda insere e processa normalmente.
- **Erros inesperados (5xx)**: a transação inteira faz rollback, inclusive a chave → o cliente pode repetir. Erros de validação (400) acontecem antes do handler e também não são gravados.
- **Hash da requisição canônica** (`bookId|userId`), não dos bytes brutos: espaços, ordem de campos ou capitalização do JSON não transformam um retry legítimo em 422.
- **Replay**: sucesso devolve o snapshot do `LoanResponse` original + `Location`; erro é reconstruído a partir do `Error` gravado (mesmo status e `code`, com `correlationId`/`traceId` da requisição atual) — "resultado semanticamente equivalente".
- Rejeições são reproduzidas mesmo que o estado tenha mudado (ex.: um exemplar foi devolvido depois): a chave representa uma tentativa, e a resposta de uma tentativa não muda. Uma nova tentativa usa uma nova chave.

### 4. Organização
- `Infrastructure/Idempotency`: `IdempotencyKey` (validação do header), `IdempotencyRecord` + configuração EF, `IdempotencyStore` (`TryBeginAsync` → `Begun | Replay | Mismatch`, `CompleteAsync`), hashing canônico.
- `Features/Loans`: `IdempotentCreateLoanHandler` orquestra transação, savepoint e store; o `CreateLoanHandler` passa a receber a transação já aberta (só regra de negócio).
- Migration `AddIdempotencyKeys`.

## Fora de escopo (documentado no ADR)
- **Retenção/limpeza**: chaves ficam guardadas indefinidamente nesta versão. Evolução: job agendado (`CronJob`/`pg_cron`) removendo registros com mais de 24h, apoiado no índice de `created_at`.
- **Escopo por cliente**: a chave é única por endpoint, não por cliente autenticado (não há autenticação). Com JWT, a PK passa a incluir o `sub`.
- Idempotência em outros endpoints (devolver e cancelar já são naturalmente idempotentes no efeito: a segunda chamada recebe 409 e não altera estado).

## Testes obrigatórios (integração)
Os helpers de teste existentes passam a enviar uma chave nova por requisição.

| Cenário | Esperado |
|---|---|
| Sem header / header malformado | 400 `idempotency.key_missing` / `idempotency.key_invalid` |
| Mesma chave repetida em sequência | 201 nas duas; mesmo `id`; segunda com `Idempotent-Replayed: true`; disponibilidade −1; 1 empréstimo; 1 `LoanCreated` |
| Mesma chave, 10 requisições em paralelo em dois hosts | todas 201 com o mesmo `id`; 9 marcadas como replay; 1 empréstimo; disponibilidade −1 |
| Mesma chave, outro `bookId` | 422 `idempotency.key_reused` |
| Rejeição gravada: chave K → 409 `book.unavailable`; exemplar liberado; K de novo | 409 replay; nova chave → 201 |
| `loan.duplicate_active` com chave (caminho do savepoint) e replay | 409 nas duas; disponibilidade inalterada |
| Chaves diferentes, mesmo leitor e livro | segunda → 409 `loan.duplicate_active` (idempotência não mascara regra de negócio) |

## Critérios de aceite
- `dotnet build -c Release` sem warnings; `dotnet test` verde (incluindo todos os testes de empréstimo e concorrência existentes).
- Compose: repetir um `POST /loans` com a mesma chave via `curl` devolve o mesmo empréstimo com `Idempotent-Replayed: true`.
- ADR atualizado.

## Commits
1. `feat: add idempotency key store`
2. `feat: require idempotency key on loan creation`
3. `test: cover idempotent loan creation scenarios`
