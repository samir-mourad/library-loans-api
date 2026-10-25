# Parte 11 — README e ADR finais

## Objetivo
Fechar a entrega: README completo com os 5 itens exigidos pelo enunciado, contrato de endpoints documentado (o enunciado permite ajustes "desde que documente") e ADR revisado. Sem código novo.

## Escopo

### 1. README
- Remover o aviso "Em construção" e os TODOs.
- **Seções 1–3** (pré-requisitos/execução, migrations/testes, variáveis): já prontas; apenas revisar.
- **Nova seção "Endpoints"**: tabela com todos os endpoints e, em destaque, os ajustes em relação à lista sugerida:
  - `DELETE /books/{id}` desativa (soft delete, idempotente) em vez de remover;
  - `PATCH /books/{id}` é parcial e o ISBN é imutável;
  - `POST /loans` exige `Idempotency-Key`;
  - adicionados `GET /users/{id}` e `GET /loans/{id}`;
  - `POST /loans/{id}/cancel` aceita `reason` opcional.
- **Seção 4 — Decisões** (concorrência, idempotência, cache, auditoria): um parágrafo curto por tema com o mecanismo e o principal trade-off, linkando o ADR para detalhes. Não duplicar o ADR inteiro.
- **Seção 5 — Limitações conhecidas e evolução para produção**, em lista objetiva:
  - sem autenticação: `X-Actor-Id` é confiado → JWT e `sub` como ator e no escopo da idempotência;
  - chaves de idempotência sem limpeza → job removendo registros > 24h;
  - paginação por offset → keyset na auditoria;
  - corrida residual do cache (≤ TTL) → *delayed double delete* ou snapshot versionado;
  - contador de exemplares, sem rastrear exemplar físico → tabela de exemplares com `SKIP LOCKED`;
  - sem anonimização de usuário → endpoint que anonimiza e registra `UserAnonymized`;
  - crescimento de `audit_events` → particionamento mensal;
  - conexões acima de ~11 réplicas → PgBouncer;
  - Kubernetes mínimo → HPA, PDB, Ingress/TLS, NetworkPolicy, imagens publicadas em registry;
  - sem dashboards/alertas → SLO de p99 do `POST /loans` e alerta da taxa de `library.loans.unavailable`.

### 2. ADR 0001
- Revisão de consistência com o código entregue (nada de Kustomize/HPA/Respawn, que saíram do escopo) e status final.

### 3. Roadmap
- Parte 11 marcada como concluída.

### 4. CI
- Conferir no GitHub Actions que o último push está verde (nunca foi verificado); corrigir se não estiver.

## Fora de escopo
Qualquer mudança de código ou de comportamento (exceto correção que o CI exija).

## Critérios de aceite
- README sem TODOs, cobrindo os 5 itens do enunciado + endpoints + Kubernetes + réplicas.
- ADR consistente com o código.
- CI verde no GitHub.

## Commits
1. `docs: finalize readme with endpoints, decisions and limitations`
2. `docs: review adr for consistency` (se houver ajuste)
