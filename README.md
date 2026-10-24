# Odontologia Integrada — Projeto Multiplataforma

Monorepo da plataforma de gestão odontológica. Reúne o **BFF** (NestJS), três APIs
**.NET 10**, uma biblioteca de **mensageria Kafka** compartilhada e a **infraestrutura**
em Docker (Kafka, Keycloak e bancos PostgreSQL).

Cada serviço tem o seu próprio `docker-compose` e o seu próprio README com detalhes
internos. Este documento é o guia **de ponta a ponta**: como subir a infraestrutura e
todas as aplicações.

---

## Arquitetura

```
                         ┌──────────────────────────────┐
       Clientes          │   BFF (NestJS)  :3000        │
   (web / mobile) ─────▶ │  JWT Keycloak + RBAC         │
                         └───────┬───────────────┬──────┘
                                 │               │
                 ┌───────────────▼──┐      ┌─────▼────────────────┐
                 │ Dentist-service  │      │ Employee-service     │
                 │ .NET 10  :5068   │      │ .NET 10      :5070   │
                 └───┬──────────┬───┘      └──────────┬───────────┘
                     │          │                     │
        publica/consome│      │ EF Core             │ EF Core
                     │          ▼                     ▼
        ┌────────────▼───┐  ┌────────────┐      ┌────────────┐
        │ Kafka   :9092  │  │ Postgres   │      │ Postgres   │
        │ (KRaft) :8081  │  │ dentist    │      │ employee   │
        │ Kafka UI       │  │ :5432      │      │ :5434      │
        └────────────────┘  └────────────┘      └────────────┘

        ┌────────────────┐  ┌────────────┐      ┌────────────────┐
        │ Keycloak :8080 │  │ Postgres   │      │ PaymentService │
        │ realm dentist  │  │ keycloak   │      │ .NET 10 :5127  │
        └────────────────┘  │ :5435      │      └───────┬────────┘
                            └────────────┘              ▼
                                                  ┌────────────┐
                                                  │ Postgres   │
                                                  │ payment    │
                                                  │ :5433      │
                                                  └────────────┘
```

| Componente | Stack | Pasta |
|---|---|---|
| BFF | NestJS 11 / TypeScript / Node 20 (pnpm) | `bff/` |
| Dentist-service | .NET 10 / EF Core 10 / PostgreSQL | `Dentist-Service/Dentist-service/` |
| Employee-service | .NET 10 / EF Core 10 / PostgreSQL | `Employee-service/Employee-service/` |
| PaymentService | .NET 10 / EF Core 10 / PostgreSQL | `PaymentService/` |
| Messaging | Biblioteca .NET (Kafka + outbox) | `Messaging/` |
| Kafka | Apache Kafka 4.3.1 (KRaft) + Kafka UI | `kafka/` |
| Keycloak | Keycloak 26 + PostgreSQL | `keycloak/` |
| ELK | Elasticsearch + Logstash + Kibana + Filebeat 8.17 (logs) | `elk/` |
| Observability | Biblioteca .NET (logs JSON ECS + correlation id) | `Observability/` |
| Observabilidade | Prometheus + Grafana + kafka-exporter | `observability/` |

---

## Pré-requisitos

| Ferramenta | Versão | Para quê |
|---|---|---|
| [Docker](https://www.docker.com/) + Docker Compose v2 | recente | Kafka, Keycloak e bancos |
| [.NET SDK](https://dotnet.microsoft.com/download) | **10** | as três APIs |
| [Node.js](https://nodejs.org/) + [pnpm](https://pnpm.io/) | **20** + **9** | BFF (`corepack enable`) |
| [EF Core CLI](https://learn.microsoft.com/ef/core/cli/dotnet) | 10.x | migrations (`dotnet tool install --global dotnet-ef`) |

> No Windows, os comandos com caminho `/opt/kafka/...` precisam de `MSYS_NO_PATHCONV=1` no Git Bash.

---

## Mapa de portas

| Serviço | Host | Observações |
|---|---|---|
| BFF | **3000** | Swagger em `/docs` se `SWAGGER_ENABLED=true` |
| Dentist-service | **5068** | HTTPS `7235`; OpenAPI em `/openapi/v1.json` |
| Employee-service | **5070** | HTTPS `7237`; OpenAPI em `/openapi/v1.json` |
| PaymentService | **5127** | HTTPS `7094`; Swagger só em Development |
| Keycloak | **8080** | realm `dentist` |
| Kafka UI | **8081** | kafbat UI |
| Kibana | **5601** | logs (ver [`elk/README.md`](elk/README.md)) |
| Elasticsearch | **9200** | API HTTP, sem autenticação em dev |
| Kafka broker | **9092** | containers na rede usam `kafka:19092` |
| Postgres Dentist | **5432** | db `dentist_service` |
| Postgres Payment | **5433** | db `payment_service` |
| Postgres Employee | **5434** | db `employee_service` |
| Postgres Keycloak | **5435** | db `keycloak` |
| Prometheus | **9090** | métricas dos serviços (ver `observability/`) |
| Grafana | **3001** | `admin`/`admin` — dashboards versionados |
| kafka-exporter | **9308** | métricas do broker para o Prometheus |

Todas as senhas de desenvolvimento são `postgres` (bancos) e o Kafka é `PLAINTEXT`.

---

## Subindo a infraestrutura (Docker)

Cada pasta tem o seu próprio compose. Rode da **raiz do repositório** com `-f`:

```bash
# 0. (Opcional) ELK: Elasticsearch + Logstash + Filebeat + Kibana (~3 GB de RAM)
(cd elk && docker compose up -d)

# 1. Kafka + Kafka UI + criação dos tópicos
docker compose -f kafka/docker-compose.yaml up -d

# 2. Keycloak + banco do Keycloak (realm `dentist` importado automaticamente)
docker compose -f keycloak/docker-compose.yaml up -d

# 3. Bancos das APIs .NET
docker compose -f Dentist-Service/Dentist-service/docker-compose.yml up -d   # :5432
docker compose -f Employee-service/Employee-service/docker-compose.yml up -d # :5434
docker compose -f PaymentService/docker-compose.yml up -d                    # :5433
```

### Kafka

O `kafka-init` cria os tópicos e termina. A criação automática de tópicos está
desligada (`KAFKA_AUTO_CREATE_TOPICS_ENABLE=false`), então tópicos novos precisam
entrar no comando do `kafka-init`.

Tópicos: `appointments`, `payments`, `health-insurance` e as respectivas `.dlq`.

- Aplicações no host → `localhost:9092`
- Containers na rede `odontologia-network` → `kafka:19092`
- Kafka UI → `http://localhost:8081`

Detalhes de evento, envelope e DLQ em [`kafka/README.md`](kafka/README.md).

### Keycloak

- Console admin: `http://localhost:8080` — usuário `admin`, senha `admin`
- Realm: **`dentist`** (importado de `keycloak/keycloak/dentist-realm.json`)
- Roles de realm: `dentist`, `employ`, `patient`
- Usuários de teste (senha `123456`):

  | Usuário | Role | Acessa no BFF |
  |---|---|---|
  | `dentista1` | `dentist` | `/api/dentist-services` |
  | `funcionario1` | `employ` | `/api/employees` |
  | `paciente1` | `patient` | `/api/appointments` |

> O Keycloak só importa o realm quando ele ainda não existe. Se mudou o
> `dentist-realm.json` (ou já tinha subido antes), recrie o banco para reimportar:
> `docker compose -f keycloak/docker-compose.yaml down -v && docker compose -f keycloak/docker-compose.yaml up -d`.

#### Seeder

Com o Keycloak no ar, o `keycloak/seed.sh` garante — de forma **idempotente**, sem
apagar dados — o realm `dentist`, os clientes, as roles (`dentist`, `employ`,
`patient`) e os usuários de teste acima:

```bash
./keycloak/seed.sh
```

É a alternativa ao `down -v` para aplicar mudanças no realm com o Keycloak já
inicializado. Variáveis opcionais: `KC_SERVER`, `KC_ADMIN_USER`, `KC_ADMIN_PASSWORD`,
`REALM`, `DEFAULT_PASSWORD`.

Obter um token de teste:

```bash
curl -X POST "http://localhost:8080/realms/dentist/protocol/openid-connect/token" \
  -d "grant_type=password&client_id=dentist-app&username=funcionario1&password=123456"
```

---

## Subindo as aplicações

Suba sempre nesta ordem: **Kafka → banco → API**. As três APIs aplicam as
migrations do EF Core automaticamente no boot.

### Dentist-service (`:5068`)

```bash
cd Dentist-Service/Dentist-service
dotnet run                               # aplica migrations e sobe em http://localhost:5068
```

Publica eventos em `appointments` e consome `payments` e `health-insurance`. Para
subir sem Kafka, use `"Kafka": { "Enabled": false }` no `appsettings.Development.json`.

### Employee-service (`:5070`)

```bash
cd Employee-service/Employee-service
dotnet run                               # aplica migrations e sobe em http://localhost:5070
```

### PaymentService (`:5127`)

```bash
cd PaymentService
dotnet run                               # http://localhost:5127
```

O `Program.cs` aplica as migrations automaticamente no boot — assim como o
Dentist-service e o Employee-service. Os logs vão para `logs/payment-service/`
(`Observability:LogDirectory` = `../logs` no `appsettings.Development.json`).

Para usar respostas HTTP falsas, sem PostgreSQL, Kafka ou migrations:

```bash
dotnet run --project Payment-Service/PaymentService.WireMock/PaymentService.WireMock.csproj
# ou, incluindo o banco do serviço real:
docker compose -f Payment-Service/docker-compose.yml up payment-service-wiremock
```

Quando iniciado pelo Docker Compose, o WireMock escuta em
`http://localhost:5128` e cobre `GET /api/payments`,
`GET /api/payments/{id}`, `POST /api/payments` e
`PATCH /api/payments/{id}/status`, incluindo respostas de sucesso, validação
(`400`) e pagamento inexistente (`404`). O identificador usado nas respostas
determinísticas é `00000000-0000-0000-0000-000000000001`. Para escolher outra
porta ao executar localmente, defina `WIREMOCK_PORT`.

### BFF (`:3000`)

```bash
cd bff
cp .env.example .env                     # ajuste KEYCLOAK_* e DENTIST_SERVICE_BASE_URL
pnpm install
pnpm run start:dev                       # http://localhost:3000
```

Para rodar o BFF **no host** apontando para os serviços locais, o `.env` deve conter:

```bash
KEYCLOAK_ISSUER=http://localhost:8080/realms/dentist
KEYCLOAK_JWKS_URI=http://localhost:8080/realms/dentist/protocol/openid-connect/certs
KEYCLOAK_CLIENT_ID=dentist-app
DENTIST_SERVICE_BASE_URL=http://localhost:5068
EMPLOYEE_SERVICE_BASE_URL=http://localhost:5070
SWAGGER_ENABLED=true
```

Via Docker (sobe o BFF com um **mock** do Dentist-service, sem precisar da API real):

```bash
cd bff
docker compose -f docker-compose.dev.yml up
```

Lista completa de variáveis e a matriz de RBAC em [`bff/README.md`](bff/README.md).

---

## Imagem Docker e CD

O workflow **CD** (`.github/workflows/cd.yml`) roda a cada push em `main`, em tags
`v*` e por execução manual. Ele compila e publica no **GitHub Container Registry**
(GHCR) **uma única imagem autocontida**:

```
ghcr.io/<owner>/<repo>/odontologia
```

A imagem sobe, no mesmo container e via `supervisor`:

| Processo | Porta | Acesso | Descrição |
|---|---|---|---|
| BFF (NestJS) | `3000` | **externo** | único ponto de entrada da aplicação (`/health`, `/docs`) |
| Keycloak 26.6 | `8080` | **externo** | login / emissão de tokens; realm `dentist` importado automaticamente |
| Dentist-service | `5068` | interno | API .NET (migrations no boot) |
| Employee-service | `5070` | interno | API .NET (migrations no boot) |
| Payment-service | `5127` | interno | API .NET (migrations no boot) |
| Kafka 4.3 (KRaft) | `9092` | interno | tópicos criados automaticamente no boot |
| Postgres 16 | `5432` | interno | bancos `dentist_service`, `employee_service`, `payment_service` e `keycloak` |

**O BFF é o único ponto de entrada.** Os processos "internos" escutam apenas em
`127.0.0.1`: o BFF os alcança por dentro do container, mas eles não respondem a
chamadas de fora — mesmo que a porta seja publicada com `-p`, a conexão é recusada.
Assim toda requisição passa pela autenticação e pelo RBAC do BFF.

Tags geradas: `latest` (branch `main`), `sha-<commit>`, `main` e, para tags
semver (`v1.2.3`), `1.2.3` e `1.2`. Os processos rodam como usuário não-root `app`.

Build local (na raiz do repositório):

```bash
docker build -t odontologia .
```

Executando sem nenhuma configuração extra:

```bash
docker run --rm \
  -p 3000:3000 -p 8080:8080 \
  -v odonto-pg:/app/pgdata \
  -v odonto-kafka:/app/kafka-data \
  odontologia
```

Os volumes preservam Postgres e Kafka entre execuções; o estado do Keycloak fica
no Postgres. Sem os volumes, a imagem inicia com os bancos e tópicos vazios.

Credenciais padrão (apenas para desenvolvimento):

- Keycloak admin: `admin` / `admin` — console em `http://localhost:8080/admin`
- Usuários de teste do realm `dentist`: `dentista1`, `funcionario1` e
  `paciente1`, senha `123456`
- Postgres: usuário `postgres`, senha `postgres` (sem porta publicada; use
  `docker exec -u app <container> psql -U postgres`)

Teste de ponta a ponta:

```bash
TOKEN=$(curl -s -X POST http://localhost:8080/realms/dentist/protocol/openid-connect/token \
  -d 'grant_type=password' -d 'client_id=dentist-app' \
  -d 'username=dentista1' -d 'password=123456' | jq -r .access_token)

curl -H "Authorization: Bearer $TOKEN" http://localhost:3000/api/dentist-services

# Acesso direto a um microsserviço: recusado (não passa pelo BFF)
curl http://localhost:5127/api/payments   # Connection refused/reset
```

> Use `localhost` (e não `127.0.0.1`) ao obter o token: o emissor padrão é
> `http://localhost:8080/realms/dentist`. Se mudar porta/host, ajuste
> `KEYCLOAK_ISSUER` e `KEYCLOAK_JWKS_URI`.

Acompanhe os processos e os logs:

```bash
docker exec -u app <container> supervisorctl -c /app/supervisord.conf status
docker logs -f <container>   # stdout/stderr de todos os processos
```

Principais variáveis:

| Variável | Padrão na imagem | Usada por |
|---|---|---|
| `KEYCLOAK_ISSUER` | `http://localhost:8080/realms/dentist` | BFF |
| `KEYCLOAK_JWKS_URI` | `http://127.0.0.1:8080/realms/dentist/protocol/openid-connect/certs` | BFF |
| `KEYCLOAK_CLIENT_ID` | `dentist-app` | BFF |
| `KC_BOOTSTRAP_ADMIN_USERNAME` / `KC_BOOTSTRAP_ADMIN_PASSWORD` | `admin` / `admin` | Keycloak |
| `DENTIST_CONNECTION_STRING` | `Host=127.0.0.1;Port=5432;Database=dentist_service;...` | Dentist-service |
| `EMPLOYEE_CONNECTION_STRING` | `Host=127.0.0.1;Port=5432;Database=employee_service;...` | Employee-service |
| `PAYMENT_CONNECTION_STRING` | `Host=127.0.0.1;Port=5432;Database=payment_service;...` | Payment-service |
| `KAFKA_BOOTSTRAP_SERVERS` | `localhost:9092` | Dentist e Payment |
| `KAFKA_ENABLED` | `true` | Dentist e Payment |
| `DENTIST_SERVICE_BASE_URL` | `http://127.0.0.1:5068` | BFF |
| `EMPLOYEE_SERVICE_BASE_URL` | `http://127.0.0.1:5070` | BFF |
| `SWAGGER_ENABLED` | `true` | BFF |
| `CORS_ORIGINS` | vazio | BFF |

As connection strings podem ser sobrescritas para apontar para Postgres externo,
e `KAFKA_BOOTSTRAP_SERVERS` para um Kafka externo. Nesse caso, os serviços
embarcados continuam rodando, mas as aplicações usam o destino informado.

> A infraestrutura embarcada é pensada para desenvolvimento/demonstração e para
> entregar tudo em um único artefato. Para produção, o recomendado é usar
> Postgres, Kafka e Keycloak gerenciados/separados. Reserve pelo menos ~2 GB de
> RAM para o container completo.

> O CD só publica a imagem; ele não faz deploy em nenhum ambiente. Para
> consumir o pacote, ele precisa estar acessível (público ou autenticado com
> `docker login ghcr.io`).

> O target `bff` ainda existe no Dockerfile para desenvolvimento isolado do BFF:
> `docker build --target bff -t bff .`.

---

## Resumo: subir o sistema inteiro

```bash
# Infraestrutura
(cd elk && docker compose up -d)                      # opcional: logs no Kibana :5601
docker compose -f kafka/docker-compose.yaml up -d
docker compose -f keycloak/docker-compose.yaml up -d
docker compose -f Dentist-Service/Dentist-service/docker-compose.yml up -d
docker compose -f Employee-service/Employee-service/docker-compose.yml up -d
docker compose -f PaymentService/docker-compose.yml up -d

# Aplicações (uma por terminal; as migrations são aplicadas no boot)
(cd Dentist-Service/Dentist-service && dotnet run)   # :5068
(cd Employee-service/Employee-service && dotnet run)  # :5070
(cd PaymentService && dotnet run)                     # :5127
(cd bff && pnpm run start:dev)                        # :3000
```

---

## Observabilidade (Prometheus + Grafana)

Stack completa em `observability/`, cobrindo os fluxos de **agendamento** e
**pagamento** de ponta a ponta (negócio, Kafka/outbox/DLQ, HTTP e runtime).

```bash
# Kafka precisa estar no ar primeiro
docker compose -f kafka/docker-compose.yaml up -d
docker compose -f observability/docker-compose.yaml up -d

# Prometheus → http://localhost:9090   |   Grafana → http://localhost:3001 (admin/admin)
```

Para o Prometheus (em container) raspar as apps (no host), suba-as escutando em
**`0.0.0.0`** — o bind padrão de dev (`localhost`) não é acessível de dentro do
container:

```bash
(cd Dentist-Service/Dentist-service && ASPNETCORE_URLS=http://0.0.0.0:5068 dotnet run --no-launch-profile)
(cd PaymentService && ASPNETCORE_URLS=http://0.0.0.0:5127 dotnet run --no-launch-profile)
(cd bff && pnpm run start:dev)
```

Endpoints: `/metrics` e `/health` em cada app. Dashboards e catálogo completo de
métricas em [`observability/README.md`](observability/README.md).

---

## Testes

```bash
# BFF
cd bff && pnpm run test:unit && pnpm run test:int

# Dentist-service
dotnet test Dentist-Service/Dentist-service.sln

# Employee-service
dotnet test Employee-service/Employee-service.sln

# Messaging
dotnet test Messaging/Messaging.Tests/Messaging.Tests.csproj

# Observability
dotnet test Observability/Observability.sln

# PaymentService
dotnet test PaymentService.Tests/PaymentService.Tests.csproj
```

Os testes do BFF usam um JWKS local (`test/support/jwt-test-kit.ts`) — não exigem
um Keycloak real.

---

## Logs (ELK)

BFF, Dentist-service e PaymentService gravam logs JSON (ECS) em `logs/<servico>/`; o Filebeat
os envia ao Elasticsearch e a consulta é feita no Kibana (`http://localhost:5601`). Cada
requisição carrega um `labels.correlation_id` que atravessa BFF, Dentist-service, outbox,
Kafka e PaymentService.

- Como subir e consultar: [`elk/README.md`](elk/README.md)
- Campos, catálogo de eventos e política de dados pessoais: [`elk/docs/logging-standard.md`](elk/docs/logging-standard.md)

---

## Parando / limpando

```bash
(cd elk && docker compose down)                              # add -v para apagar índices e offsets
docker compose -f kafka/docker-compose.yaml down            # add -v para apagar tópicos/dados
docker compose -f keycloak/docker-compose.yaml down -v      # remove o banco do Keycloak (reimporta o realm no próximo up)
docker compose -f Dentist-Service/Dentist-service/docker-compose.yml down -v
docker compose -f Employee-service/Employee-service/docker-compose.yml down -v
docker compose -f PaymentService/docker-compose.yml down -v
```

---

## Troubleshooting

- **Conflito de portas nos bancos.** Cada banco publica uma porta diferente no host:
  Dentist `5432`, Payment `5433`, Employee `5434`, Keycloak `5435`. O Postgres do
  Keycloak fica em `5435` justamente porque `5433` é do PaymentService. Se aparecer
  `port is already allocated`, ajuste a porta publicada no compose correspondente.
- **Realm do Keycloak desatualizado.** O realm só é importado na primeira subida.
  Alterou o JSON? `docker compose -f keycloak/docker-compose.yaml down -v && up -d`.
- **`dotnet ef` não encontrado.** Necessário apenas para criar novas migrations:
  `dotnet tool install --global dotnet-ef` e garanta que `~/.dotnet/tools` está no `PATH`.
- **Dentist-service com erro 500 ao criar agendamento.** Confirme que o Postgres
  estava acessível no boot; as migrations são aplicadas automaticamente.
- **BFF não sobe.** O boot valida o `.env` e falha listando as variáveis inválidas.
  Confira `KEYCLOAK_ISSUER`, `KEYCLOAK_JWKS_URI`, `KEYCLOAK_CLIENT_ID` e
  `DENTIST_SERVICE_BASE_URL`.

---

## Observações

- O `PaymentService` ainda não tem README, solução (`.sln`) nem testes, e publica
  eventos apenas via log (sem Kafka).
- Pastas `Dentist-service.Tests/` (na raiz) e `Dentist-Service/service-Dentist/`
  contêm apenas artefatos de build (`bin/`/`obj/`), sem código-fonte.
