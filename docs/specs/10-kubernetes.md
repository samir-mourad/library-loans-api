# Parte 10 — Kubernetes

## Objetivo
Entregar o mínimo que o enunciado pede para Kubernetes — sem provisionar cluster — e explicar no README por que a aplicação permanece correta entre 2 e 11 réplicas.

## Escopo

### 1. Manifests (`deploy/k8s/`, YAML puro, aplicáveis com `kubectl apply -f deploy/k8s/`)

| Arquivo | Conteúdo |
|---|---|
| `configmap.yaml` | `library-api-config`: `ASPNETCORE_ENVIRONMENT=Production`, `Loans__DefaultPeriodDays`, `Cache__BookTtlSeconds`, `OTEL_EXPORTER_OTLP_ENDPOINT`, `OTEL_SERVICE_NAME` (nenhum segredo) |
| `secret.example.yaml` | `library-api-secrets` com as chaves `ConnectionStrings__Postgres` e `ConnectionStrings__Redis` e valores `CHANGE_ME` — exemplo de formato, sem valores reais |
| `deployment.yaml` | `library-api`, 2 réplicas; `envFrom` do ConfigMap; connection strings via `secretKeyRef`; requests/limits; probes |
| `service.yaml` | `ClusterIP`, porta 80 → 8080 |
| `migration-job.yaml` | `Job` com a imagem `migrator` e a connection string do Secret |

**Deployment:**
- Imagem `library-api:1.0.0` (placeholder documentado — trocar pelo registry real).
- Recursos: requests `cpu: 100m`, `memory: 128Mi`; limits `cpu: 500m`, `memory: 256Mi`.
- `livenessProbe` → `GET /health/live` (não depende de PostgreSQL/Redis, então nunca reinicia o pod por queda de dependência).
- `readinessProbe` → `GET /health/ready` (PostgreSQL fora → 503 → pod sai do Service; Redis fora → `Degraded` = 200, pod continua servindo).

**Por que o `Job` de migration, se o enunciado não pede**: é consequência direta de uma decisão já tomada (ADR §7) — as réplicas nunca migram no startup. Sem o Job, um deploy no cluster não teria como criar o schema. Ele é aplicado antes do Deployment.

### 2. README
- Seção **Kubernetes**: como criar o Secret a partir do exemplo (`kubectl create secret generic ...`), ordem de aplicação (Secret/ConfigMap → Job → Deployment/Service).
- Seção **Correção entre 2 e 11 réplicas**, resumindo por que escalar horizontalmente não quebra nada:
  - réplicas stateless: nenhuma decisão depende de memória local;
  - último exemplar: update condicional atômico no PostgreSQL (EvalPlanQual em Read Committed) + `CHECK`;
  - empréstimo duplicado: índice único parcial; idempotência: PK da chave na mesma transação;
  - devolução/cancelamento: lock da linha do empréstimo, ordem de locks sem deadlock;
  - cache: Redis compartilhado, invalidação após commit, sem cache local; Redis nunca decide;
  - conexões: `Maximum Pool Size=8` × 11 réplicas = 88 < 100 (`max_connections` padrão do PostgreSQL);
  - migrations: uma única execução via Job (com lock do EF), nunca por réplica;
  - probes: liveness não reinicia por dependência; readiness tira do balanceamento só quando o PostgreSQL falta.

## Fora de escopo
HPA, PodDisruptionBudget, Ingress, Kustomize/Helm, NetworkPolicy, securityContext adicional — citados no README como evolução para produção.

## Validação
Sem cluster: validação de schema dos YAML (`kubectl apply --dry-run=client -f deploy/k8s/` com o `kubectl` do Docker Desktop, ou equivalente offline) e checagem visual dos campos exigidos.

## Critérios de aceite
- Os 5 arquivos existem, são YAML válidos e cobrem: Deployment, Service, referências a ConfigMap/Secret sem valores reais, requests/limits, liveness e readiness.
- README com as duas seções acima.
- `dotnet build` e `dotnet test` continuam verdes (nenhuma mudança de código esperada).

## Commits
1. `feat: add kubernetes manifests`
2. `docs: explain kubernetes deployment and correctness across replicas`
