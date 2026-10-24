# Parte 03 — Persistência

## Objetivo
Deixar pronta a base de dados para as features: `DbContext` com convenções do PostgreSQL, pipeline de migrations versionadas aplicadas fora das réplicas (bundle + serviço `migrator`), telemetria do banco e fixture de testes com schema migrado. Nenhuma entidade de negócio nesta parte — elas chegam a partir da Parte 04 já com o caminho pavimentado.

## Escopo

### 1. DbContext e convenções
- `LibraryDbContext` em `src/Library.Api/Infrastructure/Persistence/`.
- Configurações de entidade via `IEntityTypeConfiguration<T>` aplicadas com `ApplyConfigurationsFromAssembly`.
- `UseSnakeCaseNamingConvention()` (`EFCore.NamingConventions`): tabelas, colunas, índices e constraints em snake_case.
- `ConfigureConventions`:
  - enums persistidos como `text` (`HaveConversion<string>()`), legíveis em consultas e auditáveis;
  - `string` sem tamanho explícito é proibida por convenção de revisão — cada configuração define `HasMaxLength`.
- Datas como `DateTimeOffset` em UTC → `timestamptz` (mapeamento nativo do Npgsql; offset diferente de zero é rejeitado pelo provider).
- Registro com `AddDbContext<LibraryDbContext>` usando `ConnectionStringsOptions.Postgres`.
- **Sem `EnableRetryOnFailure`**: a estratégia de retry do EF conflita com transações explícitas (a Parte 06 usa uma transação cobrindo update atômico + loan + auditoria + idempotência). Falha transitória vira erro para o cliente, que pode repetir com segurança graças ao `Idempotency-Key`.

### 2. Migrations
- Migrations em `src/Library.Api/Infrastructure/Persistence/Migrations/`, versionadas.
- `dotnet-ef` como ferramenta local (`.config/dotnet-tools.json`), versão alinhada ao EF Core.
- `LibraryDbContextFactory : IDesignTimeDbContextFactory<LibraryDbContext>` para que tooling e bundle não dependam do host da aplicação (que valida Redis e demais options no startup).
- Migration inicial `Baseline` vazia: valida o pipeline ponta a ponta e cria `__EFMigrationsHistory`. A Parte 04 adiciona a primeira migration com tabelas.

### 3. Aplicação das migrations — nunca pelas réplicas
- Nenhuma migração no startup da API, em nenhum ambiente: com 2–11 réplicas, migrar no boot gera corrida e acopla deploy de schema a deploy de código.
- Dockerfile ganha o estágio `migrator`:
  - no estágio de build: `dotnet tool restore` + `dotnet ef migrations bundle --self-contained -r linux-x64 -o /app/efbundle`;
  - imagem final `runtime-deps`, usuário não-root, `ENTRYPOINT ["./efbundle"]`.
- Compose: serviço `migrator` (`build.target: migrator`) recebe a connection string por variável de ambiente e roda `--connection`; a `api` passa a depender de `migrator: service_completed_successfully`.
- Kubernetes (Parte 10) reutiliza a mesma imagem como `Job`.
- EF Core 9+ adquire lock de migração no banco, então execuções concorrentes do bundle não corrompem o schema.
- Desenvolvimento local fora do Compose: `dotnet ef database update` (documentado no README).

### 4. Telemetria do banco
- `Npgsql.OpenTelemetry`: `AddNpgsql()` nos traces (um span por comando SQL) e `AddMeter("Npgsql")` nas métricas (pool, comandos).
- Instrumentação específica do EF Core não é adicionada (pacote ainda em beta); os spans do Npgsql cobrem os comandos gerados pelo EF.

### 5. Testes: fixture e isolamento
- `ApiFactory.InitializeAsync` aplica as migrations no container (`Database.MigrateAsync`) uma vez por execução, exercitando as mesmas migrations do bundle.
- **Isolamento por dados, sem reset de banco**: cada teste cria seus próprios registros (IDs v7, ISBNs/e-mails únicos) e só faz asserções sobre eles. Isso mantém os testes paralelos e rápidos; um reset global (Respawn) exigiria serializar todas as classes de teste. Listagens são verificadas com "contém", nunca com contagem total.
- Helper `ApiFactory.CreateDbContext()` para asserções diretas no banco (ex.: contar loans no teste concorrente da Parte 06).

## Fora de escopo
Entidades e tabelas (Parte 04+), auditoria, cache.

## Pacotes novos (CPM)
`Microsoft.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.Design` (PrivateAssets all), `Npgsql.EntityFrameworkCore.PostgreSQL`, `EFCore.NamingConventions`, `Npgsql.OpenTelemetry`. Ferramenta local `dotnet-ef`.

## Testes obrigatórios (integração)
| Cenário | Esperado |
|---|---|
| Após a fixture migrar o banco | `GetPendingMigrationsAsync()` vazio |
| Modelo atual vs. migrations | `Database.HasPendingModelChanges()` é `false` — falha se alguém alterar o modelo e esquecer a migration |
| Todos os testes da Parte 02 | continuam verdes |

## Critérios de aceite
- `dotnet build -c Release` sem warnings; `dotnet test` verde.
- `docker compose up --build`: `migrator` executa e termina com código 0 antes da `api` subir; `__EFMigrationsHistory` contém `Baseline`.
- `docker build --target migrator` funciona no CI.
- Spans SQL do Npgsql são validados no Aspire Dashboard na Parte 04, com o primeiro endpoint real (health fica fora dos traces).
- README: como criar migration, aplicar localmente e via Compose.
- ADR atualizado: sem migração no startup (remove a flag de Development citada antes), sem retry do EF, isolamento de testes por dados.

## Commits
1. `feat: add ef core persistence with npgsql and snake_case conventions`
2. `feat: add migration bundle and migrator service`
3. `test: apply migrations in integration fixture and guard pending model changes`
