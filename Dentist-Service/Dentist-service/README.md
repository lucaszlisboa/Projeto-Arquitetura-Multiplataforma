# Dentist Service

API REST para gerenciamento de serviços odontológicos, construída com **.NET 10**, **Entity Framework Core** e **PostgreSQL**, seguindo **arquitetura hexagonal**.

---

## Pré-requisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker](https://www.docker.com/) e Docker Compose
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

Suba o PostgreSQL via Docker Compose:

```bash
docker compose up -d
```

Isso cria um container PostgreSQL 12 com:

| Parâmetro | Valor         |
|-----------|---------------|
| Host      | localhost     |
| Porta     | 5432          |
| Banco     | dentist_service |
| Usuário   | postgres      |
| Senha     | postgres      |

> Para usar credenciais diferentes, edite `docker-compose.yml` e `appsettings.json` antes de continuar.

### 2. Connection string

O arquivo `appsettings.json` já está configurado para o banco acima:

```json
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Port=5432;Database=dentist_service;Username=postgres;Password=postgres"
}
```

---

## Migrations

Aplique as migrations para criar as tabelas:

```bash
# Instalar a ferramenta dotnet-ef (caso ainda não tenha)
dotnet tool install --global dotnet-ef

# Criar a migration inicial
dotnet ef migrations add InitialCreate

# Aplicar ao banco
dotnet ef database update
```

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

### Exemplos de requisição

**Criar serviço**
```http
POST /api/dentist-services
Content-Type: application/json

{
  "name": "Limpeza dental",
  "dentistId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "price": 150,
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

---

## Parando o banco

```bash
docker compose down
```

Para remover também os dados persistidos:

```bash
docker compose down -v
```
