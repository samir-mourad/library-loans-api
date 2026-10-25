# Roadmap

Cada parte tem uma spec própria (`NN-nome.md`) com: objetivo, escopo, fora de escopo, contratos, regras, critérios de aceite, testes obrigatórios e mensagem de commit.

| # | Parte | Status |
|---|---|---|
| 01 | Fundação: solution, props, Compose, Dockerfile, CI, docs | ✅ |
| 02 | Transversal: Problem Details, correlação, ator, TimeProvider, logs JSON, OpenTelemetry, health checks | ✅ |
| 03 | Persistência: DbContext, snake_case, migrations, bundle/migrator, fixture Testcontainers migrada | ✅ |
| 04 | Auditoria + Catálogo: `audit_events` append-only, CRUD de livros, soft delete, PATCH de quantidade | ✅ |
| 05 | Usuários: cadastro e consulta, auditoria sem PII | ✅ |
| 06 | Empréstimos: criar (atômico), devolver, cancelar, disponibilidade, histórico; teste concorrente | ✅ |
| 07 | Idempotência: `Idempotency-Key`, replay, 400/422 | ✅ |
| 08 | Cache: cache-aside Redis, invalidação pós-commit, fail-open | ✅ |
| 09 | Métricas de negócio | ✅ |
| 10 | Kubernetes: manifests YAML (Deployment, Service, ConfigMap, Secret de exemplo, Job de migration) | ✅ |
| 11 | README e ADRs finais | ⏳ |
