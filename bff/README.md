# BFF Odontológico (NestJS · Hexagonal · Keycloak RBAC)

Única porta de entrada HTTP para os clientes (web/mobile). Autentica via JWT do
Keycloak (JWKS/RS256), autoriza por RBAC (`employ` | `dentist` | `patient`) e
orquestra o **Dentist Service** e o **Employee Service**. Spec em `docs/specs/spec01.md`, tarefas em
`docs/tasks/task01.md`.

## Como subir

```bash
cp .env.example .env   # ajuste KEYCLOAK_* e DENTIST_SERVICE_BASE_URL
pnpm install
pnpm run start:dev     # http://localhost:3000 | Swagger em /docs (se SWAGGER_ENABLED=true)
```

Via Docker, usando o Compose único na raiz do repositório:

```bash
docker compose up --build -d

# BFF com mock do Dentist-service, na porta 3002
docker compose --profile mock up -d bff-mock
```

`GET /health` é público (liveness).

## Variáveis de ambiente

Ver `.env.example` (todas as variáveis do 10 do spec). Obrigatórias:
`KEYCLOAK_ISSUER`, `KEYCLOAK_JWKS_URI`, `KEYCLOAK_CLIENT_ID`,
`DENTIST_SERVICE_BASE_URL`. O boot **falha** com env inválida, listando o problema.
`EMPLOYEE_SERVICE_BASE_URL` é opcional (padrão `http://localhost:5070`, porta do Employee-service).

### Logs

O BFF escreve logs JSON (ECS) no stdout e em `/app/logs/bff/bff.log` no volume compartilhado
com o Filebeat. Cada log traz `labels.correlation_id` (header
`X-Correlation-Id`). Eventos: `auth.denied`, `http.rate_limited`, `downstream.timeout|unavailable|error`.
Headers, tokens e query strings não são logados. Detalhes em
[`../elk/docs/logging-standard.md`](../elk/docs/logging-standard.md).

## Como obter um token de teste

1. No Keycloak, crie o realm/cliente `bff-clinica` com uma role de realm
   `dentist` (valores exatos: `employ`, `dentist`, `patient` — case-sensitive).
2. Crie um usuário de teste com a role `dentist`.
3. Obtenha o token (Direct Access Grants, apenas dev):
   ```bash
   curl -X POST "$KEYCLOAK_ISSUER/protocol/openid-connect/token" \
     -d "grant_type=password&client_id=bff-clinica&username=dentista1&password=senha"
   ```
4. Chame o BFF:
   ```bash
   curl -H "Authorization: Bearer $TOKEN" http://localhost:3000/api/dentist-services
   ```

Nos testes automatizados um JWKS local + `signToken` é usado
(`test/support/jwt-test-kit.ts`) — nenhum Keycloak real é necessário.

## Usando o Keycloak do repositório

O realm `dentist` em `keycloak/keycloak/dentist-realm.json` já traz as roles e
usuários de teste (senha `123456`):

| Usuário        | Role      | Acessa                  |
| -------------- | --------- | ----------------------- |
| `dentista1`    | `dentist` | `/api/dentist-services` |
| `funcionario1` | `employ`  | `/api/employees`        |

Valores do `.env` para esse realm:

```bash
KEYCLOAK_ISSUER=http://localhost:8080/realms/dentist
KEYCLOAK_JWKS_URI=http://localhost:8080/realms/dentist/protocol/openid-connect/certs
KEYCLOAK_CLIENT_ID=dentist-app
KEYCLOAK_AUDIENCE=
```

Token do funcionário e chamada ao BFF:

```bash
TOKEN=$(curl -s -X POST "http://localhost:8080/realms/dentist/protocol/openid-connect/token" \
  -d "grant_type=password&client_id=dentist-app&username=funcionario1&password=123456" | jq -r .access_token)
curl -H "Authorization: Bearer $TOKEN" http://localhost:3000/api/employees
```

> O Keycloak importa o realm quando ele ainda não existe. Para reaplicar alterações ao realm,
> use `keycloak/seed.sh` ou recrie os dados conforme as instruções do README da raiz.

## Matriz RBAC

| Recurso                                               |          `employ`          | `dentist` | `patient` |
| ----------------------------------------------------- | :------------------------: | :-------: | :-------: |
| `/api/dentist-services` (todas)                       |            403             |    ✅     |    403    |
| `/api/appointments`, `/api/availability`              |            403             |    ✅     |    ✅     |
| `/api/dentist-schedule/*`, confirmar/rejeitar pedidos |            403             |    ✅     |    403    |
| `/api/employees` (todas)                              |             ✅             |    403    |    403    |
| `/health`                                             |          público           |  público  |  público  |
| `/docs`                                               | conforme `SWAGGER_ENABLED` |           |           |

Sem token/token inválido → `401`; sem a role → `403`. Rotas sem
`@Public()`/`@Roles()` negam por padrão (fail-closed). O cliente nunca envia
`X-Dentist-Id`/`dentistId`: o BFF injeta o `sub` do token.

## Scripts

| Comando                        | O quê                                        |
| ------------------------------ | -------------------------------------------- |
| `pnpm run build` / `start:dev` | build / dev                                  |
| `pnpm run lint`                | ESLint + Prettier                            |
| `pnpm run arch:check`          | dependency-cruiser (hexagonal)               |
| `pnpm run test:unit`           | testes unitários (`src/**/*.spec.ts`)        |
| `pnpm run test:int`            | integração (JWKS local + HTTP local)         |
| `pnpm run test:e2e`            | E2E (pulado nesta entrega — sem specs)       |
| `pnpm run test:cov`            | cobertura (metas §12.10 no `jest.config.js`) |

Testes E2E (`T-07.21–T-07.25`) foram **pulados nesta entrega** a infra (`configureApp`, `jwt-test-kit`, `downstream-mock`) está
pronta para reativá-los.

## Como adicionar um novo módulo (ex.: `patient-services`)

> O módulo `employees` é um exemplo completo desse passo a passo.

1. Crie `src/modules/<nome>/{domain,application,infrastructure}` no mesmo molde.
2. Declare a porta OUT e os use cases (`UseCase<In,Out>`).
3. Implemente o gateway com `DownstreamHttpModule.forClient(TOKEN)` (uma linha).
4. Anote o controller com `@Auth(Role.X)` — nada muda em `auth/` ou `shared/`
   (exceto novos `DomainError`s no mapa do filtro).
