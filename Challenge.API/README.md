# CMS Webhook Integration Challenge

A .NET 9 webhook that ingests CMS batches, validates them, and enqueues the work for a background worker to persist to PostgreSQL.

## Overview

- **Webhook Endpoint**: `POST /api/cms/events` with `CMS_WEBHOOK` role and Basic Authentication
- **Event Processing**: Validated batches are serialized and published to RabbitMQ
- **Persistence Worker**: Background service (or separate host) consumes the queue, runs `EventProcessingService`, and keeps the existing version/idempotency guarantees
- **Read/Write Separation**: `ApplicationDbContext` for writes, `ReadOnlyDbContext` for API queries
- **Observability**: Serilog logs to console and rolling files

## Queue-Based Processing

1. The controller validates each incoming batch with `BatchEventValidator`.
2. Validated payloads are serialized and sent to the RabbitMQ queue named `cms-events`.
3. A consumer (Docker container worker, background hosted service, etc.) reads the queue and invokes `EventProcessingService.ProcessEventsAsync`, keeping the synchronous transaction semantics for version ordering and idempotency.
4. The webhook responds with `202 Accepted` as soon as the batch is queued, keeping HTTP latency low while persistence happens asynchronously.

See [`SYNC_VS_ASYNC_DECISION.md`](SYNC_VS_ASYNC_DECISION.md) for the reasoning that led to the queue-based architecture.

## Quick Start

1. Restore packages: `dotnet restore`
2. Apply migrations: `dotnet ef database update`
3. Run the API: `dotnet run`

The API listens on `https://localhost:5001` and `http://localhost:5000` by default.

## Configuration

`appsettings.json` includes database and RabbitMQ settings. Update the sections below for your environment.

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=ChallengeDB;Username=challenge_user;Password=Challenge123!@;"
  },
  "RabbitMq": {
    "HostName": "rabbitmq",
    "Port": 5672,
    "UserName": "guest",
    "Password": "guest",
    "QueueName": "cms-events"
  }
}
```

Use Docker Compose to wire RabbitMQ (`rabbitmq` hostname) and PostgreSQL to the API, or point to managed services with the same JSON structure.

## Authentication

- **CMS webhook**: `cmswh_challenge` / `a1b2c3d4-e5f6-7890-abcd-ef1234567890` (role `CMS_WEBHOOK`)
- **API read**: `apiuser_demo` / `f0e9d8c7-b6a5-4321-8765-fedcba987654` (role `API_USER`)
- **Admin**: `admin` / `12345678-1234-1234-1234-123456789012` (role `ADMIN`)

All endpoints require Basic Authentication over HTTPS.

## API Usage

### Send a Batch to the Webhook

```bash
curl -X POST https://localhost:5001/api/cms/events \
  -H "Content-Type: application/json" \
  -H "Authorization: Basic $(echo -n 'cmswh_challenge:a1b2c3d4-e5f6-7890-abcd-ef1234567890' | base64)" \
  -d '[{...}]'
```

**Response**

```json
{ "message": "Batch of X events accepted for processing" }
```

### List Entities

```bash
curl https://localhost:5001/api/entities \
  -H "Authorization: Basic $(echo -n 'apiuser_demo:f0e9d8c7-b6a5-4321-8765-fedcba987654' | base64)"
```

### Disable/Enable Entity (Admin)

```bash
curl -X PUT https://localhost:5001/api/entities/{id}/disable \
  -H "Authorization: Basic $(echo -n 'admin:12345678-1234-1234-1234-123456789012' | base64)"
```

## Troubleshooting

- **RabbitMQ unreachable**: Verify credentials, hostnames, and firewall rules. Use `rabbitmqctl` inside the container for diagnostics.
- **Database migrations fail**: Ensure your connection string points to a running PostgreSQL instance and the migrations have not already been applied.
- **Batches rejected**: Check `logs/` for validation errors and ensure payloads meet the DTO constraints.

## Support

If you run into issues:

1. Inspect the `logs/` folder for Serilog output.
2. Confirm RabbitMQ queue `cms-events` is declared and messages arrive.
3. Make sure the worker/deployment that reads from RabbitMQ is running and has access to the same database.
4. Ask via GitHub Issues in the upstream repository.

---

**Last Updated**: 2024-01-28  
**Maintainer**: Challenge Contributors
