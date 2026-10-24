# Parte 06 — Empréstimos

## Objetivo
Implementar o coração do desafio: emprestar, devolver e cancelar com integridade sob concorrência entre réplicas, consultar disponibilidade e histórico, e provar por teste automatizado que duas requisições simultâneas nunca emprestam o mesmo último exemplar.

## Escopo

### 1. Domínio (`Library.Domain/Loans`)

#### `LoanStatus`
`Active`, `Returned`, `Cancelled`. `Returned` e `Cancelled` são terminais. "Atrasado" não é estado: é derivado (`Active` e `DueAt < agora`).

#### `Loan` (entidade)
- Campos: `Id` (v7), `BookId`, `UserId`, `Status`, `LoanedAt`, `DueAt`, `ReturnedAt?`, `CancelledAt?`, `CancellationReason?` (≤ 500).
- `Loan.Create(bookId, userId, now, loanPeriod)`: `DueAt = now + loanPeriod`; período precisa ser positivo.
- `Return(now)` → `Result<FieldChange>`: só a partir de `Active`; senão `Error.Conflict("loan.not_active")`.
- `Cancel(now, reason?)` → `Result<FieldChange>`: só a partir de `Active`; motivo opcional até 500 caracteres (`loan.cancellation_reason_invalid`).
- `IsOverdue(now)`.
- Um empréstimo nunca é removido: devolução e cancelamento só mudam o status, preservando que ele existiu.

### 2. Persistência
- Tabela `loans` com FKs `book_id → books` e `user_id → users` (`ON DELETE RESTRICT` — histórico não some).
- Índices: `(book_id, loaned_at)`, `(user_id, loaned_at)`.
- **Índice único parcial** `ux_loans_active_book_user (book_id, user_id) WHERE status = 'Active'`: um leitor não pode ter dois empréstimos ativos do mesmo livro, nem sob concorrência.
- CHECKs: status válido; `returned_at` preenchido ⇔ `Returned`; `cancelled_at` preenchido ⇔ `Cancelled`; `due_at > loaned_at`.
- Migration `AddLoans`.

### 3. Criar empréstimo — caminho quente, update condicional atômico
Uma transação (Read Committed):
1. Usuário existe? Senão 404 `user.not_found`.
2. `UPDATE books SET available_copies = available_copies - 1 WHERE id = @bookId AND is_active AND available_copies > 0` via `ExecuteUpdateAsync`.
3. 0 linhas afetadas → rollback e diagnóstico com uma leitura simples: livro inexistente → 404 `book.not_found`; inativo → 409 `book.inactive`; senão → 409 `book.unavailable`.
4. Insere o `Loan` e o evento `LoanCreated` (`{ bookId, userId, dueAt }`); `SaveChanges`; commit.
5. Violação do índice único parcial → rollback (desfaz o decremento) e 409 `loan.duplicate_active`.

- **Por que é correto entre réplicas**: a segunda transação concorrente espera o lock da linha do livro; quando a primeira commita, o PostgreSQL reavalia o `WHERE` sobre a versão nova (EvalPlanQual), vê `available_copies = 0` e não atualiza. Nenhum lock distribuído, nenhum retry, nenhum Serializable. O `CHECK (available_copies BETWEEN 0 AND total_copies)` é a última defesa.
- `DueAt` calculado pelo servidor a partir de `Loans:DefaultPeriodDays` — o cliente não escolhe o prazo.
- Rejeições não geram evento de auditoria (nada mudou); são registradas em log estruturado (e viram métrica na Parte 09).

### 4. Devolver e cancelar — lock do empréstimo + regra no domínio
Uma transação:
1. `SELECT ... FROM loans WHERE id = @id FOR UPDATE` → 404 `loan.not_found`.
2. `loan.Return(now)` / `loan.Cancel(now, reason)` no domínio → 409 `loan.not_active` se já terminal.
3. `UPDATE books SET available_copies = available_copies + 1 WHERE id = @bookId AND available_copies < total_copies`; 0 linhas indica estado inconsistente → exceção (500, rollback) em vez de mascarar o problema.
4. Evento `LoanReturned` / `LoanCancelled` (`{ status: { from, to }, reason? }`); commit.

- **Por quê**: duas devoluções simultâneas serializam no lock da linha; a segunda vê `Returned` e recebe 409, então o estoque nunca é incrementado duas vezes. A regra de transição fica no domínio (testável) e o diff da auditoria é exato.
- Ordem de locks sempre `loan → book`; criação só trava `book` e insere uma linha nova → sem ciclo, sem deadlock.

### 5. Endpoints

| Método | Rota | Sucesso | Erros |
|---|---|---|---|
| POST | `/loans` | 201 + `Location` + `LoanResponse` | 400; 404 `book.not_found`/`user.not_found`; 409 `book.unavailable`/`book.inactive`/`loan.duplicate_active` |
| GET | `/loans/{id}` | 200 `LoanResponse` | 404 `loan.not_found` |
| POST | `/loans/{id}/return` | 200 `LoanResponse` | 404; 409 `loan.not_active` |
| POST | `/loans/{id}/cancel` | 200 `LoanResponse` | 400; 404; 409 `loan.not_active` |
| GET | `/books/{id}/availability` | 200 `AvailabilityResponse` | 404 `book.not_found` |
| GET | `/books/{id}/history` | 200 página de `LoanResponse` | 404 `book.not_found` |
| GET | `/users/{id}/loans` | 200 página de `LoanResponse` (filtro `status` opcional) | 404 `user.not_found` |

- `CreateLoanRequest { bookId, userId }`; `CancelLoanRequest { reason? }`.
- `LoanResponse { id, bookId, userId, status, loanedAt, dueAt, returnedAt, cancelledAt, cancellationReason, isOverdue }` — `isOverdue` calculado com o `TimeProvider`.
- `AvailabilityResponse { bookId, totalCopies, availableCopies, loanedCopies, isActive, isAvailable }` — leitura direta do PostgreSQL nesta parte (cache na Parte 08).
- Históricos incluem todos os status (ativos, devolvidos e cancelados), mais recentes primeiro, paginados.
- `Idempotency-Key` em `POST /loans` chega na Parte 07.

### 6. Logs estruturados
`LoggerMessage` para empréstimo criado, rejeitado (com motivo), devolvido e cancelado, sempre com `loanId`/`bookId`/`userId` — nunca nome ou e-mail.

## Fora de escopo
Idempotência (07), cache (08), métricas (09), renovação de empréstimo, multas, limite de empréstimos por leitor.

## Pacotes novos (CPM)
`Microsoft.Extensions.TimeProvider.Testing` (testes).

## Testes obrigatórios

### Unitários (`Loan`)
- `Create` define `DueAt = LoanedAt + período`; período não positivo é rejeitado.
- `Return` a partir de `Active` define `ReturnedAt`; segunda devolução → `loan.not_active`.
- `Cancel` a partir de `Active` guarda o motivo; cancelar após devolver → `loan.not_active`; motivo > 500 → erro.
- `IsOverdue`: falso antes do prazo, verdadeiro depois, falso se devolvido.

### Integração — fluxo
| Cenário | Esperado |
|---|---|
| Emprestar | 201; `dueAt = loanedAt + 14 dias`; disponibilidade −1; `LoanCreated` com ator e correlação |
| Livro/usuário inexistente | 404 com o código correspondente; disponibilidade inalterada |
| Livro inativo | 409 `book.inactive` |
| Sem exemplares | 409 `book.unavailable` |
| Mesmo leitor, mesmo livro, empréstimo ativo | 409 `loan.duplicate_active`; disponibilidade inalterada |
| Devolver | 200 `Returned`; disponibilidade restaurada; `LoanReturned` |
| Cancelar com motivo | 200 `Cancelled`; disponibilidade restaurada; `LoanCancelled` com motivo |
| Devolver duas vezes / cancelar após devolver | 409 `loan.not_active` |
| **Preservação de histórico** | após devolução e cancelamento, `/books/{id}/history` e `/users/{id}/loans` listam os empréstimos com os status finais e os eventos de auditoria continuam lá |
| Atraso (`FakeTimeProvider` avançado além do prazo) | `isOverdue = true` |

### Integração — concorrência (o teste central do desafio)
Dois hosts independentes (`ApiFactory` + `WithWebHostBuilder`, cada um com seu DI e pool de conexões) simulam duas réplicas; todas as requisições são liberadas ao mesmo tempo por um sinal de largada.

| Cenário | Esperado |
|---|---|
| 1 exemplar, 20 leitores distintos em paralelo | exatamente 1 × 201 e 19 × 409 `book.unavailable`; `available_copies = 0`; 1 linha em `loans`; 1 `LoanCreated` |
| 3 exemplares, 20 leitores em paralelo | exatamente 3 × 201; `available_copies = 0`; 3 empréstimos |
| 2 devoluções simultâneas do mesmo empréstimo | 1 × 200 e 1 × 409; disponibilidade incrementada uma única vez |
| Mesmo leitor, 2 pedidos simultâneos do mesmo livro (2 exemplares) | 1 × 201 e 1 × 409 `loan.duplicate_active`; `available_copies = 1` |

## Critérios de aceite
- `dotnet build -c Release` sem warnings; `dotnet test` verde.
- Compose: fluxo emprestar → disponibilidade → devolver → histórico via HTTP; spans SQL do `UPDATE` condicional visíveis no dashboard.
- ADR atualizado: estratégia de devolução/cancelamento, índice único parcial, ordem de locks, rejeições fora da auditoria.

## Nota sobre `BookTests`
O decremento de disponibilidade acontece no banco por design (update condicional), então `Book` não ganha um método de empréstimo. O teste unitário de "reduzir abaixo do emprestado" continua simulando o estado persistido por reflection — e o cenário real é coberto pelo teste de integração.

## Commits
1. `feat: add loan domain model`
2. `feat: add loan creation with atomic availability decrement`
3. `feat: add loan return and cancellation`
4. `feat: add availability and loan history queries`
5. `test: add concurrency tests for last copy and double return`
