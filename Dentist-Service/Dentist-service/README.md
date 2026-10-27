# Dentist Service

API REST para gerenciamento de serviços, expediente e agendamentos odontológicos, construída com **.NET 10**, **Entity Framework Core** e **PostgreSQL**.

---

## Pré-requisitos

- [Docker](https://www.docker.com/) e Docker Compose v2
- [.NET 10 SDK](https://dotnet.microsoft.com/download) (somente para executar no host)
- (Opcional) [dotnet-ef CLI](https://learn.microsoft.com/ef/core/cli/dotnet)

---

## Estrutura do projeto

```
DentistsServices.Domain/          # Entidades, portas e regras de negócio
DentistsServices.Application/     # Casos de uso e DTOs
DentistsServices.Infrastructure/  # EF Core, DbContext e repositórios
DentistsServices.Api/             # Controllers HTTP
```

---

## Configuração

### 1. Banco de dados

Suba o banco pelo Compose único, a partir da raiz do repositório:

```bash
docker compose up -d dentist-db
```

Isso cria um container PostgreSQL 16 com:

| Parâmetro | Valor         |
|-----------|---------------|
| Host      | localhost     |
| Porta     | 5432          |
| Banco     | dentist_service |
| Usuário   | postgres      |
| Senha     | postgres      |

> As portas e credenciais podem ser alteradas no `.env` da raiz. No container, o Compose configura a conexão pelo nome `dentist-db`.

### 2. Connection string

O arquivo `appsettings.json` já está configurado para o banco acima:

```json
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Port=5432;Database=dentist_service;Username=postgres;Password=postgres"
}
```

---

## Logs

A API grava logs JSON (ECS) em `{Observability:LogDirectory}/{Observability:ServiceName}/` e no
console, para o ELK. Em `appsettings.Development.json`:

```json
"Observability": { "ServiceName": "dentist-service", "LogDirectory": "../../logs" }
```

`../../logs` aponta para a pasta `logs/` na raiz do repositório quando a API roda a partir de
`Dentist-service/Dentist-service`. Eventos de agendamento (`appointment.*`, `schedule.*`) e de
mensageria (`outbox.*`, `consumer.*`) trazem `labels.correlation_id`. Ver
[`elk/README.md`](../../elk/README.md) e [`elk/docs/logging-standard.md`](../../elk/docs/logging-standard.md).

---

## Migrations

As migrations são aplicadas automaticamente no boot da API (`Database.MigrateAsync()`).
Para criar uma nova migration:

```bash
# Instalar a ferramenta dotnet-ef (caso ainda não tenha)
dotnet tool install --global dotnet-ef

dotnet ef migrations add NomeDaMigration
```

A migration `20261013120000_AddAppointments` adiciona duração aos serviços, expediente semanal, exceções e pedidos de consulta.

A migration `AddOutboxMessages` cria a tabela `outbox_messages`, usada para publicar eventos no Kafka. Sem ela, toda criação ou mudança de status de agendamento retorna erro 500.

---

## Executando a API

```bash
dotnet run
```

A API ficará disponível em:

- HTTP: `http://localhost:5068`
- HTTPS: `https://localhost:7235`

A documentação OpenAPI (Scalar/Swagger) estará em:

```
http://localhost:5068/openapi/v1.json
```

---

## Mensageria (Kafka)

O serviço publica o ciclo de vida dos agendamentos no tópico `appointments` e consome os tópicos `payments` e `health-insurance`. Como subir o broker, o formato dos eventos e como criar handlers está em [`kafka/README.md`](../../kafka/README.md).

| Ação                                    | Evento publicado                                     |
|-----------------------------------------|------------------------------------------------------|
| `POST /api/appointments`                | `AppointmentRequested`                               |
| `POST /api/appointments/{id}/confirm`   | `AppointmentConfirmed`                               |
| `POST /api/appointments/{id}/reject`    | `AppointmentRejected`                                |
| `POST /api/appointments/{id}/cancel`    | `AppointmentCancelled` (com `cancelledBy`)           |

Os eventos são gravados na tabela `outbox_messages` na mesma transação do agendamento e publicados em segundo plano. Se o Kafka estiver fora do ar, a API continua respondendo normalmente e os eventos são publicados quando ele voltar.

Ordem de subida: Kafka, banco, `dotnet ef database update` e então `dotnet run`. Para rodar sem o Kafka, use `"Kafka": { "Enabled": false }` no `appsettings.Development.json`.

Ainda não há handlers de negócio para pagamentos e planos de saúde. O Payment-service já publica em `payments`, mas por enquanto esses eventos só são registrados no log e confirmados.

---

## Endpoints

Todos os endpoints que envolvem ownership exigem o header `X-Dentist-Id` com o GUID do dentista requisitante.

> **Nota:** quando autenticação JWT for adicionada, o `X-Dentist-Id` será substituído pelo claim do token.

### Serviços odontológicos

| Método   | Rota                              | Descrição                                          | Header obrigatório |
|----------|-----------------------------------|----------------------------------------------------|-------------------|
| `GET`    | `/api/dentist-services`           | Lista os serviços do dentista autenticado          | `X-Dentist-Id`    |
| `GET`    | `/api/dentist-services/{id}`      | Busca um serviço por Id (somente do próprio dono)  | `X-Dentist-Id`    |
| `POST`   | `/api/dentist-services`           | Cria um novo serviço                               | —                 |
| `PUT`    | `/api/dentist-services/{id}`      | Atualiza um serviço existente                      | —                 |
| `DELETE` | `/api/dentist-services/{id}`      | Remove um serviço (somente o próprio dono)         | `X-Dentist-Id`    |
| `GET` | `/api/availability?dentistId={uuid}&serviceId={uuid}` | Horários livres nos próximos 30 dias | `X-Dentist-Id` opcional |
| `GET/PUT` | `/api/dentist-schedule/weekly` | Consultar ou substituir expediente semanal (dia 0=domingo) | `X-Dentist-Id` |
| `GET/PUT` | `/api/dentist-schedule/exceptions` | Consultar ou definir exceção por data | `X-Dentist-Id` |
| `GET/POST` | `/api/appointments` | Listar próprios pedidos ou solicitar consulta | `X-Patient-Id` ou `X-Dentist-Id` |
| `POST` | `/api/appointments/{id}/confirm`, `/reject`, `/cancel` | Gerenciar pedido | Header conforme perfil |

### Exemplos de requisição

**Criar serviço**
```http
POST /api/dentist-services
Content-Type: application/json

{
  "name": "Limpeza dental",
  "dentistId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "price": 150,
  "durationMinutes": 30,
  "isPeriodic": true,
  "status": "disponivel"
}
```

**Listar serviços do dentista**
```http
GET /api/dentist-services
X-Dentist-Id: 3fa85f64-5717-4562-b3fc-2c963f66afa6
```

**Buscar serviço por Id**
```http
GET /api/dentist-services/7c9e6679-7425-40de-944b-e07fc1f90ae7
X-Dentist-Id: 3fa85f64-5717-4562-b3fc-2c963f66afa6
```

**Deletar serviço**
```http
DELETE /api/dentist-services/7c9e6679-7425-40de-944b-e07fc1f90ae7
X-Dentist-Id: 3fa85f64-5717-4562-b3fc-2c963f66afa6
```

### Códigos de resposta

| Código | Significado                                          |
|--------|------------------------------------------------------|
| `200`  | Sucesso                                              |
| `201`  | Recurso criado                                       |
| `204`  | Deletado com sucesso                                 |
| `400`  | Payload inválido ou header `X-Dentist-Id` ausente    |
| `403`  | Serviço existe, mas pertence a outro dentista        |
| `404`  | Serviço não encontrado                               |

### Valores válidos para `status`

| Valor          | Descrição        |
|----------------|------------------|
| `disponivel`   | Disponível       |
| `indisponivel` | Indisponível     |
| `emBreve`      | Em breve         |

Pedidos começam `Pending`. O tipo `Private` registra preço em centavos no pedido; `Insurance` exige `insuranceName` e `insuranceCardNumber`. Datas precisam de offset ISO 8601 e são salvas em UTC. Expediente semanal usa `America/Sao_Paulo`. Paciente só pode cancelar consulta confirmada com ao menos 24 horas; dentista pode cancelar a qualquer momento.

---

## Parando o banco

```bash
docker compose stop dentist-service dentist-db
```

O volume do banco permanece. `docker compose down -v`, executado na raiz, remove os volumes de toda a stack.
