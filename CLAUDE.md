# CLAUDE.md — convenções do repositório

API de empréstimos de biblioteca (.NET 10, PostgreSQL, Redis). Enunciado em `docs/challenge.md`, decisões em `docs/adr/`, trabalho dividido em partes em `docs/specs/`.

## Fluxo de trabalho
- Cada parte começa lendo `docs/specs/NN-*.md` e o ADR `docs/adr/0001-architecture-overview.md`. Não implemente fora do escopo da spec.
- Ao terminar uma parte: `dotnet build -c Release` sem warnings + `dotnet test` verde, e um commit Conventional Commits (`feat:`, `fix:`, `test:`, `chore:`, `docs:`).
- Mudou uma decisão? Atualize o ADR na mesma parte.

## Estrutura
- `src/Library.Domain`: entidades e regras puras. Sem EF, sem ASP.NET, sem I/O.
- `src/Library.Api`: endpoints, handlers, EF Core, Redis, telemetria. Organizado por feature: `Features/{Books,Users,Loans,Audit}`; transversais em `Infrastructure/`.
- `tests/Library.UnitTests`: regras do domínio. `tests/Library.IntegrationTests`: API + PostgreSQL/Redis reais (Testcontainers).

## Código
- Respeitar SOLID e Clean Code: classes e métodos pequenos com responsabilidade única, nomes que revelam intenção, dependências por abstração quando houver mais de uma implementação ou necessidade de teste.
- Sem comentários dentro de classes ou métodos. Documentação apenas via XML doc (`/// <summary>`) na declaração de classes e métodos, quando agregar contexto que o nome não expressa.
- Código, identificadores e mensagens de log em inglês; docs em PT-BR.
- Minimal APIs com `MapGroup` + `TypedResults`. Handlers são classes simples injetadas — sem MediatR.
- Async de ponta a ponta e `CancellationToken` propagado.
- Tempo via `TimeProvider` injetado (nunca `DateTime.UtcNow`/`Now`); tudo UTC, colunas `timestamptz`.
- IDs: `Guid.CreateVersion7()`.
- Erros: Problem Details com extensão `code` (ex.: `book.unavailable`) e `correlationId`. 400 validação, 404, 409 conflito de estado/regra, 422 Idempotency-Key reutilizada com outro payload.
- Disponibilidade e criação de empréstimo decididas no PostgreSQL (update condicional atômico). Redis é só cache, nunca fonte de verdade.
- Auditoria explícita nos handlers, na mesma transação da mudança.
- Pacotes: Central Package Management — versões só em `Directory.Packages.props`, nunca no `.csproj`.
- Testes: xUnit v3 com `Assert` nativo — sem bibliotecas de asserção (Shouldly, FluentAssertions etc.). Não usar MediatR.
- Testes sem comentários (nada de `// Arrange`, `// Act`, `// Assert`); a estrutura arrange/act/assert é expressa por blocos separados por linha em branco e o nome do teste descreve o cenário.
- Nunca versionar segredos; configuração por `appsettings*.json` + variáveis de ambiente.
