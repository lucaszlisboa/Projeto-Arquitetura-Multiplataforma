# Odontologia Integrada

Monorepo da plataforma de gestão odontológica. Um único arquivo, [`compose.yaml`](compose.yaml), define APIs, BFF, bancos, mensageria, autenticação, métricas e logs.

## Instalação rápida

Instale Docker Desktop (ou Docker Engine com Docker Compose v2). .NET, Node.js, pnpm, PostgreSQL, Kafka e Keycloak rodam nos containers; não precisam estar instalados no computador.

Na raiz do projeto:

```bash
cp .env.example .env
docker compose --profile observability --profile elk up --build -d
```

Esse comando constrói as aplicações e inicia o sistema completo, incluindo Prometheus, Grafana e ELK. A primeira execução baixa imagens e dependências, então pode levar alguns minutos.

Para iniciar somente aplicações e dependências necessárias ao runtime:

```bash
docker compose up --build -d
```

Confira o estado e os logs:

```bash
docker compose ps
docker compose logs -f bff
```

## Componentes e endereços

| Componente | Endereço local | Função |
|---|---|---|
| BFF | <http://localhost:3000> | API NestJS; Swagger em `/docs` |
| Dentist-service | <http://localhost:5068> | API .NET 10; OpenAPI em `/openapi/v1.json` |
| Employee-service | <http://localhost:5070> | API .NET 10; OpenAPI em `/openapi/v1.json` |
| Patient-service | <http://localhost:5072> | API .NET 10; OpenAPI em `/openapi/v1.json` |
| Payment-service | <http://localhost:5127> | API .NET 10; Swagger em `/swagger` |
| Keycloak | <http://localhost:8080> | Login, tokens e realm `dentist` |
| Kafka UI | <http://localhost:8081> | Inspeção de tópicos e mensagens |
| Prometheus | <http://localhost:9090> | Métricas; perfil `observability` |
| Grafana | <http://localhost:3001> | Dashboards; perfil `observability` |
| Kibana | <http://localhost:5601> | Pesquisa de logs; perfil `elk` |
| Elasticsearch | <http://localhost:9200> | Índices de logs; perfil `elk` |

Os bancos PostgreSQL ficam nas portas `5432` (Dentist), `5433` (Payment), `5434` (Employee), `5435` (Keycloak) e `5437` (Patient). O Kafka aceita conexões do host em `9092`; containers usam `kafka:19092`.

O Compose cria bancos e tópicos, importa o realm do Keycloak e aplica migrations no boot das APIs Dentist, Employee, Patient e Payment.

## Credenciais de desenvolvimento

- Keycloak: `admin` / `admin`.
- Usuários do realm `dentist`: `dentista1`, `funcionario1` e `paciente1`; senha `123456`.
- PostgreSQL das APIs: usuário `postgres`, senha `postgres`.
- PostgreSQL do Keycloak: usuário `keycloak`, senha `keycloak`.
- Grafana: `admin` / `admin`.

Use credenciais próprias fora de desenvolvimento. Portas, senhas e heaps podem ser alterados no `.env`; os padrões também estão documentados em [`.env.example`](.env.example).

## Operações comuns

```bash
# Reconstruir imagens após alterar código
docker compose up --build -d

# Parar e remover containers; volumes com dados permanecem
docker compose --profile observability --profile elk down

# Apagar também bancos, índices, logs e dados do Kafka
docker compose --profile observability --profile elk down --volumes
```

Para ver logs de outro serviço, troque `bff` pelo nome correspondente, por exemplo `docker compose logs -f dentist-service kafka keycloak`.

### BFF com serviço Dentist simulado

Para iniciar a configuração de desenvolvimento do BFF com mock, sem subir as APIs reais:

```bash
docker compose --profile mock up -d bff-mock
```

O BFF simulado fica em <http://localhost:3002>; ele usa o Keycloak e o banco do Keycloak definidos no mesmo Compose.

## Desenvolvimento fora do Docker

Para executar código no host, instale .NET SDK 10, Node.js 20 e pnpm 9 (`corepack enable`). O `dotnet-ef` 10 é necessário apenas para criar ou aplicar migrations manualmente. Os serviços de infraestrutura continuam disponíveis pelo Compose.

## Estrutura

| Pasta | Projeto |
|---|---|
| `bff/` | BFF NestJS 11 / TypeScript |
| `Dentist-Service/` | API Dentist .NET 10 |
| `Employee-service/` | API Employee .NET 10 |
| `Patient-Service/` | API Patient .NET 10 |
| `Payment-Service/` | API Payment .NET 10 |
| `Messaging/` | Biblioteca de mensageria Kafka para .NET |
| `Observability/` | Logs ECS e stack Prometheus/Grafana |
| `kafka/`, `keycloak/`, `elk/` | Configurações da infraestrutura |
