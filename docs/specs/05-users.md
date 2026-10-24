# Parte 05 — Usuários

## Objetivo
Cadastrar e consultar leitores, pré-requisito dos empréstimos (Parte 06). Parte pequena de propósito: reutiliza o padrão do catálogo (domínio com invariantes, handler por caso de uso, auditoria transacional) e introduz um único tema novo — dados pessoais na trilha de auditoria.

## Escopo

### 1. Domínio (`Library.Domain/Users`)

#### `Email` (value object)
- `Email.Create(string? raw)` → `Result<Email>`: trim, até 254 caracteres, formato `local@domínio` com domínio contendo ponto (validação pragmática — a verificação real de um e-mail é enviar uma mensagem, fora do escopo).
- Normalizado para minúsculas (`Ana@Example.com` = `ana@example.com`), para que a unicidade não dependa de capitalização.
- Inválido → `Error.Validation("user.email_invalid", ...)`.

#### `User` (entidade)
- Campos: `Id` (v7), `Name` (1–200, trim), `Email`, `CreatedAt` (`DateTimeOffset` UTC).
- `User.Create(name, email, now)` → `Result<User>`; nome inválido → `user.name_invalid`.
- Sem atualização ou desativação nesta parte (não exigidas pelo enunciado).

### 2. Persistência
- Tabela `users`: `id`, `name varchar(200)`, `email varchar(254)` com índice único `ix_users_email`, `created_at timestamptz`.
- Migration `AddUsers`.
- E-mail duplicado detectado pela violação de unicidade (`23505`) → 409 `user.email_duplicate`, sem consulta prévia (mesmo padrão do ISBN).

### 3. Endpoints (`Features/Users`)

| Método | Rota | Sucesso | Erros |
|---|---|---|---|
| POST | `/users` | 201 + `Location` + `UserResponse` | 400 `validation.failed` / `user.email_invalid` / `user.name_invalid`; 409 `user.email_duplicate` |
| GET | `/users/{id}` | 200 `UserResponse` | 404 `user.not_found` |

- `CreateUserRequest { name, email }` com DataAnnotations; `UserResponse { id, name, email, createdAt }`.
- `GET /users/{id}/loans` fica na Parte 06, junto com os empréstimos.

### 4. Auditoria sem dados pessoais
- Evento `UserCreated` (entidade `User`) registrado na mesma transação do cadastro.
- **O payload não contém nome nem e-mail**: `{ "registered": true }` — o `entity_id` já identifica o usuário.
- **Por quê** (ADR): a trilha é append-only por design (triggers bloqueiam UPDATE/DELETE). Gravar PII nela tornaria impossível atender um pedido de eliminação de dados (LGPD art. 18). A PII fica só em `users`, que pode ser anonimizada; a trilha continua íntegra referenciando o id.
- Pela mesma razão, logs não registram e-mail nem nome — apenas `userId`.

## Fora de escopo
Atualização, desativação e anonimização de usuários (evolução documentada no ADR); autenticação.

## Testes obrigatórios

### Unitários
- `Email`: válido; normalização para minúsculas e trim; inválidos (sem `@`, sem domínio, domínio sem ponto, vazio, nulo, > 254).
- `User.Create`: sucesso com trim do nome; nome vazio ou > 200 → `user.name_invalid`.

### Integração
| Cenário | Esperado |
|---|---|
| Cadastrar usuário | 201, `Location`, e-mail normalizado; evento `UserCreated` com ator e correlação |
| Payload do `UserCreated` | não contém o nome nem o e-mail |
| E-mail já cadastrado com outra capitalização | 409 `user.email_duplicate` |
| Payload inválido / e-mail malformado | 400 `validation.failed` / `user.email_invalid` |
| GET existente / inexistente | 200 / 404 `user.not_found` |

## Critérios de aceite
- `dotnet build -c Release` sem warnings; `dotnet test` verde.
- Compose: cadastrar e consultar usuário via HTTP; endpoint visível no `/scalar`.
- ADR atualizado: PII fora da trilha de auditoria e dos logs.

## Commits
1. `feat: add user domain model and email value object`
2. `feat: add user registration endpoints with pii-free audit`
