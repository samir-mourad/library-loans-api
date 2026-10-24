# Parte 04 — Auditoria + Catálogo

## Objetivo
Entregar o catálogo de livros (criar, consultar, listar, atualizar, desativar) com trilha de auditoria de negócio append-only, gravada na mesma transação de cada mudança. A auditoria nasce aqui porque o catálogo é o primeiro produtor de eventos; empréstimos (Parte 06) reutilizam o mesmo mecanismo.

## Escopo

### 1. Domínio (`Library.Domain`)

#### `Isbn` (value object)
- `Isbn.Create(string raw)` → `Result<Isbn>`. Aceita ISBN-10 ou ISBN-13, com ou sem hífens/espaços; valida dígito verificador (ISBN-10 aceita `X` no final).
- Normaliza para **ISBN-13** (ISBN-10 vira `978` + 9 dígitos + novo dígito verificador). Assim `0-306-40615-2` e `978-0-306-40615-7` são o mesmo livro e colidem na unicidade.
- Inválido → `Error.Validation("isbn.invalid", ...)`.

#### `Book` (entidade)
- Campos: `Id` (v7), `Title` (1–300), `Author` (1–200), `Isbn`, `TotalCopies`, `AvailableCopies`, `IsActive`, `CreatedAt`, `UpdatedAt` (`DateTimeOffset` UTC).
- Invariantes protegidas no domínio (a API também valida o formato, mas o domínio não confia nela):
  - título e autor não vazios e dentro do tamanho;
  - `TotalCopies` entre 1 e 10.000;
  - `0 ≤ AvailableCopies ≤ TotalCopies`.
- Operações:
  - `Book.Create(title, author, isbn, totalCopies, now)` → `Result<Book>`, com `AvailableCopies = TotalCopies`.
  - `Update(title?, author?, totalCopies?, now)` → `Result<BookChanges>`: aplica só o que mudou e devolve o diff (`campo → {from, to}`) para a auditoria. Sem mudanças → diff vazio, `UpdatedAt` intacto.
  - Mudança de quantidade: `emprestados = Total − Available`; novo total `< emprestados` → `Error.Conflict("book.copies_below_loaned")`; senão `Available += novo − Total`.
  - Livro inativo não aceita `Update` → `Error.Conflict("book.inactive")`.
  - `Deactivate(now)` → `bool` indicando se mudou (idempotente).
- ISBN é imutável após a criação: identifica o livro e aparece no histórico.

### 2. Auditoria (`Features/Audit`)
- Tabela `audit_events`:

  | coluna | tipo |
  |---|---|
  | `id` | uuid (v7, ordenável) |
  | `entity_type` | text (`Book`, `Loan`, `User`) |
  | `entity_id` | uuid |
  | `action` | text (`BookCreated`, `BookUpdated`, `BookDeactivated`, ...) |
  | `actor` | text |
  | `occurred_at` | timestamptz |
  | `correlation_id` | text |
  | `data` | jsonb |

- Índices: `(entity_type, entity_id, occurred_at)` e `(occurred_at)`.
- **Append-only no banco**: a migration cria uma função e triggers `BEFORE UPDATE OR DELETE` (por linha) e `BEFORE TRUNCATE` (por statement) que lançam exceção. Nem um bug na aplicação apaga a trilha silenciosamente.
- `IAuditTrail.Record(entityType, entityId, action, data)`: adiciona o evento ao `DbContext` atual, preenchendo `actor` (`IActorAccessor`), `correlation_id` (`ICorrelationContext`) e `occurred_at` (`TimeProvider`). Persiste no mesmo `SaveChanges`/transação da mudança.
- Payloads (`data`):
  - `BookCreated`: snapshot `{ title, author, isbn, totalCopies }`;
  - `BookUpdated`: `{ changes: { <campo>: { from, to } } }`;
  - `BookDeactivated`: `{ isActive: { from: true, to: false } }`.
- `GET /audit-events?entityType=&entityId=&action=&from=&to=&page=&pageSize=` → mais recentes primeiro, ordenado por `occurred_at desc, id desc`.

### 3. Escritas administrativas: lock pessimista de linha
- `PATCH` e `DELETE` abrem transação, carregam o livro com `SELECT ... FOR UPDATE`, aplicam a regra no domínio, gravam auditoria e commitam.
- **Por quê** (ADR): são escritas raras que precisam de um diff exato (`from`/`to`) e não podem perder updates concorrentes. O lock serializa com os decrementos atômicos dos empréstimos (que também travam a linha), então a regra "não reduzir abaixo do emprestado" vale sob concorrência.
- O caminho quente (empréstimo, Parte 06) continua com update condicional atômico, sem round-trip de leitura.
- Token otimista `xmin` foi descartado: todo empréstimo altera a linha do livro, e um PATCH concorrente receberia conflitos espúrios.

### 4. Endpoints (`Features/Books`)

| Método | Rota | Sucesso | Erros |
|---|---|---|---|
| POST | `/books` | 201 + `Location` + `BookResponse` | 400 `validation.failed` / `isbn.invalid`; 409 `isbn.duplicate` |
| GET | `/books` | 200 página | 400 |
| GET | `/books/{id}` | 200 `BookResponse` (inclusive inativo) | 404 `book.not_found` |
| PATCH | `/books/{id}` | 200 `BookResponse` | 400; 404; 409 `book.inactive` / `book.copies_below_loaned` |
| DELETE | `/books/{id}` | 204 (idempotente) | 404 |

- Requests: `CreateBookRequest { title, author, isbn, totalCopies }`, `UpdateBookRequest { title?, author?, totalCopies? }` (campo ausente/nulo = não altera), com DataAnnotations validadas pelo `AddValidation()`.
- `BookResponse { id, title, author, isbn, totalCopies, availableCopies, isActive, createdAt, updatedAt }`.
- Listagem: `search` (ILIKE em título/autor), `isbn` (normalizado, exato), `includeInactive` (padrão `false`), `page` (≥ 1), `pageSize` (1–100, padrão 20). Resposta `{ items, page, pageSize, totalCount }`, ordenada por título e depois id.
- ISBN duplicado: a unicidade é garantida pelo índice único; a violação `23505` vira 409 `isbn.duplicate`. Não há "consulta antes de inserir", que teria corrida.
- `DELETE` em livro já inativo → 204 sem novo evento de auditoria.
- Leituras usam `AsNoTracking` com projeção direta para o response.
- Handlers como classes simples por caso de uso (`CreateBookHandler`, `UpdateBookHandler`, `DeactivateBookHandler`, `GetBookHandler`, `ListBooksHandler`); endpoints só traduzem HTTP ↔ handler ↔ `Result`.

### 5. Migration
- `AddCatalogAndAudit`: tabelas `books` e `audit_events`, índice único em `books.isbn`, `CHECK (available_copies BETWEEN 0 AND total_copies)`, `CHECK (total_copies BETWEEN 1 AND 10000)`, índices e triggers append-only.

### 6. OpenAPI
- `AddOpenApi()` nativo; documento em `/openapi/v1.json` e UI Scalar em `/scalar`, apenas em Development.

## Fora de escopo
Disponibilidade e histórico de empréstimos (Parte 06), cache (Parte 08), reativação de livro.

## Pacotes novos (CPM)
`Microsoft.AspNetCore.OpenApi`, `Scalar.AspNetCore`.

## Testes obrigatórios

### Unitários
- `Isbn`: ISBN-13 e ISBN-10 válidos (com hífens/espaços); `X` no ISBN-10; dígito verificador inválido; tamanho inválido; ISBN-10 e seu ISBN-13 equivalente são iguais.
- `Book.Create`: sucesso com `Available = Total`; título/autor vazios ou longos; quantidade fora de 1–10.000.
- `Book.Update`: diff só com campos alterados; sem mudanças → diff vazio; aumentar quantidade aumenta disponíveis; reduzir respeitando os emprestados; reduzir abaixo → `book.copies_below_loaned`; livro inativo → `book.inactive`.
- `Book.Deactivate`: primeira chamada muda; segunda não.

### Integração
| Cenário | Esperado |
|---|---|
| Criar livro com `X-Actor-Id` e `X-Correlation-Id` | 201, `Location`, `availableCopies = totalCopies`; evento `BookCreated` com ator, correlação e snapshot |
| Criar com ISBN já existente (inclusive ISBN-10 equivalente) | 409 `isbn.duplicate` |
| Payload inválido / ISBN com dígito errado | 400 `validation.failed` / `isbn.invalid` |
| GET existente / inexistente | 200 / 404 `book.not_found` |
| Listagem | contém o livro criado; `search` filtra; inativo oculto por padrão e visível com `includeInactive=true` |
| PATCH título | 200; `BookUpdated` com `from`/`to` só do título |
| PATCH quantidade abaixo do emprestado (estado arranjado direto no banco) | 409 `book.copies_below_loaned`, livro inalterado, sem evento |
| PATCH em inativo | 409 `book.inactive` |
| DELETE / DELETE repetido | 204 e `BookDeactivated`; segundo 204 sem novo evento |
| `UPDATE`/`DELETE` em `audit_events` via SQL | exceção do banco |
| `GET /audit-events?entityType=Book&entityId=` | eventos do livro, mais recentes primeiro |

## Critérios de aceite
- `dotnet build -c Release` sem warnings; `dotnet test` verde.
- Compose: criar e atualizar livro via HTTP; trace no Aspire Dashboard com spans SQL do Npgsql; `/scalar` acessível.
- ADR atualizado: lock pessimista nas escritas administrativas, ISBN normalizado e imutável, DELETE idempotente, paginação por offset (keyset como evolução).

## Commits
1. `feat: add book domain model and isbn value object`
2. `feat: add append-only audit trail`
3. `feat: add catalog endpoints with audit events`
4. `feat: add audit events query and openapi docs`
