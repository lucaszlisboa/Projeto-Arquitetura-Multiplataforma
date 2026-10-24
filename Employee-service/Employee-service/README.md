# Employee Service

API REST para gerenciamento de funcionários da clínica (**Odontologia Integrada**), construída com **.NET 10**, **Entity Framework Core** e **PostgreSQL**, seguindo **arquitetura hexagonal** — mesmo padrão do Dentist-service.

---

## Pré-requisitos

- [Docker](https://www.docker.com/) e Docker Compose v2
- [.NET 10 SDK](https://dotnet.microsoft.com/download) (somente para executar no host)
- [dotnet-ef CLI](https://learn.microsoft.com/ef/core/cli/dotnet) (versão 10)

---

## Estrutura do projeto

```
Employees.Domain/          # Entidade Employee, value objects (cargo/status), portas e validator
Employees.Application/     # Caso de uso (EmployeeUseCase) e DTOs
Employees.Infrastructure/  # EF Core, DbContext e repositório
Employees.Api/             # Controller HTTP (EmployeeRouter do modelo C4)
Migrations/                # Migrations do EF Core
docs/                      # OpenAPI e collection do Postman
```

### Mapeamento com o modelo C4

| Componente C4      | Implementação                                     |
|--------------------|---------------------------------------------------|
| EmployeeRouter     | `EmployeesController`                             |
| CreateEmployee     | `EmployeeUseCase.CreateAsync`                     |
| EditEmployee       | `EmployeeUseCase.UpdateAsync`                     |
| GetEmployees       | `EmployeeUseCase.GetAllAsync`                     |
| GetEmployeeById    | `EmployeeUseCase.GetByIdAsync`                    |
| RemoveEmployee     | `EmployeeUseCase.DeleteAsync`                     |
| Employee           | `Employee` (entidade de domínio)                  |
| EmployeeRole       | `EmployeeRole` (value object)                     |
| EmployeeStatus     | `EmployeeStatus` (value object)                   |
| EmployeeRepository | `IEmployeeRepository` → `EmployeeRepository`      |
| PostgreSQL         | Banco `employee_service`, tabela `employees`      |

---

## Configuração

### 1. Banco de dados

Suba o banco pelo Compose único, a partir da raiz do repositório:

```bash
docker compose up -d employee-db
```

Isso cria um container PostgreSQL 16 com:

| Parâmetro | Valor              |
|-----------|--------------------|
| Host      | localhost          |
| Porta     | 5434               |
| Banco     | employee_service   |
| Usuário   | postgres           |
| Senha     | postgres           |

> A porta **5434** evita conflito com os demais bancos publicados pelo Compose único.

### 2. Connection string

O arquivo `appsettings.json` já está configurado para o banco acima:

```json
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Port=5434;Database=employee_service;Username=postgres;Password=postgres"
}
```

---

## Migrations

As migrations são aplicadas automaticamente no boot da API (`Database.MigrateAsync()`).
Para criar uma nova migration:

```bash
# Instalar/atualizar a ferramenta dotnet-ef (caso ainda não tenha)
dotnet tool install --global dotnet-ef
dotnet tool update --global dotnet-ef

dotnet ef migrations add NomeDaMigration
```

> Na primeira execução em um banco vazio, o EF registra um `fail` ao consultar `__EFMigrationsHistory` antes de criar essa tabela. É esperado: a migration é aplicada normalmente em seguida.

---

## Executando a API

```bash
dotnet run
```

A API ficará disponível em:

- HTTP: `http://localhost:5070`
- HTTPS: `https://localhost:7237`

A documentação OpenAPI gerada pela aplicação estará em:

```
http://localhost:5070/openapi/v1.json
```

A especificação escrita à mão fica em [`docs/openapi.yml`](docs/openapi.yml).

---

## Testes

```bash
# Na pasta Employee-service (onde está a solution)
dotnet test
```

Os testes ficam no projeto `Employee-service.Tests`:

| Pasta          | O que cobre                                            |
|----------------|--------------------------------------------------------|
| `Domain/`      | Entidade `Employee` e `EmployeeValidator`              |
| `Application/` | `EmployeeUseCase` (repositório e validator mockados)   |
| `Api/`         | `EmployeesController` (mapeamento para status HTTP)    |

### Postman

Importe [`docs/employee-service.postman_collection.json`](docs/employee-service.postman_collection.json) no Postman e execute pelo **Collection Runner**. O cadastro gera um CPF aleatório e salva o `employeeId` usado pelas requisições seguintes.

O arquivo `Employee-service.http` tem as mesmas chamadas para uso direto no Visual Studio ou Rider.

---

## Endpoints

| Método   | Rota                    | Descrição                                   |
|----------|-------------------------|---------------------------------------------|
| `GET`    | `/api/employees`        | Lista todos os funcionários da clínica      |
| `GET`    | `/api/employees/{id}`   | Busca um funcionário por Id                 |
| `POST`   | `/api/employees`        | Cadastra um novo funcionário                |
| `PUT`    | `/api/employees/{id}`   | Edita os dados de um funcionário            |
| `DELETE` | `/api/employees/{id}`   | Remove um funcionário                       |

### Regras de negócio

- **CPF único**: não é possível cadastrar dois funcionários com o mesmo CPF (`409 Conflict`). A regra é validada no caso de uso e garantida no banco por um índice único.
- **CPF imutável**: o CPF identifica o funcionário e não pode ser alterado na edição.
- **Normalização**: CPF e telefone são armazenados só com dígitos (`123.456.789-01` → `12345678901`); nome e e-mail sem espaços extras, e-mail em minúsculas.
- **Validações**: nome com 3 a 100 caracteres, CPF com 11 dígitos, e-mail válido (até 150 caracteres) e telefone com 10 ou 11 dígitos (com DDD).

### Exemplos de requisição

**Cadastrar funcionário**
```http
POST /api/employees
Content-Type: application/json

{
  "name": "Maria Souza",
  "cpf": "123.456.789-01",
  "email": "maria.souza@clinica.com",
  "phone": "(43) 99999-8888",
  "role": "recepcionista",
  "status": "ativo"
}
```

**Listar funcionários**
```http
GET /api/employees
```

**Buscar funcionário por Id**
```http
GET /api/employees/7c9e6679-7425-40de-944b-e07fc1f90ae7
```

**Editar funcionário**
```http
PUT /api/employees/7c9e6679-7425-40de-944b-e07fc1f90ae7
Content-Type: application/json

{
  "name": "Maria Souza Lima",
  "email": "maria.lima@clinica.com",
  "phone": "43999998888",
  "role": "administrativo",
  "status": "ferias"
}
```

**Remover funcionário**
```http
DELETE /api/employees/7c9e6679-7425-40de-944b-e07fc1f90ae7
```

### Códigos de resposta

| Código | Significado                                    |
|--------|------------------------------------------------|
| `200`  | Sucesso                                        |
| `201`  | Funcionário cadastrado                         |
| `204`  | Removido com sucesso                           |
| `400`  | Payload inválido (lista os erros de validação) |
| `404`  | Funcionário não encontrado                     |
| `409`  | Já existe um funcionário com o CPF informado   |

### Valores válidos para `role`

| Valor            | Descrição      |
|------------------|----------------|
| `recepcionista`  | Recepcionista  |
| `auxiliar`       | Auxiliar       |
| `administrativo` | Administrativo |

### Valores válidos para `status`

| Valor     | Descrição |
|-----------|-----------|
| `ativo`   | Ativo     |
| `inativo` | Inativo   |
| `ferias`  | Em férias |

---

## Parando o banco

```bash
docker compose stop employee-service employee-db
```

O volume do banco permanece. `docker compose down -v`, executado na raiz, remove os volumes de toda a stack.
