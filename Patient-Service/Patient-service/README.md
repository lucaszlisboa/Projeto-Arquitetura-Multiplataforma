# Patient Service

API REST para gerenciamento de pacientes da clínica (**Odontologia Integrada**), construída com **.NET 10**, **Entity Framework Core** e **PostgreSQL**, seguindo **arquitetura hexagonal** — mesmo padrão do Dentist-service e do Employee-service.

---

## Pré-requisitos

- [Docker](https://www.docker.com/) e Docker Compose v2
- [.NET 10 SDK](https://dotnet.microsoft.com/download) (somente para executar no host)
- [dotnet-ef CLI](https://learn.microsoft.com/ef/core/cli/dotnet) (versão 10)

---

## Estrutura do projeto

```
Patients.Domain/          # Entidade Patient, value object PatientStatus, portas e validator
Patients.Application/     # Caso de uso (PatientUseCase) e DTOs
Patients.Infrastructure/  # EF Core, DbContext e repositório
Patients.Api/             # Controller HTTP
Migrations/               # Migrations do EF Core
```

---

## Configuração

### 1. Banco de dados

Suba o banco pelo Compose único, a partir da raiz do repositório:

```bash
docker compose up -d patient-db
```

Isso cria um container PostgreSQL 16 com:

| Parâmetro | Valor             |
|-----------|-------------------|
| Host      | localhost         |
| Porta     | 5437              |
| Banco     | patient_service   |
| Usuário   | postgres          |
| Senha     | postgres          |

> A porta **5437** evita conflito com os bancos do Dentist-service (5432), PaymentService (5433), Employee-service (5434) e Keycloak (5435).

### 2. Connection string

O arquivo `appsettings.json` já está configurado para o banco acima:

```json
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Port=5437;Database=patient_service;Username=postgres;Password=postgres"
}
```

---

## Migrations

A API aplica as migrations ao iniciar (`Database.MigrateAsync()`). Para aplicar manualmente no host:

```bash
dotnet tool install --global dotnet-ef   # caso ainda não tenha
dotnet ef database update
```

---

## Executando a API

```bash
dotnet run
```

A API ficará disponível em:

- HTTP: `http://localhost:5072`
- HTTPS: `https://localhost:7239`

A documentação OpenAPI gerada pela aplicação estará em:

```
http://localhost:5072/openapi/v1.json
```

O arquivo `Patient-service.http` tem chamadas prontas para uso no Visual Studio ou Rider.

---

## Testes

```bash
# Na pasta Patient-Service (onde está a solution)
dotnet test
```

| Pasta          | O que cobre                                          |
|----------------|------------------------------------------------------|
| `Domain/`      | Entidade `Patient` e `PatientValidator`              |
| `Application/` | `PatientUseCase` (repositório e validator mockados)  |
| `Api/`         | `PatientsController` (mapeamento para status HTTP)   |

---

## Endpoints

| Método   | Rota                   | Descrição                         |
|----------|------------------------|-----------------------------------|
| `GET`    | `/api/patients`        | Lista todos os pacientes          |
| `GET`    | `/api/patients/{id}`   | Busca um paciente por Id          |
| `POST`   | `/api/patients`        | Cadastra um novo paciente         |
| `PUT`    | `/api/patients/{id}`   | Edita os dados de um paciente     |
| `DELETE` | `/api/patients/{id}`   | Remove um paciente                |

### Regras de negócio

- **CPF único**: não é possível cadastrar dois pacientes com o mesmo CPF (`409 Conflict`). A regra é validada no caso de uso e garantida no banco por um índice único.
- **CPF imutável**: o CPF identifica o paciente e não pode ser alterado na edição.
- **Normalização**: CPF e telefone são armazenados só com dígitos; nome e e-mail sem espaços extras, e-mail em minúsculas.
- **Data de nascimento**: entre 01/01/1900 e hoje (formato `yyyy-MM-dd`).
- **Convênio (opcional)**: `insuranceName` e `insuranceCardNumber` devem ser informados juntos — mesma regra dos agendamentos do tipo `Insurance` no Dentist-service. Enviar ambos vazios/omitidos remove o convênio.
- **Validações**: nome com 3 a 100 caracteres, CPF com 11 dígitos, e-mail válido (até 150 caracteres) e telefone com 10 ou 11 dígitos (com DDD).

### Valores válidos para `status`

| Valor     | Descrição |
|-----------|-----------|
| `ativo`   | Ativo     |
| `inativo` | Inativo   |

### Exemplo de requisição

**Cadastrar paciente**
```http
POST /api/patients
Content-Type: application/json

{
  "name": "Carlos Pereira",
  "cpf": "987.654.321-00",
  "email": "carlos.pereira@email.com",
  "phone": "(43) 98888-7777",
  "birthDate": "1990-05-20",
  "status": "ativo",
  "insuranceName": "Unimed",
  "insuranceCardNumber": "0001234"
}
```
