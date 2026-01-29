# API Reference Guide

## Base URL

```
https://localhost:5001/api
```

All endpoints require **Basic Authentication**.

---

## Authentication

### Credentials

Three credential sets with different roles:

| Role | Username | Password | Access |
|------|----------|----------|--------|
| **CMS_WEBHOOK** | `cmswh_challenge` | `a1b2c3d4-e5f6-7890-abcd-ef1234567890` | POST /cms/events |
| **API_USER** | `apiuser_demo` | `f0e9d8c7-b6a5-4321-8765-fedcba987654` | GET /entities* |
| **ADMIN** | `admin` | `12345678-1234-1234-1234-123456789012` | GET /entities* + PUT /entities/{id}/disable\|enable |

### Basic Auth Header

```
Authorization: Basic <base64(username:password)>
```

**Example**:
```
Username: cmswh_challenge
Password: a1b2c3d4-e5f6-7890-abcd-ef1234567890

Base64: Y21zd2hfY2hhbGxlbmdlOmExYjJjM2Q0LWU1ZjYtNzg5MC1hYmNkLWVmMTIzNDU2Nzg5MA==

Header: Authorization: Basic Y21zd2hfY2hhbGxlbmdlOmExYjJjM2Q0LWU1ZjYtNzg5MC1hYmNkLWVmMTIzNDU2Nzg5MA==
```

---

## Webhook Endpoint

### POST /cms/events

Receive batch of CMS events (publish, unpublish, delete).

**Authentication**: CMS_WEBHOOK role required

**Request**:
```http
POST /api/cms/events HTTP/1.1
Host: localhost:5001
Content-Type: application/json
Authorization: Basic cmNtc3doX2NoYWxsZW5nZTphMWIyYzNkNC1lNWY2LTc4OTAtYWJjZC1lZjEyMzQ1Njc4OTA=

[
  {
    "type": "publish",
    "id": "entity-1",
    "version": 1,
    "payload": {
      "title": "Product A",
      "price": 99.99,
      "description": "Initial version"
    },
    "timestamp": "2024-01-28T10:00:00Z"
  },
  {
    "type": "publish",
    "id": "entity-1",
    "version": 2,
    "payload": {
      "title": "Product A (Updated)",
      "price": 89.99,
      "description": "Price reduced"
    },
    "timestamp": "2024-01-28T11:00:00Z"
  },
  {
    "type": "unPublish",
    "id": "entity-1",
    "version": 2,
    "payload": {
      "title": "Product A (Updated)",
      "price": 89.99
    },
    "timestamp": "2024-01-28T11:30:00Z"
  },
  {
    "type": "delete",
    "id": "entity-2",
    "timestamp": "2024-01-28T12:00:00Z"
  }
]
```

**Parameters**:

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| type | string | Yes | Event type: "publish", "unPublish", or "delete" |
| id | string | Yes | Entity ID (max 255 chars, alphanumeric + `-_.`) |
| version | integer | No* | Version number (required for publish/unPublish) |
| payload | object | No* | Entity data (required for publish/unPublish) |
| timestamp | string (ISO 8601) | Yes | Event timestamp (must not be future-dated) |

*Conditional on event type

**Response (202 Accepted)**:
```json
{
  "message": "Batch of 4 events accepted for processing"
}
```

**Response (400 Bad Request)**:
```json
{
  "errors": [
    "Type must be 'publish', 'unPublish', or 'delete'",
    "Id must not exceed 255 characters"
  ]
}
```

**Response (401 Unauthorized)**:
```json
{
  "error": "Invalid username or password"
}
```

**Response (500 Internal Server Error)**:
```json
{
  "error": "Internal server error while processing events"
}
```

### Curl Example

```bash
curl -X POST https://localhost:5001/api/cms/events \
  -H "Content-Type: application/json" \
  -H "Authorization: Basic cmNtc3doX2NoYWxsZW5nZTphMWIyYzNkNC1lNWY2LTc4OTAtYWJjZC1lZjEyMzQ1Njc4OTA=" \
  -d '[
    {
      "type": "publish",
      "id": "test-entity",
      "version": 1,
      "payload": {"name": "Test"},
      "timestamp": "2024-01-28T10:00:00Z"
    }
  ]' \
  --insecure
```

---

## REST API Endpoints

### GET /entities

List all entities.

**Authentication**: API_USER or ADMIN role required

**Query Parameters**: None

**Response (200 OK)**:
```json
[
  {
    "id": "entity-1",
    "currentPublishedVersion": 1,
    "isPublished": true,
    "isDisabledByAdmin": false,
    "latestPayload": "{\"title\":\"Product A\",\"price\":99.99}",
    "createdAt": "2024-01-28T10:00:00Z",
    "updatedAt": "2024-01-28T10:00:00Z"
  }
]
```

**Behavior**:
- **Regular User** (API_USER): Only published entities
- **Admin**: All entities including unpublished and admin-disabled

### Curl Example

```bash
curl https://localhost:5001/api/entities \
  -H "Authorization: Basic YXBpdXNlcl9kZW1vOmYwZTlkOGM3LWI2YTUtNDMyMS04NzY1LWZlZGNiYTk4NzY1NA==" \
  --insecure
```

---

### GET /entities/{id}

Get a specific entity by ID.

**Authentication**: API_USER or ADMIN role required

**Path Parameters**:
| Parameter | Type | Description |
|-----------|------|-------------|
| id | string | Entity ID |

**Response (200 OK)**:
```json
{
  "id": "entity-1",
  "currentPublishedVersion": 1,
  "isPublished": true,
  "isDisabledByAdmin": false,
  "latestPayload": "{\"title\":\"Product A\",\"price\":99.99}",
  "createdAt": "2024-01-28T10:00:00Z",
  "updatedAt": "2024-01-28T10:00:00Z"
}
```

**Response (404 Not Found)**:
```json
{
  "error": "Entity entity-1 not found"
}
```

**Response (401 Unauthorized)**:
```json
{
  "error": "Invalid username or password"
}
```

### Curl Example

```bash
curl https://localhost:5001/api/entities/entity-1 \
  -H "Authorization: Basic YXBpdXNlcl9kZW1vOmYwZTlkOGM3LWI2YTUtNDMyMS04NzY1LWZlZGNiYTk4NzY1NA==" \
  --insecure
```

---

### PUT /entities/{id}/disable

Disable an entity (admin only).

This is a **local override** that doesn't affect CMS data. Users cannot access disabled entities.

**Authentication**: ADMIN role required

**Path Parameters**:
| Parameter | Type | Description |
|-----------|------|-------------|
| id | string | Entity ID |

**Request**:
```http
PUT /api/entities/entity-1/disable HTTP/1.1
Host: localhost:5001
Authorization: Basic YWRtaW46MTIzNDU2NzgtMTIzNC0xMjM0LTEyMzQtMTIzNDU2Nzg5MDEy
```

**Response (204 No Content)**:
```
(empty body)
```

**Response (404 Not Found)**:
```json
{
  "error": "Entity entity-1 not found"
}
```

**Response (401 Unauthorized)**:
```json
{
  "error": "Invalid username or password"
}
```

**Response (403 Forbidden)**:
```
(User does not have ADMIN role)
```

### Curl Example

```bash
curl -X PUT https://localhost:5001/api/entities/entity-1/disable \
  -H "Authorization: Basic YWRtaW46MTIzNDU2NzgtMTIzNC0xMjM0LTEyMzQtMTIzNDU2Nzg5MDEy" \
  --insecure
```

---

### PUT /entities/{id}/enable

Enable an entity (admin only).

Removes the admin-only disable flag. Opposite of `/disable`.

**Authentication**: ADMIN role required

**Path Parameters**:
| Parameter | Type | Description |
|-----------|------|-------------|
| id | string | Entity ID |

**Request**:
```http
PUT /api/entities/entity-1/enable HTTP/1.1
Host: localhost:5001
Authorization: Basic YWRtaW46MTIzNDU2NzgtMTIzNC0xMjM0LTEyMzQtMTIzNDU2Nzg5MDEy
```

**Response (204 No Content)**:
```
(empty body)
```

**Response (404 Not Found)**:
```json
{
  "error": "Entity entity-1 not found"
}
```

### Curl Example

```bash
curl -X PUT https://localhost:5001/api/entities/entity-1/enable \
  -H "Authorization: Basic YWRtaW46MTIzNDU2NzgtMTIzNC0xMjM0LTEyMzQtMTIzNDU2Nzg5MDEy" \
  --insecure
```

---

## Event Types Reference

### PUBLISH Event

Create or update entity (both Add and Update operations).

```json
{
  "type": "publish",
  "id": "product-101",
  "version": 1,
  "payload": {
    "title": "Product Name",
    "description": "Product description",
    "price": 99.99
  },
  "timestamp": "2024-01-28T10:00:00Z"
}
```

**Rules**:
- `id`: Required, max 255 chars
- `version`: Required, must be > 0
- `payload`: Required, must be valid JSON
- `timestamp`: Required, must not be future-dated

**Behavior**:
- If entity doesn't exist: Create entity + publish version
- If entity exists: Add new version + update current published version
- Sets `IsPublished = true`

---

### UNPUBLISH Event

Unpublish/disable a specific version (soft-delete).

```json
{
  "type": "unPublish",
  "id": "product-101",
  "version": 2,
  "payload": {
    "title": "Product Name (Updated)",
    "price": 89.99
  },
  "timestamp": "2024-01-28T11:30:00Z"
}
```

**Rules**:
- `id`: Required, max 255 chars
- `version`: Required, must be > 0
- `payload`: Required (for audit trail)
- `timestamp`: Required, must not be future-dated

**Behavior**:
- Marks specific version as unpublished
- If unpublishing current version:
  - Tries to rollback to previous published version
  - If no previous version exists: entity becomes unpublished
- Data is preserved in database
- Can republish later

---

### DELETE Event

Hard-delete entity (completely remove).

```json
{
  "type": "delete",
  "id": "product-101",
  "timestamp": "2024-01-28T12:00:00Z"
}
```

**Rules**:
- `id`: Required, max 255 chars
- `version`: Not allowed (must be null/omitted)
- `payload`: Not allowed (must be null/omitted)
- `timestamp`: Required, must not be future-dated

**Behavior**:
- Completely removes entity from database
- Cascades delete to all versions
- Data cannot be recovered
- Entity permanently removed

---

## Response Status Codes

| Code | Meaning | When |
|------|---------|------|
| **200** | OK | GET request successful |
| **202** | Accepted | Batch events accepted for processing |
| **204** | No Content | PUT request successful (disable/enable) |
| **400** | Bad Request | Invalid input (validation errors) |
| **401** | Unauthorized | Missing/invalid authentication |
| **403** | Forbidden | Authenticated but not authorized for action |
| **404** | Not Found | Entity doesn't exist |
| **500** | Server Error | Unexpected error during processing |

---

## Example Workflows

### Workflow 1: Create, Update, Publish, Unpublish

```bash
# Step 1: Create entity (publish v1)
curl -X POST https://localhost:5001/api/cms/events \
  -H "Authorization: Basic cmNtc3doX2NoYWxsZW5nZTphMWIyYzNkNC1lNWY2LTc4OTAtYWJjZC1lZjEyMzQ1Njc4OTA=" \
  -H "Content-Type: application/json" \
  -d '[
    {
      "type": "publish",
      "id": "prod-1",
      "version": 1,
      "payload": {"name": "Product", "price": 100},
      "timestamp": "2024-01-28T10:00:00Z"
    }
  ]'

# Step 2: Update entity (publish v2)
curl -X POST https://localhost:5001/api/cms/events \
  -H "Authorization: Basic cmNtc3doX2NoYWxsZW5nZTphMWIyYzNkNC1lNWY2LTc4OTAtYWJjZC1lZjEyMzQ1Njc4OTA=" \
  -H "Content-Type: application/json" \
  -d '[
    {
      "type": "publish",
      "id": "prod-1",
      "version": 2,
      "payload": {"name": "Product (Updated)", "price": 90},
      "timestamp": "2024-01-28T11:00:00Z"
    }
  ]'

# Step 3: Check current entity
curl https://localhost:5001/api/entities/prod-1 \
  -H "Authorization: Basic YXBpdXNlcl9kZW1vOmYwZTlkOGM3LWI2YTUtNDMyMS04NzY1LWZlZGNiYTk4NzY1NA=="
# Returns v2 (latest)

# Step 4: Unpublish v2 (rollback to v1)
curl -X POST https://localhost:5001/api/cms/events \
  -H "Authorization: Basic cmNtc3doX2NoYWxsZW5nZTphMWIyYzNkNC1lNWY2LTc4OTAtYWJjZC1lZjEyMzQ1Njc4OTA=" \
  -H "Content-Type: application/json" \
  -d '[
    {
      "type": "unPublish",
      "id": "prod-1",
      "version": 2,
      "payload": {"name": "Product (Updated)", "price": 90},
      "timestamp": "2024-01-28T12:00:00Z"
    }
  ]'

# Step 5: Check entity again
curl https://localhost:5001/api/entities/prod-1 \
  -H "Authorization: Basic YXBpdXNlcl9kZW1vOmYwZTlkOGM3LWI2YTUtNDMyMS04NzY1LWZlZGNiYTk4NzY1NA=="
# Returns v1 (rolled back)
```

### Workflow 2: Admin Disable/Enable

```bash
# Admin disables entity for users (local override)
curl -X PUT https://localhost:5001/api/entities/prod-1/disable \
  -H "Authorization: Basic YWRtaW46MTIzNDU2NzgtMTIzNC0xMjM0LTEyMzQtMTIzNDU2Nzg5MDEy"

# Regular user no longer sees entity
curl https://localhost:5001/api/entities/prod-1 \
  -H "Authorization: Basic YXBpdXNlcl9kZW1vOmYwZTlkOGM3LWI2YTUtNDMyMS04NzY1LWZlZGNiYTk4NzY1NA=="
# Returns 404 Not Found

# Admin can still see it
curl https://localhost:5001/api/entities/prod-1 \
  -H "Authorization: Basic YWRtaW46MTIzNDU2NzgtMTIzNC0xMjM0LTEyMzQtMTIzNDU2Nzg5MDEy"
# Returns entity with isDisabledByAdmin: true

# Admin re-enables it
curl -X PUT https://localhost:5001/api/entities/prod-1/enable \
  -H "Authorization: Basic YWRtaW46MTIzNDU2NzgtMTIzNC0xMjM0LTEyMzQtMTIzNDU2Nzg5MDEy"

# Regular user can see it again
curl https://localhost:5001/api/entities/prod-1 \
  -H "Authorization: Basic YXBpdXNlcl9kZW1vOmYwZTlkOGM3LWI2YTUtNDMyMS04NzY1LWZlZGNiYTk4NzY1NA=="
# Returns entity
```

---

## Rate Limiting

**Batch size limits**:
- Minimum: 1 event per batch
- Maximum: 1000 events per batch
- Exceeding limit returns: `400 Bad Request`

**Recommended**:
- Send batches of 50-100 events for optimal performance
- Spread high-volume batches over time to avoid server load

---

## Best Practices

### 1. Use Deterministic Timestamps
```json
{
  "timestamp": "2024-01-28T10:00:00Z"  // ? ISO 8601 with Z
}
```

### 2. Include Payload in Unpublish
```json
{
  "type": "unPublish",
  "id": "entity-1",
  "version": 2,
  "payload": { ... },  // ? Always include
  "timestamp": "2024-01-28T11:00:00Z"
}
```

### 3. Order Events by Timestamp
```json
[
  { "timestamp": "2024-01-28T10:00:00Z", ... },  // First
  { "timestamp": "2024-01-28T10:01:00Z", ... },  // Second
  { "timestamp": "2024-01-28T10:02:00Z", ... }   // Third
]
```

### 4. Use Reasonable Batch Sizes
- Batches of 50-100 events: Optimal
- Batches of 1000+ events: Works but slower
- Single events: Works but inefficient

### 5. Implement Retry Logic
```csharp
// If receiving 500 error, retry after exponential backoff
int maxRetries = 3;
for (int i = 0; i < maxRetries; i++)
{
    var response = await SendEvents(batch);
    if (response.IsSuccessStatusCode) break;
    
    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, i)));
}
```

---

## Debugging

### Check Event Processing Status

Query the `WebhookEvents` table:

```sql
-- Find failed events
SELECT EventId, EventType, EntityId, ErrorMessage, ProcessedAt
FROM WebhookEvents
WHERE IsProcessed = 0
ORDER BY ProcessedAt DESC;

-- Find processing performance
SELECT EventType, COUNT(*) as Count, AVG(DATEDIFF(ms, Timestamp, ProcessedAt)) as AvgMs
FROM WebhookEvents
WHERE IsProcessed = 1
GROUP BY EventType;
```

### View Logs

**Console**:
```bash
# Tail logs while running
tail -f logs/cms-webhook-*.txt | grep ERROR
```

**Find entity version history**:
```sql
SELECT *
FROM EntityVersions
WHERE EntityId = 'entity-1'
ORDER BY VersionNumber DESC;
```

---

**Last Updated**: 2024-01-28  
**Version**: 1.0.0
