# Observabilidade — Prometheus + Grafana

Stack ativada pelo perfil `observability` no `compose.yaml` da raiz. Monitora os fluxos de
**agendamento** (Dentist-service), **pagamento** (Payment-service), mensageria e BFF.

```
┌───────────────┐   scrape /metrics   ┌──────────────┐   query   ┌──────────┐
│ Apps (Docker) │◀────────────────────│  Prometheus  │◀──────────│ Grafana  │
│ :5068 :5127   │                     │    :9090     │           │  :3001   │
│ :3000 (BFF)   │   ┌──────────────┐  └──────────────┘           └──────────┘
└───────────────┘   │ kafka-exporter│◀──────┘
                    │    :9308      │
                    └───────┬───────┘
                            ▼
                      Kafka :19092
```

---

## Portas

| Serviço | Host | Observações |
|---|---|---|
| Prometheus | **9090** | UI e API (`/targets`, `/alerts`, `/graph`) |
| Grafana | **3001** | `admin` / `admin` |
| kafka-exporter | **9308** | métricas do broker (lag, offsets) |

> O Grafana usa **3001** porque **3000** é do BFF.

---

## Subindo a stack

```bash
docker compose --profile observability up -d

# Logs / status
docker compose --profile observability ps
```

Os serviços e Prometheus compartilham a rede Docker. O Prometheus consulta `dentist-service:5068`,
`payment-service:5127` e `bff:3000`; não é necessário iniciar APIs no host.

Cada app expõe:

| App | Métricas | Health |
|---|---|---|
| Dentist-service | `http://localhost:5068/metrics` | `/health` |
| PaymentService | `http://localhost:5127/metrics` | `/health` |
| BFF | `http://localhost:3000/metrics` | `/health` |

Verificação rápida:

```bash
curl -s localhost:5068/metrics | head
curl -s localhost:5127/metrics | head
curl -s localhost:3000/metrics | head
curl -s localhost:9090/api/v1/targets | jq '.data.activeTargets[].labels.job'
```

---

## Dashboards

Provisionados como código (pasta **Odontologia** no Grafana):

| Dashboard | Conteúdo |
|---|---|
| **Agendamentos — Fluxo completo** | criações por resultado/motivo/tipo, funil confirmar/rejeitar/cancelar, conflitos de horário, agenda ativa, outbox, consumer/DLQ, HTTP |
| **Pagamentos — Fluxo completo** | criações e motivos de rejeição, transições de status, valores, ativos por status, HTTP |
| **Kafka & Infraestrutura** | lag por grupo/tópico, offsets, outbox/consumer, BFF (downstream), runtime .NET, alvos `up` |

Os arquivos ficam em [`grafana/dashboards/`](grafana/dashboards) e são gerados por
[`generate-dashboards.cjs`](generate-dashboards.cjs):

```bash
node observability/generate-dashboards.cjs
```

---

## Métricas de negócio

### Dentist-service — `Meter("DentistService.Appointments")`

| Métrica (Prometheus) | Labels | Cobre |
|---|---|---|
| `appointment_creation_total` | `result`, `reason`, `type` | criação: `created`, e rejeições (`missing_identity`, `patient_mismatch`, `invalid_type`, `insurance_required`, `insurance_not_allowed`, `service_unavailable`, `patient_required`, `forbidden_service`, `invalid_start_at`, `out_of_horizon`, `outside_hours`, `slot_taken`, `serialization_conflict`, `model_validation`) |
| `appointment_confirmation_total` | `result`, `reason` | confirmação (ok / `missing_identity`, `not_found`, `invalid_state`, `slot_taken`, `serialization_conflict`) |
| `appointment_rejection_total` | `result`, `reason` | rejeição (mesmos motivos) |
| `appointment_cancellation_total` | `result`, `reason`, `actor` | cancelamento por dentista/paciente (`cancellation_window`, `invalid_state`, `not_found`) |
| `appointment_availability_total` | `result` | consulta de agenda (`ok`, `forbidden`, `not_found`, `unavailable`) |
| `appointment_schedule_update_total` | `kind`, `result` | expediente semanal / exceções |
| `appointment_list_total` | `scope`, `result` | listagem |
| `appointment_active` | `status` | gauge: pendentes/confirmados/rejeitados/cancelados/total |

### PaymentService — `Meter("PaymentService.Payments")`

| Métrica (Prometheus) | Labels | Cobre |
|---|---|---|
| `payment_creation_total` | `result`, `reason` | `created`, `invalid_appointment`, `invalid_amount`, `model_validation` |
| `payment_status_update_total` | `from`, `to`, `result`, `reason` | transições (Pending/Processing/Paid/Failed/Cancelled), `not_found`, `missing_status`, `model_validation` |
| `payment_read_total` | `operation`, `result` | `list` / `get_by_id` |
| `payment_amount` | — | histograma do valor criado |
| `payment_active` | `status` | gauge por status + `total` |

### Messaging (compartilhado) — `Meter("Messaging")`

| Métrica (Prometheus) | Labels |
|---|---|
| `messaging_outbox_published_total` | `event_type`, `topic` |
| `messaging_outbox_publish_failures_total` | `topic` |
| `messaging_outbox_pending` | — (backlog) |
| `messaging_outbox_publish_duration_seconds` | — |
| `messaging_consumer_messages_total` | `topic`, `result` (`handled`/`skipped`/`deadlettered`) |
| `messaging_consumer_retries_total` | `topic`, `event_type` |
| `messaging_consumer_dlq_total` | `topic`, `reason` (`invalid_envelope`/`invalid_json`/`handler_failed`) |
| `messaging_consumer_duration_seconds` | `topic` |
| `messaging_consumer_fatal_errors_total` | `reason` |

### BFF — `prom-client`

| Métrica | Labels |
|---|---|
| `bff_http_requests_total` | `method`, `route`, `status` |
| `bff_http_request_duration_seconds` | `method`, `route`, `status` |
| `bff_downstream_requests_total` | `client`, `method`, `outcome`, `status` |
| `bff_downstream_request_duration_seconds` | `client`, `method`, `outcome` |

Além de métricas automáticas: HTTP/Runtime do OpenTelemetry
(`http_server_request_duration_seconds*`, `dotnet_*`) e do broker via kafka-exporter
(`kafka_consumergroup_lag`, `kafka_brokers`, ...).

> Metas de agendamento/pagamento são contadas **por operação**, então cada caso de
> erro tem um `reason` próprio — os painéis quebram por esses motivos.

---

## Alertas

Definidos em [`prometheus/rules/alerts.yml`](prometheus/rules/alerts.yml):

- `ServicoIndisponivel`, `AltaTaxaDeErroNoAgendamento`, `ConflitoDeHorarioFrequente`
- `TaxaDeFalhaDePagamentoAlta`, `ErrosDeCriacaoDePagamento`
- `BacklogNoOutbox`, `FalhaAoPublicarNoKafka`, `MensagensNaDLQ`,
  `ErroFatalNoConsumidorKafka`, `LagDeConsumoAlto`
- `AltaTaxaDeErroHttp5xx`

Editar regras não exige reiniciar o container (lifecycle habilitado):

```bash
curl -X POST http://localhost:9090/-/reload
```

---

## Troubleshooting

- **Target `down`.** Confirme que a app está em `0.0.0.0` (não `localhost`) e que a
  porta publicada bate com o `prometheus.yml`. Veja `http://localhost:9090/targets`.
- **`host.docker.internal` não resolve (Linux).** O compose já inclui
  `extra_hosts: host.docker.internal:host-gateway`; exige Docker 20.10+.
- **`permission denied` nos volumes (Fedora/SELinux).** Os bind mounts já usam o
  sufixo `z` (re-rotulação). Se você adicionar novos mounts, inclua `:ro,z`.
- **kafka-exporter reiniciando.** Suba o Kafka primeiro — a rede
  `odontologia-network` e o broker precisam existir.
- **Grafana sem dados.** Confirme que o datasource aponta para `http://prometheus:9090`
  e que os dashboards aparecem na pasta **Odontologia**.
