# Library Loans API

API de catálogo e empréstimos de uma biblioteca, feita em .NET 10 com PostgreSQL e Redis.

O foco é corretude: dois pedidos ao mesmo tempo nunca emprestam o mesmo último exemplar, repetir um pedido não cria empréstimo duplicado, e toda mudança fica registrada numa trilha de auditoria.

O raciocínio completo de cada decisão está no [ADR 0001](docs/adr/0001-architecture-overview.md). O enunciado original está em [docs/challenge.md](docs/challenge.md).

## Testando em 2 minutos

Com o Docker rodando:

```bash
cp .env.example .env
```

```bash
docker compose up --build
```

Isso sobe PostgreSQL, Redis, um painel de observabilidade, aplica as migrations e inicia a API.

Abra http://localhost:8080/scalar para usar a API pelo navegador. Um roteiro rápido:

1. `POST /books` com `{"title":"Clean Code","author":"Robert C. Martin","isbn":"9780132350884","totalCopies":1}`
2. `POST /users` duas vezes, com e-mails diferentes
3. `POST /loans` com o livro e o primeiro usuário, enviando o cabeçalho `Idempotency-Key` com qualquer valor (por exemplo, um UUID). Resultado: **201**
4. Repita o mesmo pedido com a mesma chave. Resultado: o mesmo empréstimo, com `Idempotent-Replayed: true`
5. Peça o livro para o segundo usuário. Resultado: **409** `book.unavailable`
6. `POST /loans/{id}/return` e depois `GET /books/{id}/history` para ver o histórico preservado

Em http://localhost:18888 (Aspire Dashboard) dá para ver os traces, logs e métricas de cada requisição.

## 1. Pré-requisitos e execução

- .NET SDK 10
- Docker Desktop (ou Docker Engine com Compose v2)

O `docker compose up --build` acima é tudo o que é preciso. Para rodar só a API fora do Docker (`dotnet run --project src/Library.Api`), configure as connection strings por variável de ambiente ou user-secrets. A aplicação não sobe sem elas.

## 2. Migrations e testes

As migrations ficam em `src/Library.Api/Infrastructure/Persistence/Migrations`. Elas nunca rodam no startup da API: um container separado (`migrator`) aplica as migrations antes de a API subir. Com várias réplicas, isso evita que todas tentem migrar o banco ao mesmo tempo.

Criar uma migration nova:

```bash
dotnet tool restore
```

```bash
dotnet ef migrations add <Nome> --project src/Library.Api --output-dir Infrastructure/Persistence/Migrations
```

Aplicar:

```bash
docker compose run --rm migrator
```

Rodar os testes (precisam do Docker, porque sobem PostgreSQL e Redis de verdade):

```bash
dotnet test --solution Library.slnx
```

Os testes cobrem as regras de negócio, os fluxos completos da API e os cenários pedidos no enunciado: 20 pedidos simultâneos pelo último exemplar (exatamente um vence), idempotência, preservação do histórico após devolução e cancelamento, e o comportamento com o Redis fora do ar.

## 3. Variáveis de ambiente

| Variável | Para que serve |
|---|---|
| `ConnectionStrings__Postgres` | Conexão com o PostgreSQL (obrigatória) |
| `ConnectionStrings__Redis` | Conexão com o Redis (obrigatória) |
| `Loans__DefaultPeriodDays` | Prazo do empréstimo em dias (padrão 14) |
| `Cache__BookTtlSeconds` | Por quanto tempo um livro fica no cache (padrão 60s) |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | Para onde enviar traces, métricas e logs (opcional) |

Nenhum segredo está no repositório. O Docker Compose lê as credenciais do arquivo `.env`, que fica fora do Git (o `.env.example` só tem valores de exemplo).

Cabeçalhos aceitos:

| Cabeçalho | Para que serve |
|---|---|
| `Idempotency-Key` | Obrigatório em `POST /loans`. Repetir a mesma chave devolve a resposta original |
| `X-Actor-Id` | Quem está fazendo a operação, registrado na auditoria (`anonymous` se não vier) |
| `X-Correlation-Id` | Identificador para rastrear a requisição nos logs (gerado se não vier) |

## Endpoints

| Método | Rota | O que faz |
|---|---|---|
| POST | `/books` | Cadastra um livro (ISBN validado e único) |
| GET | `/books` | Lista livros, com busca e paginação |
| GET | `/books/{id}` | Consulta um livro |
| PATCH | `/books/{id}` | Altera título, autor ou quantidade |
| DELETE | `/books/{id}` | Desativa o livro |
| GET | `/books/{id}/availability` | Mostra quantos exemplares estão livres |
| GET | `/books/{id}/history` | Histórico de empréstimos do livro |
| POST | `/users` | Cadastra um leitor |
| GET | `/users/{id}` | Consulta um leitor |
| GET | `/users/{id}/loans` | Histórico de empréstimos do leitor |
| POST | `/loans` | Empresta um exemplar |
| GET | `/loans/{id}` | Consulta um empréstimo |
| POST | `/loans/{id}/return` | Devolve |
| POST | `/loans/{id}/cancel` | Cancela (motivo opcional) |
| GET | `/audit-events` | Consulta a trilha de auditoria |
| GET | `/health/live`, `/health/ready` | Health checks |

Mudanças em relação à lista sugerida no enunciado:

- `DELETE /books/{id}` desativa o livro em vez de apagar. Ele continua consultável e o histórico é mantido.
- `PATCH /books/{id}` altera só os campos enviados. O ISBN não muda depois do cadastro.
- `POST /loans` exige o cabeçalho `Idempotency-Key`.
- Incluí `GET /users/{id}` e `GET /loans/{id}`, e o cancelamento aceita um motivo.

Os erros seguem o padrão Problem Details, sempre com um `code` legível (por exemplo `book.unavailable`) e o `correlationId` da requisição.

## Observabilidade

Logs estruturados em JSON, traces e métricas via OpenTelemetry, visíveis no Aspire Dashboard (http://localhost:18888). As métricas de negócio pedidas no enunciado são `library.loans.created`, `library.loans.unavailable`, `library.loans.idempotent_replays` e `library.loans.request.duration`.

## 4. Decisões de concorrência, idempotência, cache e auditoria

**Concorrência.** Quem decide se ainda há exemplar é o banco, numa única instrução: "desconte um exemplar deste livro, desde que ainda reste algum". Se dois pedidos chegam juntos, o PostgreSQL executa um de cada vez nessa linha, e o segundo já encontra zero exemplares. Por isso o resultado é o mesmo com 2 ou 11 réplicas da API. Também há uma restrição no banco que impede o estoque de ficar negativo. Considerei lock distribuído no Redis e isolamento Serializable, mas os dois trazem mais complexidade (retries, dependência do Redis) sem ganho aqui.

**Idempotência.** A chave enviada em `Idempotency-Key` é gravada no PostgreSQL na mesma transação do empréstimo. Se o pedido for repetido, a API devolve a resposta que já tinha dado. Se a mesma chave vier com outro pedido, a API recusa (422). Guardar a chave no Redis seria mais rápido, mas aí a chave e o empréstimo poderiam ficar dessincronizados numa falha.

**Cache.** As consultas de livro e de disponibilidade usam o Redis. Sempre que um empréstimo, devolução ou alteração muda o livro, a entrada do cache é apagada logo depois de gravar no banco. Se o Redis cair, a API continua funcionando direto no banco. O cache nunca decide nada: a decisão de emprestar é sempre do PostgreSQL. A contrapartida é que, num caso raro de leitura e escrita simultâneas, o cache pode mostrar um valor desatualizado por até 60 segundos.

**Auditoria.** Cada mudança de negócio (livro criado, empréstimo devolvido etc.) grava um evento com quem fez, quando, o que mudou e o identificador da requisição, na mesma transação da mudança. O próprio banco proíbe alterar ou apagar esses eventos. Dados pessoais dos leitores ficam fora da trilha, porque ela não pode ser apagada e a LGPD garante o direito de exclusão.

## 5. Limitações conhecidas e evolução para produção

- Não há autenticação: o `X-Actor-Id` é aceito como veio. Em produção, o ator viria de um token JWT.
- As chaves de idempotência não expiram. O próximo passo é uma rotina que apague as com mais de 24 horas.
- A trilha de auditoria cresce sem limite. Em produção, particionaria a tabela por mês e trocaria a paginação por offset por paginação por cursor.
- Os exemplares são um contador. Para rastrear cada exemplar físico, seria preciso uma tabela de exemplares.
- Não há como anonimizar um leitor. Seria um endpoint novo, registrado na auditoria.
- Com mais de 11 réplicas, as conexões passariam do limite padrão do PostgreSQL. A solução seria um pool externo, como o PgBouncer.
- Os manifests de Kubernetes são o mínimo pedido. Faltam autoscaling, Ingress com TLS e políticas de rede.
- Há métricas, mas não dashboards nem alertas prontos.

## 6. Kubernetes

Os manifests estão em [`deploy/k8s/`](deploy/k8s): Deployment com 2 réplicas, Service, ConfigMap, um Secret de exemplo (sem valores reais) e um Job que aplica as migrations antes da API subir.

Ordem para aplicar num cluster:

```bash
kubectl create secret generic library-api-secrets --from-literal=ConnectionStrings__Postgres="<postgres>" --from-literal=ConnectionStrings__Redis="<redis>"
```

```bash
kubectl apply -f deploy/k8s/configmap.yaml -f deploy/k8s/migration-job.yaml
```

```bash
kubectl apply -f deploy/k8s/deployment.yaml -f deploy/k8s/service.yaml
```

As imagens nos manifests (`library-api:1.0.0` e `library-migrator:1.0.0`) são nomes de exemplo. Antes de usar, é preciso publicar as imagens num registry.

Sobre as probes: a de liveness não depende do banco nem do Redis, então uma queda deles nunca reinicia a API. A de readiness tira a réplica do balanceamento só se o PostgreSQL estiver fora. Sem o Redis a réplica continua atendendo, só sem cache.

## 7. Por que funciona com 2 a 11 réplicas

As réplicas não guardam nada em memória que influencie uma decisão. Tudo o que precisa ser coordenado fica no PostgreSQL:

- o último exemplar é decidido pelo banco, como descrito na seção 4;
- um leitor não consegue dois empréstimos ativos do mesmo livro, por uma restrição única no banco;
- a mesma `Idempotency-Key` enviada a réplicas diferentes gera um único empréstimo;
- devolver duas vezes ao mesmo tempo devolve o exemplar uma vez só;
- o cache é um só, compartilhado por todas as réplicas;
- cada réplica usa no máximo 8 conexões, então 11 réplicas ficam abaixo do limite padrão de 100 do PostgreSQL;
- as migrations rodam uma única vez, pelo Job, e não em cada réplica.
