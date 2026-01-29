# CMS Webhook Integration Challenge

A .NET 9 solution for ingesting CMS events via secure webhooks, managing entity versions with sophisticated corner-case handling, and exposing a secure REST API for consumers.

## Overview

This solution implements a complete event-driven architecture for syncing CMS content:

- **Webhook Endpoint**: Accepts batch events from CMS with Basic Authentication
- **Event Processing**: Synchronous processing ensures version sequencing and idempotency
- **Version Management**: Tracks entity versions with automatic rollback on unpublish
- **Dual Authentication**: Separate credentials for CMS and API consumers
- **Admin Capabilities**: Admins can disable entities locally (without affecting CMS)
- **Database Optimization**: Read/Write separation with EF Core
- **Observability**: Comprehensive logging with Serilog

## Why Synchronous Processing?

This implementation uses **synchronous (not async/queue-based) event processing** for the following reasons:

### 1. **Version Sequencing Integrity**
```
Events MUST be processed in order:
Publish v1 ? Publish v2 ? Unpublish v2

With async/queue processing:
- Messages can be reordered
- v2 could unpublish before v1 publishes (version conflict)
- Sync processing guarantees FIFO order
```

### 2. **Atomic Idempotency**
```csharp
// Sync: Duplicate check and insert happen in same transaction
using (var transaction = db.BeginTransaction())
{
    // Check EventId (already processed?)
    // If yes: skip
    // If no: process and insert
    // Commit transaction
}

// Async: Risk of duplicate between detection and queue insertion
```

### 3. **Acceptable Latency**
- Max batch size: 1000 events
- Typical DB throughput: 100-500ms per batch
- API returns `202 Accepted` immediately (user doesn't wait)
- Processing happens asynchronously on the server

### 4. **Transaction Safety**
All events in a batch are processed atomically:
- All succeed ? commit
- Any fails ? rollback entire batch (prevents partial state)

### 5. **Better Observability**
- Errors are logged immediately and synchronously
- No need to track async failures across multiple layers
- Easier debugging and audit trails

### When You'd Want Async Processing

Consider message queues (RabbitMQ, Azure Service Bus) if:
- **High volume**: >100,000 events/day with spikes
- **Variable latency**: Some events take minutes to process
- **Microservices**: Multiple independent consumers
- **Resilience**: Need automatic retries and dead-letter queues

See [Performance Considerations](#performance-considerations) for alternatives.

## Features

### Event Types

| Type | Payload | Version | Description |
|------|---------|---------|-------------|
| **publish** | Required | Required | Create/update entity with new version |
| **unpublish** | Required | Required | Disable a specific version |
| **delete** | Not allowed | Not allowed | Hard-delete entity |

### Corner Case Handling

**Scenario**: Unpublish the current published version when no prior published version exists

```
Step 1: Publish v1 ? (entity becomes published)
Step 2: Unpublish v1 ? (entity becomes unpublished)

Result: No published version exists in DB
Solution: Entity marked as unpublished, can be republished later
```

### Authentication

Two separate credential sets for different use cases:

#### CMS Webhook (System-to-System)
- **Username**: `cmswh_challenge` (10+ chars)
- **Password**: `a1b2c3d4-e5f6-7890-abcd-ef1234567890` (GUID)
- **Role**: `CMS_WEBHOOK`
- **Endpoint**: `POST /api/cms/events`

#### API User (Consumer)
- **Username**: `apiuser_demo`
- **Password**: `f0e9d8c7-b6a5-4321-8765-fedcba987654`
- **Role**: `API_USER`
- **Endpoints**: `GET /api/entities*`

#### Admin User (Management)
- **Username**: `admin`
- **Password**: `12345678-1234-1234-1234-123456789012`
- **Role**: `ADMIN`
- **Endpoints**: All + `PUT /api/entities/{id}/disable|enable`

### Data Handling

- **Publish**: Creates new version, updates `CurrentPublishedVersion`
- **Unpublish**: Marks version as unpublished, rolls back to previous version if exists
- **Delete**: Hard-deletes entity and all versions (cascading)
- **Admin Disable**: Local override (doesn't affect CMS, doesn't delete data)

## Prerequisites

- .NET 9 SDK or later
- SQL Server LocalDB (Windows) or SQL Server / PostgreSQL (Mac/Linux)
- Git

## Quick Start

### 1. Clone Repository

```bash
git clone https://github.com/coltean/Challenge.git
cd Challenge/Challenge.API
```

### 2. Restore Packages

```bash
dotnet restore
```

### 3. Create Database

```bash
dotnet ef database drop --force  # If exists
dotnet ef database update
```

### 4. Run Application

```bash
dotnet run
```

Application will start at:
- HTTPS: `https://localhost:5001`
- HTTP: `http://localhost:5000`

## API Usage

### Send Events to Webhook

```bash
curl -X POST https://localhost:5001/api/cms/events \
  -H "Content-Type: application/json" \
  -H "Authorization: Basic $(echo -n 'cmswh_challenge:a1b2c3d4-e5f6-7890-abcd-ef1234567890' | base64)" \
  -d '[
    {
      "type": "publish",
      "id": "entity-1",
      "payload": {"title": "Product 1", "price": 99.99},
      "version": 1,
      "timestamp": "2024-01-28T10:00:00Z"
    },
    {
      "type": "publish",
      "id": "entity-1",
      "payload": {"title": "Product 1 (Updated)", "price": 89.99},
      "version": 2,
      "timestamp": "2024-01-28T10:01:00Z"
    },
    {
      "type": "unpublish",
      "id": "entity-1",
      "payload": {"title": "Product 1 (Updated)", "price": 89.99},
      "version": 2,
      "timestamp": "2024-01-28T10:02:00Z"
    },
    {
      "type": "delete",
      "id": "entity-2",
      "timestamp": "2024-01-28T10:03:00Z"
    }
  ]'
```

**Response** (202 Accepted):
```json
{
  "message": "Batch of 4 events accepted for processing"
}
```

### List Published Entities

```bash
curl https://localhost:5001/api/entities \
  -H "Authorization: Basic $(echo -n 'apiuser_demo:f0e9d8c7-b6a5-4321-8765-fedcba987654' | base64)"
```

**Response** (200 OK):
```json
[
  {
    "id": "entity-1",
    "currentPublishedVersion": 1,
    "isPublished": true,
    "isDisabledByAdmin": false,
    "latestPayload": "{\"title\":\"Product 1\",\"price\":99.99}",
    "createdAt": "2024-01-28T10:00:00Z",
    "updatedAt": "2024-01-28T10:00:00Z"
  }
]
```

### Get Specific Entity

```bash
curl https://localhost:5001/api/entities/entity-1 \
  -H "Authorization: Basic $(echo -n 'apiuser_demo:f0e9d8c7-b6a5-4321-8765-fedcba987654' | base64)"
```

### Admin: Disable Entity (Local Override)

```bash
curl -X PUT https://localhost:5001/api/entities/entity-1/disable \
  -H "Authorization: Basic $(echo -n 'admin:12345678-1234-1234-1234-123456789012' | base64)"
```

### Admin: Enable Entity

```bash
curl -X PUT https://localhost:5001/api/entities/entity-1/enable \
  -H "Authorization: Basic $(echo -n 'admin:12345678-1234-1234-1234-123456789012' | base64)"
```

## Database Schema

### Entity Table
Represents a CMS entity with version tracking.

| Column | Type | Description |
|--------|------|-------------|
| `Id` | string (PK) | External entity ID from CMS |
| `CurrentPublishedVersion` | int | Latest published version |
| `IsPublished` | bool | Whether entity is currently published |
| `IsDisabledByAdmin` | bool | Local admin override (doesn't affect CMS) |
| `CreatedAt` | datetime | Creation timestamp |
| `UpdatedAt` | datetime | Last update timestamp |
| `DeletedAt` | datetime | Soft-delete timestamp (unused - hard-delete) |

**Indexes**:
- `IsPublished` (filtering published entities)
- `IsDisabledByAdmin` (filtering disabled entities)
- `DeletedAt` (for audit)

### EntityVersion Table
Immutable version history for each entity.

| Column | Type | Description |
|--------|------|-------------|
| `Id` | int (PK) | Auto-incrementing ID |
| `EntityId` | string (FK) | Reference to Entity |
| `VersionNumber` | int | Version number (1, 2, 3...) |
| `Payload` | nvarchar(max) | JSON data |
| `IsPublished` | bool | Whether this version is published |
| `PublishedAt` | datetime | Publication timestamp |
| `UnpublishedAt` | datetime | Unpublish timestamp (nullable) |

**Indexes**:
- `(EntityId, VersionNumber)` - Unique constraint on version per entity
- `IsPublished` (filtering published versions)

### WebhookEvent Table
Audit trail of all processed webhook events.

| Column | Type | Description |
|--------|------|-------------|
| `Id` | string (PK) | Unique event ID |
| `EventId` | string | Deterministic hash (for duplicate detection) |
| `EventType` | string | "publish", "unpublish", "delete" |
| `EntityId` | string | Entity being modified |
| `Version` | int | Version number (nullable for delete) |
| `Payload` | nvarchar(max) | Event payload (nullable) |
| `Timestamp` | datetime | Event timestamp from CMS |
| `ProcessedAt` | datetime | Server processing time |
| `IsProcessed` | bool | Success flag |
| `ErrorMessage` | string | Error message (nullable) |

**Indexes**:
- `EventId` - Unique (duplicate detection)
- `IsProcessed` (audit queries)
- `ProcessedAt` (time-range queries)

## Input Validation & Sanitization

All incoming events are validated before processing:

### Validation Rules

| Field | Rules |
|-------|-------|
| **type** | Required, one of: "publish", "unpublish", "delete" |
| **id** | Required, max 255 chars, alphanumeric + `-_.` |
| **version** | Required for publish/unpublish, >0, null for delete |
| **payload** | Required for publish/unpublish, null/absent for delete |
| **timestamp** | Required, not future-dated (5s tolerance) |
| **batch size** | Max 1000 events per request |

### Error Handling

Invalid requests return `400 Bad Request` with detailed error messages:

```json
{
  "errors": [
    "Type must be 'publish', 'unpublish', or 'delete'",
    "Id must not exceed 255 characters"
  ]
}
```

## Database Optimization

### Read/Write Separation

```csharp
// Read queries use no-tracking (memory efficient)
var context = new ReadOnlyDbContext();
var entities = await context.Entities.AsNoTracking().ToListAsync();

// Webhook processing uses full tracking
var context = new ApplicationDbContext();
await context.SaveChangesAsync();
```

### Query Optimization Tips

1. **Explicit Includes**: Avoid N+1 queries
```csharp
var entities = await context.Entities
    .Include(e => e.Versions)
    .ToListAsync();
```

2. **Indexes on Filters**:
```csharp
// Indexed columns
.Where(e => e.IsPublished)
.Where(e => !e.IsDisabledByAdmin)
```

3. **Async All The Way**:
```csharp
// Good
var entity = await context.Entities.FirstOrDefaultAsync(e => e.Id == id);

// Bad
var entity = context.Entities.FirstOrDefault(e => e.Id == id);
```

## Logging & Observability

### Log Levels

| Level | Usage |
|-------|-------|
| **Information** | Event processing, auth success, database operations |
| **Warning** | Duplicate events, missing entities, validation failures |
| **Error** | Processing failures, unexpected exceptions |
| **Fatal** | Database connection failures |

### Log Output

Logs are written to:
1. **Console**: Real-time debugging (Development)
2. **Rolling Files**: `logs/cms-webhook-YYYY-MM-DD.txt` (daily rotation)

Example log output:
```
[2024-01-28 10:00:15] [INF] Received batch of 4 events from CMS
[2024-01-28 10:00:15] [INF] Authentication successful for user 'cmswh_challenge' with role 'CMS_WEBHOOK'
[2024-01-28 10:00:15] [INF] Successfully processed publish event for entity entity-1 version 1
[2024-01-28 10:00:15] [INF] Successfully committed batch of 4 events
```

## Configuration

### appsettings.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=ChallengeDB;Trusted_Connection=true;"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

### Environment-Specific Settings

For different environments:

**macOS/Linux with PostgreSQL**:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=challengedb;Username=postgres;Password=postgres"
  }
}
```

**Docker SQL Server**:
```bash
docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=YourPassword123" \
  -p 1433:1433 -d mcr.microsoft.com/mssql/server
```

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost,1433;Database=ChallengeDB;User Id=sa;Password=YourPassword123;"
  }
}
```

## Performance Considerations

### Benchmarks

Single event processing time:
- Validation: ~1ms
- Database write: ~5-10ms
- Total: ~10-15ms per event

Batch performance:
- 100 events: ~150-200ms
- 1000 events: ~1.5-2s
- Acceptable for 202 Accepted response

### Scaling Strategies

#### Scenario 1: Few Events, High Latency Tolerance
**Current implementation (synchronous)** is optimal.

#### Scenario 2: Many Events, Need Async Processing
Implement message queue:

```csharp
// 1. Controller enqueues events
await _messageQueue.EnqueueAsync(events);
return Accepted(); // Return immediately

// 2. Background worker processes
var events = await _messageQueue.DequeueAsync(100);
await _eventProcessor.ProcessAsync(events);
```

Options:
- **RabbitMQ**: Open source, on-premises
- **Azure Service Bus**: Managed, cloud-native
- **AWS SQS**: Managed, AWS ecosystem
- **Hangfire**: Background job scheduler with UI

#### Scenario 3: High Volume, Complex Logic
Implement event sourcing:

```csharp
// All events stored immutably
var eventLog = new[] {
    new { type: "publish", version: 1, timestamp: ... },
    new { type: "publish", version: 2, timestamp: ... }
};

// Async projections to read model
await ProjectToReadModel(eventLog);
```

## Testing

### Running Tests

```bash
# Run all tests
dotnet test

# Run with verbose output
dotnet test --verbosity detailed

# Run specific test class
dotnet test --filter "TestClass=EventProcessingServiceTests"
```

### Test Coverage

Test categories:

1. **Event Processing Tests**
   - Publish creates new entity ?
   - Publish with existing entity updates version ?
   - Unpublish marks version as unpublished ?
   - Unpublish with no prior version makes entity unpublished ?
   - Delete hard-deletes entity ?

2. **Validation Tests**
   - Invalid event type rejected ?
   - Missing required fields rejected ?
   - Future timestamp rejected ?
   - Oversized batch rejected ?

3. **Authentication Tests**
   - Valid credentials accepted ?
   - Invalid password rejected ?
   - Missing auth header rejected ?
   - Wrong scheme rejected ?

4. **Authorization Tests**
   - CMS user can access webhook ?
   - API user cannot access webhook ?
   - Admin can disable entities ?
   - Regular user cannot disable ?

## Troubleshooting

### Database Connection Error

```
Error: Cannot connect to server (localdb)\mssqllocaldb
```

**Solution**: 
- Windows: Install SQL Server Express LocalDB or use Docker
- Mac/Linux: Use PostgreSQL or Docker SQL Server

### Authentication Fails

```
Error: Invalid username or password
```

**Check**:
- Credentials in Authorization header are correct
- Base64 encoding is valid
- Password hasn't been changed in appsettings

### Events Not Processing

**Check**:
- Logs in `logs/` directory for error messages
- WebhookEvents table for `IsProcessed = false` with `ErrorMessage`
- Database connectivity

### Port Already in Use

```bash
# Change port in appsettings.json or via environment variable
dotnet run --urls "https://localhost:5555"
```

## Production Checklist

- [ ] Use secrets management (Azure Key Vault, AWS Secrets Manager)
- [ ] Rotate credentials regularly
- [ ] Enable HTTPS only (disable HTTP)
- [ ] Set up monitoring and alerting
- [ ] Configure log retention policies
- [ ] Test disaster recovery and backups
- [ ] Load test with expected volume
- [ ] Set up CI/CD pipeline
- [ ] Implement rate limiting
- [ ] Configure database connection pooling

## Architecture Diagram

```
???????????????????????????????????????????????????
? CMS System (External)                           ?
???????????????????????????????????????????????????
                     ? HTTP POST + Basic Auth
                     ?
         ?????????????????????????????????
         ? POST /api/cms/events          ?
         ? WebhookController             ?
         ? [Authorization: CMS_WEBHOOK]  ?
         ?????????????????????????????????
                      ? Validate & Sanitize
                      ?
         ??????????????????????????????????
         ? CmsEventValidator              ?
         ? BatchEventValidator            ?
         ??????????????????????????????????
                      ? Synchronous
                      ?
         ????????????????????????????????????????
         ? EventProcessingService               ?
         ? ProcessEventsAsync(batch)            ?
         ? ?? Duplicate detection (idempotent) ?
         ? ?? Handle Publish (version create)  ?
         ? ?? Handle Unpublish (rollback)      ?
         ? ?? Handle Delete (hard delete)      ?
         ????????????????????????????????????????
                      ? Atomic Transaction
                      ?
         ????????????????????????????????????
         ? ApplicationDbContext              ?
         ? (Read/Write)                      ?
         ? ?? Entities                       ?
         ? ?? EntityVersions                 ?
         ? ?? WebhookEvents (audit)          ?
         ????????????????????????????????????
                      ?
                      ?
         ????????????????????????????????????
         ? SQL Server / PostgreSQL           ?
         ????????????????????????????????????

API Consumers
         ? HTTP GET + Basic Auth
         ?
????????????????????????????????????
? GET /api/entities                ?
? GET /api/entities/{id}           ?
? EntitiesController               ?
? [Authorization: API_USER,ADMIN]  ?
????????????????????????????????????
             ? Read-Only Queries
             ?
????????????????????????????????????
? ReadOnlyDbContext                ?
? (No-Tracking, Query Only)        ?
????????????????????????????????????
             ?
             ?
????????????????????????????????????
? SQL Server / PostgreSQL (Read)   ?
????????????????????????????????????
```

## Security Considerations

### Basic Authentication
- ? Simple and widely supported
- ?? Credentials sent Base64-encoded (not encrypted)
- ?? **Always use HTTPS** in production

### Input Validation
- All inputs validated with FluentValidation
- Entity IDs sanitized (alphanumeric only)
- JSON payloads stored as-is (validated by consumer)
- Batch size limited to 1000 events

### Database Security
- Entity Framework parameterized queries (SQL injection prevention)
- Read/Write separation (principle of least privilege)
- No direct SQL access

### Role-Based Access Control (RBAC)
- `CMS_WEBHOOK`: Can only post to webhook
- `API_USER`: Can only read published entities
- `ADMIN`: Can manage entity state locally

## Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing`)
3. Commit changes (`git commit -am 'Add amazing feature'`)
4. Push to branch (`git push origin feature/amazing`)
5. Open a Pull Request

## License

MIT License - See LICENSE file

## Support

For issues and questions:
1. Check the [Troubleshooting](#troubleshooting) section
2. Review logs in `logs/` directory
3. Open an issue on GitHub

---

**Last Updated**: 2024-01-28  
**Maintainer**: Challenge Contributors
