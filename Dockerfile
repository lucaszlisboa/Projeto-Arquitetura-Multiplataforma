# syntax=docker/dockerfile:1
#
# Imagem única e autocontida do monorepo:
#   Postgres + Kafka + Keycloak + BFF + Dentist + Employee + Payment
# no mesmo container, gerenciados pelo supervisor.
#
#   docker build -t odontologia .
#   docker run -p 3000:3000 -p 8080:8080 odontologia
#
# O BFF é o ÚNICO ponto de entrada da aplicação: só ele (3000) e o Keycloak
# (8080, login) escutam em todas as interfaces. As APIs .NET, o Postgres e o
# Kafka escutam apenas em 127.0.0.1 — o BFF as alcança por dentro do container,
# mas elas ficam inacessíveis de fora, mesmo que alguém publique a porta com -p.
#
# O target "bff" continua disponível apenas para desenvolvimento isolado do BFF:
#   docker build --target bff -t bff .

# =============================================================================
# BFF (NestJS) — build
# =============================================================================
FROM node:20-alpine AS bff-build
WORKDIR /app
RUN corepack enable && corepack prepare pnpm@9 --activate
COPY bff/package.json bff/pnpm-lock.yaml bff/pnpm-workspace.yaml ./
RUN pnpm install --frozen-lockfile
COPY bff/ ./
RUN pnpm run build && pnpm prune --prod

# Target opcional para rodar somente o BFF (usado no docker-compose.dev.yml).
FROM node:20-alpine AS bff
WORKDIR /app
ENV NODE_ENV=production
RUN addgroup -S app && adduser -S app -G app
COPY --from=bff-build --chown=app:app /app/dist ./dist
COPY --from=bff-build --chown=app:app /app/node_modules ./node_modules
COPY --from=bff-build --chown=app:app /app/package.json ./package.json
USER app
EXPOSE 3000
HEALTHCHECK --interval=30s --timeout=5s --retries=3 \
  CMD wget -qO- http://127.0.0.1:3000/health || exit 1
CMD ["node", "dist/main"]

# =============================================================================
# APIs .NET — build das três aplicações
# =============================================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dotnet-build
WORKDIR /src

# Copia apenas os .csproj primeiro para aproveitar o cache do restore.
COPY Messaging/Messaging/Messaging.csproj Messaging/Messaging/
COPY Observability/Observability/Observability.csproj Observability/Observability/
COPY Dentist-Service/Dentist-service/Dentist-service.csproj Dentist-Service/Dentist-service/
COPY Employee-service/Employee-service/Employee-service.csproj Employee-service/Employee-service/
COPY Payment-Service/PaymentService.csproj Payment-Service/
RUN --mount=type=cache,target=/root/.nuget/packages \
    sh -c 'dotnet restore Dentist-Service/Dentist-service/Dentist-service.csproj && \
           dotnet restore Employee-service/Employee-service/Employee-service.csproj && \
           dotnet restore Payment-Service/PaymentService.csproj'

COPY Messaging/ Messaging/
COPY Observability/ Observability/
COPY Dentist-Service/Dentist-service/ Dentist-Service/Dentist-service/
COPY Employee-service/Employee-service/ Employee-service/Employee-service/
COPY Payment-Service/ Payment-Service/
RUN --mount=type=cache,target=/root/.nuget/packages \
    sh -c 'dotnet publish Dentist-Service/Dentist-service/Dentist-service.csproj \
             --configuration Release --no-restore --output /out/dentist /p:UseAppHost=false && \
           dotnet publish Employee-service/Employee-service/Employee-service.csproj \
             --configuration Release --no-restore --output /out/employee /p:UseAppHost=false && \
           dotnet publish Payment-Service/PaymentService.csproj \
             --configuration Release --no-restore --output /out/payment /p:UseAppHost=false'

# =============================================================================
# Imagem final — aplicações + infraestrutura no mesmo container
# =============================================================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

# Node 20 (glibc) copiado da imagem oficial para rodar o BFF.
COPY --from=node:20-bookworm-slim /usr/local/bin/node /usr/local/bin/node

# Keycloak e Kafka oficiais, sem os entrypoints das imagens originais.
COPY --from=quay.io/keycloak/keycloak:26.6.0 --chown=app:app /opt/keycloak /opt/keycloak
COPY --from=apache/kafka:4.3.1 --chown=app:app /opt/kafka /opt/kafka

ENV DEBIAN_FRONTEND=noninteractive
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        supervisor \
        curl \
        postgresql-16 \
        postgresql-client-16 \
        openjdk-21-jre-headless \
    && rm -rf /var/lib/apt/lists/* /var/lib/postgresql/16/main /etc/postgresql/16/main

WORKDIR /app
RUN mkdir -p /app/logs /app/pgdata /app/kafka-data /app/kafka-logs /app/scripts \
        /opt/keycloak/data/import \
    && chown -R app:app /app /opt/keycloak

COPY --chown=app:app keycloak/keycloak/dentist-realm.json /opt/keycloak/data/import/dentist-realm.json

COPY --from=bff-build --chown=app:app /app/dist /app/bff/dist
COPY --from=bff-build --chown=app:app /app/node_modules /app/bff/node_modules
COPY --from=bff-build --chown=app:app /app/package.json /app/bff/package.json
COPY --from=dotnet-build --chown=app:app /out/dentist /app/dentist
COPY --from=dotnet-build --chown=app:app /out/employee /app/employee
COPY --from=dotnet-build --chown=app:app /out/payment /app/payment

# =============================================================================
# Configuração dos serviços embarcados
# =============================================================================
ENV ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_NOLOGO=true \
    DOTNET_RUNNING_IN_CONTAINER=true \
    NODE_ENV=production \
    PORT=3000 \
    HOME=/app \
    SWAGGER_ENABLED=true \
    KEYCLOAK_ISSUER=http://localhost:8080/realms/dentist \
    KEYCLOAK_JWKS_URI=http://127.0.0.1:8080/realms/dentist/protocol/openid-connect/certs \
    KEYCLOAK_CLIENT_ID=dentist-app \
    DENTIST_SERVICE_BASE_URL=http://127.0.0.1:5068 \
    EMPLOYEE_SERVICE_BASE_URL=http://127.0.0.1:5070 \
    KAFKA_BOOTSTRAP_SERVERS=localhost:9092 \
    KAFKA_ENABLED=true \
    KAFKA_HEAP_OPTS="-Xms256m -Xmx512m" \
    JAVA_HOME=/usr/lib/jvm/java-21-openjdk-amd64 \
    JAVA_OPTS_APPEND="-Xms64m -Xmx512m" \
    PATH=/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin:/usr/lib/postgresql/16/bin \
    DENTIST_CONNECTION_STRING="Host=127.0.0.1;Port=5432;Database=dentist_service;Username=postgres;Password=postgres" \
    EMPLOYEE_CONNECTION_STRING="Host=127.0.0.1;Port=5432;Database=employee_service;Username=postgres;Password=postgres" \
    PAYMENT_CONNECTION_STRING="Host=127.0.0.1;Port=5432;Database=payment_service;Username=postgres;Password=postgres" \
    KC_BOOTSTRAP_ADMIN_USERNAME=admin \
    KC_BOOTSTRAP_ADMIN_PASSWORD=admin \
    KC_DB=postgres \
    KC_DB_URL=jdbc:postgresql://127.0.0.1:5432/keycloak \
    KC_DB_USERNAME=postgres \
    KC_DB_PASSWORD=postgres \
    KC_HTTP_ENABLED=true

# ---- Postgres: inicializa o cluster no primeiro boot e cria os bancos ----
RUN cat > /app/scripts/postgres.sh <<'EOF'
#!/bin/sh
set -eu

PGDATA="${PGDATA:-/app/pgdata}"
export PGDATA

if [ ! -f "$PGDATA/PG_VERSION" ]; then
  mkdir -p "$PGDATA"
  initdb -D "$PGDATA" --username=postgres --auth=trust --encoding=UTF8 --locale=C
  cat >> "$PGDATA/postgresql.conf" <<'PGCONF'
listen_addresses = '127.0.0.1'
port = 5432
unix_socket_directories = '/tmp'
shared_buffers = 128MB
PGCONF
fi

exec postgres -D "$PGDATA"
EOF

RUN cat > /app/scripts/postgres-init.sh <<'EOF'
#!/bin/sh
set -eu

until pg_isready -h 127.0.0.1 -p 5432 -U postgres >/dev/null 2>&1; do
  sleep 1
done

for db in dentist_service employee_service payment_service keycloak; do
  exists="$(psql -h 127.0.0.1 -U postgres -tAc "SELECT 1 FROM pg_database WHERE datname='$db'")"
  if [ "$exists" != "1" ]; then
    psql -h 127.0.0.1 -U postgres -c "CREATE DATABASE \"$db\""
  fi
done
EOF

# ---- Kafka (KRaft, nó único) ----
RUN cat > /app/kafka.properties <<'EOF'
process.roles=broker,controller
node.id=1
controller.quorum.bootstrap.servers=127.0.0.1:9093
listeners=PLAINTEXT://127.0.0.1:9092,CONTROLLER://127.0.0.1:9093
advertised.listeners=PLAINTEXT://localhost:9092
listener.security.protocol.map=CONTROLLER:PLAINTEXT,PLAINTEXT:PLAINTEXT
controller.listener.names=CONTROLLER
inter.broker.listener.name=PLAINTEXT
log.dirs=/app/kafka-data
num.partitions=3
default.replication.factor=1
offsets.topic.replication.factor=1
transaction.state.log.replication.factor=1
transaction.state.log.min.isr=1
group.initial.rebalance.delay.ms=0
auto.create.topics.enable=false
EOF

RUN cat > /app/scripts/kafka.sh <<'EOF'
#!/bin/sh
set -eu

CONFIG=/app/kafka.properties
if [ ! -f /app/kafka-data/meta.properties ]; then
  CLUSTER_ID="$(/opt/kafka/bin/kafka-storage.sh random-uuid)"
  /opt/kafka/bin/kafka-storage.sh format --standalone -t "$CLUSTER_ID" -c "$CONFIG"
fi

exec /opt/kafka/bin/kafka-server-start.sh "$CONFIG"
EOF

RUN cat > /app/scripts/kafka-init.sh <<'EOF'
#!/bin/sh
set -eu

until /opt/kafka/bin/kafka-topics.sh --bootstrap-server 127.0.0.1:9092 --list >/dev/null 2>&1; do
  sleep 1
done

for topic in appointments payments health-insurance appointments.dlq payments.dlq health-insurance.dlq; do
  /opt/kafka/bin/kafka-topics.sh --bootstrap-server 127.0.0.1:9092 \
    --create --if-not-exists --topic "$topic" --partitions 3 --replication-factor 1
done
EOF

RUN cat > /app/scripts/healthcheck.sh <<'EOF'
#!/bin/sh
set -eu

curl -fsS http://127.0.0.1:3000/health >/dev/null
curl -fsS http://127.0.0.1:5068/health >/dev/null
curl -sS -o /dev/null http://127.0.0.1:5070/
curl -fsS http://127.0.0.1:5127/health >/dev/null
pg_isready -h 127.0.0.1 -p 5432 -U postgres >/dev/null
bash -c 'exec 3<>/dev/tcp/127.0.0.1/9092'
curl -fsS http://127.0.0.1:8080/realms/dentist >/dev/null
EOF

# =============================================================================
# Supervisor
# =============================================================================
RUN cat > /app/supervisord.conf <<'EOF'
[supervisord]
nodaemon=true
logfile=/dev/stdout
logfile_maxbytes=0
pidfile=/tmp/supervisord.pid
childlogdir=/tmp
minfds=1024
minprocs=200

[unix_http_server]
file=/tmp/supervisor.sock
chmod=0700

[supervisorctl]
serverurl=unix:///tmp/supervisor.sock

[rpcinterface:supervisor]
supervisor.rpcinterface_factory = supervisor.rpcinterface:make_main_rpcinterface

[program:postgres]
command=/app/scripts/postgres.sh
priority=1
autostart=true
autorestart=true
startsecs=3
startretries=3
stopsignal=TERM
stopwaitsecs=30
stopasgroup=true
killasgroup=true
redirect_stderr=true
stdout_logfile=/dev/stdout
stdout_logfile_maxbytes=0
stdout_logfile_backups=0

[program:postgres-init]
command=/app/scripts/postgres-init.sh
priority=2
autostart=true
autorestart=false
startsecs=0
redirect_stderr=true
stdout_logfile=/dev/stdout
stdout_logfile_maxbytes=0
stdout_logfile_backups=0

[program:kafka]
command=/app/scripts/kafka.sh
priority=3
autostart=true
autorestart=true
startsecs=0
startretries=10
stopsignal=TERM
stopwaitsecs=30
stopasgroup=true
killasgroup=true
redirect_stderr=true
stdout_logfile=/dev/stdout
stdout_logfile_maxbytes=0
stdout_logfile_backups=0
environment=LOG_DIR="/app/kafka-logs"

[program:kafka-init]
command=/app/scripts/kafka-init.sh
priority=4
autostart=true
autorestart=false
startsecs=0
redirect_stderr=true
stdout_logfile=/dev/stdout
stdout_logfile_maxbytes=0
stdout_logfile_backups=0

[program:keycloak]
command=/opt/keycloak/bin/kc.sh start-dev --import-realm --http-port=8080 --http-host=0.0.0.0
priority=5
autostart=true
autorestart=true
startsecs=0
startretries=10
stopsignal=TERM
stopwaitsecs=30
stopasgroup=true
killasgroup=true
redirect_stderr=true
stdout_logfile=/dev/stdout
stdout_logfile_maxbytes=0
stdout_logfile_backups=0

[program:bff]
directory=/app/bff
command=node dist/main
priority=10
autostart=true
autorestart=true
startsecs=3
startretries=10
stopsignal=TERM
stopwaitsecs=15
stopasgroup=true
killasgroup=true
redirect_stderr=true
stdout_logfile=/dev/stdout
stdout_logfile_maxbytes=0
stdout_logfile_backups=0

[program:dentist-service]
directory=/app/dentist
command=dotnet Dentist-service.dll
priority=20
autostart=true
autorestart=true
# Aplica migrations no boot: reinicia até o Postgres estar disponível.
startsecs=0
startretries=10
stopsignal=TERM
stopwaitsecs=15
stopasgroup=true
killasgroup=true
redirect_stderr=true
stdout_logfile=/dev/stdout
stdout_logfile_maxbytes=0
stdout_logfile_backups=0
environment=ASPNETCORE_URLS="http://127.0.0.1:5068",ConnectionStrings__DefaultConnection="%(ENV_DENTIST_CONNECTION_STRING)s",Kafka__BootstrapServers="%(ENV_KAFKA_BOOTSTRAP_SERVERS)s",Kafka__Enabled="%(ENV_KAFKA_ENABLED)s"

[program:employee-service]
directory=/app/employee
command=dotnet Employee-service.dll
priority=20
autostart=true
autorestart=true
startsecs=0
startretries=10
stopsignal=TERM
stopwaitsecs=15
stopasgroup=true
killasgroup=true
redirect_stderr=true
stdout_logfile=/dev/stdout
stdout_logfile_maxbytes=0
stdout_logfile_backups=0
environment=ASPNETCORE_URLS="http://127.0.0.1:5070",ConnectionStrings__DefaultConnection="%(ENV_EMPLOYEE_CONNECTION_STRING)s"

[program:payment-service]
directory=/app/payment
command=dotnet PaymentService.dll
priority=20
autostart=true
autorestart=true
# Payment aplica migrations no boot: reinicia até o Postgres estar disponível.
startsecs=0
startretries=10
stopsignal=TERM
stopwaitsecs=15
stopasgroup=true
killasgroup=true
redirect_stderr=true
stdout_logfile=/dev/stdout
stdout_logfile_maxbytes=0
stdout_logfile_backups=0
environment=ASPNETCORE_URLS="http://127.0.0.1:5127",ConnectionStrings__DefaultConnection="%(ENV_PAYMENT_CONNECTION_STRING)s",Kafka__BootstrapServers="%(ENV_KAFKA_BOOTSTRAP_SERVERS)s",Kafka__Enabled="%(ENV_KAFKA_ENABLED)s"
EOF

RUN chmod +x /app/scripts/*.sh

USER app
# Somente BFF e Keycloak são expostos; os demais processos escutam em 127.0.0.1.
EXPOSE 3000 8080

HEALTHCHECK --interval=30s --timeout=15s --start-period=180s --retries=3 \
  CMD ["/app/scripts/healthcheck.sh"]

CMD ["supervisord", "-c", "/app/supervisord.conf"]
