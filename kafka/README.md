# Kafka (Message-service)

Broker de eventos do **Odontologia Integrada**: o `Message-service` do diagrama C4. Ele registra os eventos de agendamento, pagamento e plano de saúde para que os serviços se comuniquem sem chamadas HTTP diretas.

O contrato completo dos eventos está em [`docs/asyncapi.yml`](docs/asyncapi.yml).

---

## Pré-requisitos

- [Docker](https://www.docker.com/) e Docker Compose

---

## Subindo o broker

Na raiz do repositório:

```bash
docker compose -f kafka/docker-compose.yaml up -d
```

Isso sobe três containers:

| Container    | O que faz                                                        |
|--------------|------------------------------------------------------------------|
| `kafka`      | Broker Apache Kafka 4.3.1 em modo KRaft, com um único nó          |
| `kafka-init` | Cria os tópicos e termina. Pode rodar de novo sem problema        |
| `kafka-ui`   | Interface web para ver tópicos, mensagens e consumer groups       |

| Acesso                                       | Endereço                |
|----------------------------------------------|-------------------------|
| Aplicações rodando na máquina (`dotnet run`) | `localhost:9092`        |
| Containers na rede `odontologia-network`     | `kafka:19092`           |
| Kafka UI                                     | `http://localhost:8081` |

> A criação automática de tópicos está desligada. Todo tópico novo precisa entrar no comando do `kafka-init`.

---

## Ordem de subida do sistema

1. Kafka: `docker compose -f kafka/docker-compose.yaml up -d`
2. Banco do Dentist-Service: `docker compose up -d` em `Dentist-Service/Dentist-service`
3. Migrations: `dotnet ef database update` em `Dentist-Service/Dentist-service`
4. Dentist-Service: `dotnet run` em `Dentist-Service/Dentist-service`
5. Banco do Payment-Service: `docker compose up -d` em `Payment-Service`
6. Payment-Service: `dotnet run` em `Payment-Service` (aplica as migrations ao subir)

Os dois serviços sobem mesmo sem o Kafka. Os eventos ficam guardados no outbox e são publicados quando o broker voltar.

---

## Tópicos

| Tópico             | Produtor                                   | Consumidores                                    | Eventos                                                                                        |
|--------------------|--------------------------------------------|-------------------------------------------------|------------------------------------------------------------------------------------------------|
| `appointments`     | Dentist-Service (interino, até existir o Scheduled-Service) | Payment-service, Patient-Service (planejados) | `AppointmentRequested`, `AppointmentConfirmed`, `AppointmentRejected`, `AppointmentCancelled` |
| `payments`         | Payment-Service                            | Dentist-Service                                 | `PaymentApproved`, `PaymentFailed`                                                             |
| `health-insurance` | healthInsurance-service (planejado)        | Dentist-Service                                 | `InsuranceAuthorized`, `InsuranceDenied`                                                       |

Cada tópico tem 3 partições e um tópico de falhas `<tópico>.dlq` (`appointments.dlq`, `payments.dlq`, `health-insurance.dlq`).

O Payment-Service consome `AppointmentRequested` para criar um pagamento pendente
para agendamentos particulares com preço maior que zero. O consumo é idempotente:
eventos repetidos para o mesmo agendamento não criam outro pagamento. O diagrama
C4 prevê o Scheduled-Service como consumidor de pagamentos e planos. Enquanto o
agendamento estiver no Dentist-Service, é ele quem assina esses tópicos.

O Payment-Service publica quando o status de um pagamento muda (`PATCH /api/payments/{id}/status`):

| Novo status                          | Evento            |
|--------------------------------------|-------------------|
| `Paid`                               | `PaymentApproved` |
| `Failed`                             | `PaymentFailed`, com o `reason` enviado no PATCH (ou um motivo padrão) |
| `Pending`, `Processing`, `Cancelled` | nenhum            |

Repetir o mesmo status não gera um novo evento.

---

## Formato das mensagens

- **Chave:** o `appointmentId`. Isso garante a ordem dos eventos de um mesmo agendamento.
- **Valor:** um envelope JSON com os dados do evento em `data`.

```json
{
  "eventId": "0199e3c4-6a8b-7c21-9d3e-4f5a6b7c8d9e",
  "eventType": "AppointmentCancelled",
  "eventVersion": 1,
  "occurredAtUtc": "2026-10-14T15:30:00Z",
  "source": "dentist-service",
  "correlationId": "8f14e45f-ceea-467a-9575-0f5f4b6b2c11",
  "data": {
    "appointment": {
      "appointmentId": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
      "dentistId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "patientId": "5b1f3c2a-9d84-4e1b-a7c6-2f0e9d8c7b6a",
      "serviceId": "1d2c3b4a-5e6f-4a7b-8c9d-0e1f2a3b4c5d",
      "startAtUtc": "2026-10-20T13:00:00Z",
      "endAtUtc": "2026-10-20T13:30:00Z",
      "type": "Private",
      "status": "Cancelled",
      "priceCents": 15000,
      "insuranceName": null
    },
    "cancelledBy": "Patient"
  }
}
```

Regras:

- **Entrega at-least-once:** um evento pode chegar mais de uma vez. Todo consumidor deve ser idempotente e deduplicar pelo `eventId`.
- **Schema só cresce:** campos novos são permitidos. Remover ou mudar o tipo de um campo exige incrementar o `eventVersion`.
- **Correlação:** o `correlationId` vem do header `x-correlation-id` que o BFF propaga. Ele permite rastrear um evento até a requisição que o gerou.
- **Sem dados sensíveis:** o número da carteirinha do plano nunca é publicado.

---

## Biblioteca compartilhada (`Messaging/`)

Os serviços .NET usam a biblioteca `Messaging/Messaging`, que traz:

```
Contracts/   # Envelope, nomes dos tópicos e os contratos de cada evento
Outbox/      # Outbox transacional com EF Core e o publicador em background
Consumers/   # Handlers, registro por tipo de evento e despacho com retry e DLQ
Kafka/       # Opções, produtor e consumidor Kafka (Confluent.Kafka)
```

Configuração em `appsettings.json`:

```json
"Kafka": {
  "BootstrapServers": "localhost:9092",
  "ClientId": "dentist-service",
  "Enabled": true,
  "Outbox": { "PollingIntervalMs": 1000, "BatchSize": 50 },
  "Consumer": {
    "GroupId": "dentist-service",
    "Topics": [ "payments", "health-insurance" ],
    "MaxRetries": 3,
    "RetryDelayMs": 1000
  }
}
```

Com `"Enabled": false`, o serviço continua gravando no outbox, mas não publica nem consome. É útil para rodar a API sem o Kafka.

---

## Publicando eventos a partir de um serviço novo (.NET)

1. Referencie a biblioteca no `.csproj` do serviço:

   ```xml
   <ProjectReference Include="..\..\Messaging\Messaging\Messaging.csproj" />
   ```

2. No `DbContext`, adicione o outbox:

   ```csharp
   public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

   protected override void OnModelCreating(ModelBuilder modelBuilder)
   {
       modelBuilder.ApplyOutboxConfiguration();
   }
   ```

3. Gere a migration: `dotnet ef migrations add AddOutboxMessages`.
4. No `Program.cs`:

   ```csharp
   builder.Services.AddKafkaMessaging(builder.Configuration)
       .AddOutbox<MeuDbContext>();
   ```

5. Se o evento ainda não existir, crie o contrato em `Messaging/Messaging/Contracts/<Contexto>/`, implementando `IIntegrationEvent`, e documente-o em `docs/asyncapi.yml`.
6. Injete `IOutbox` e chame `Enqueue` **antes** do `SaveChanges`. O evento é gravado na mesma transação da alteração e publicado depois pelo `OutboxPublisherService`:

   ```csharp
   outbox.Enqueue(new PaymentApproved(payment.Id, payment.AppointmentId, payment.AmountCents, DateTime.UtcNow));
   await db.SaveChangesAsync(ct);
   ```

---

## Consumindo eventos

1. Implemente `IEventHandler<TEvent>`:

   ```csharp
   public class PaymentApprovedHandler : IEventHandler<PaymentApproved>
   {
       public Task HandleAsync(PaymentApproved @event, EventMetadata metadata, CancellationToken cancellationToken = default)
       {
           ...
       }
   }
   ```

2. Registre o consumidor e o handler:

   ```csharp
   builder.Services.AddKafkaMessaging(builder.Configuration)
       .AddConsumer()
       .AddEventHandler<PaymentApproved, PaymentApprovedHandler>();
   ```

3. Garanta que o tópico do evento está em `Kafka:Consumer:Topics`. Se não estiver, o serviço avisa no log ao subir.

Como o consumidor trata cada mensagem:

| Situação                                | Resultado                                                         |
|-----------------------------------------|-------------------------------------------------------------------|
| Handler executa sem erro                | Offset confirmado                                                 |
| Nenhum handler para o `eventType`       | Registrado no log e offset confirmado                             |
| Handler falha                           | Novas tentativas (`MaxRetries`), depois envio para `<tópico>.dlq` |
| Mensagem não é um envelope válido       | Envio direto para `<tópico>.dlq`                                  |
| Falha ao enviar para a DLQ              | A mensagem é lida de novo, sem confirmar o offset                 |

As mensagens na DLQ mantêm o valor e a chave originais e ganham os headers `dlq-original-topic`, `dlq-original-partition`, `dlq-original-offset`, `dlq-consumer-group` e `dlq-error`.

---

## Serviços que não são .NET

O healthInsurance-service ainda não tem stack definida. Para outra linguagem:

- siga o envelope e os schemas de [`docs/asyncapi.yml`](docs/asyncapi.yml);
- use o `appointmentId` como chave da mensagem;
- use um particionador compatível com o do Java (murmur2), que é o padrão do cliente Java e do KafkaJS. Assim a mesma chave cai na mesma partição, seja qual for o produtor.

---

## Containers na rede compartilhada

Um serviço que rode em container pode entrar na rede do Kafka. O compose do Kafka precisa estar no ar antes:

```yaml
services:
  meu-servico:
    environment:
      Kafka__BootstrapServers: kafka:19092
    networks:
      - odontologia

networks:
  odontologia:
    name: odontologia-network
    external: true
```

---

## Testando manualmente

Ler os eventos de agendamento, com a chave:

```bash
docker compose -f kafka/docker-compose.yaml exec kafka /opt/kafka/bin/kafka-console-consumer.sh --bootstrap-server localhost:9092 --topic appointments --from-beginning --formatter-property print.key=true
```

Publicar um evento de pagamento no formato `chave|valor`:

```bash
docker compose -f kafka/docker-compose.yaml exec kafka /opt/kafka/bin/kafka-console-producer.sh --bootstrap-server localhost:9092 --topic payments --reader-property parse.key=true --reader-property "key.separator=|"
```

Pelo Kafka UI (`http://localhost:8081`) também dá para ver as mensagens e publicar novas em **Topics → Produce Message**.

> No Git Bash do Windows, prefixe os comandos com `MSYS_NO_PATHCONV=1` para que o caminho `/opt/kafka/...` não seja convertido.

---

## Limitações conhecidas

- As linhas já publicadas do outbox (`processed_at_utc` preenchido) não são apagadas. Uma rotina de limpeza ainda precisa ser criada.
- O publicador do outbox assume uma única instância de cada serviço.
- O Dentist-Service publica `appointments` de forma interina. Quando o Scheduled-Service existir, a publicação deve migrar para ele.

---

## Parando o broker

```bash
docker compose -f kafka/docker-compose.yaml down
```

Para remover também os tópicos e mensagens:

```bash
docker compose -f kafka/docker-compose.yaml down -v
```
