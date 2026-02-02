# Getting Started

## Prerequisites
- Docker Desktop installed (Windows or macOS)
- Git repository cloned to your local machine

## Step 1: Restore NuGet Packages

```bash
git clone https://github.com/coltean/Challenge.git
cd Challenge
docker-compose up --build
```

# Implementation Summary

## ✅ Deliverables Checklist

### Core Requirements

- ✅ **Data Ingestion Layer**
  - Webhook endpoint at `/api/cms/events`
  - Basic Authentication (username: `cmswh_challenge`, password: a1b2c3d4-e5f6-7890-abcd-ef1234567890)
  - Batch event validation (up to 1000 events)
  - Event validation and sanitization
  - Support for publish, unpublish, and delete events
  - **Events are queued to RabbitMQ for background processing**

- ✅ **Application Layer**
  - FluentValidation for input validation
  - Comprehensive error handling
  - Version management with corner case handling
  - Hard-delete for delete events
  - Soft-delete (unpublish) for unpublish events

- ✅ **Data Storage**
  - EF Core + PostgreSQL
  - Entity version tracking
  - Latest version management
  - Idempotent event processing (EventId duplicate detection)

- ✅ **REST API**
  - GET /api/entities (list entities)
  - GET /api/entities/`{id}` (get specific entity)
  - PUT /api/entities/`{id}`/disable (admin only)
  - PUT /api/entities/`{id}`/enable (admin only)
  - Role-based access control (API_USER vs ADMIN)
  - Published entities visible to users, unpublished only to admin

- ✅ **Performance**
  - RabbitMQ queueing for fast webhook response (202 Accepted)
  - Background consumer processes events sequentially
  - Read/Write context separation
  - Query tracking disabled for read context

- ✅ **Observability**
  - Serilog logging to console and file
  - Event audit trail (WebhookEvents table)
  - Failure tracking with error messages
  - Processing timestamps
  - Health and readiness endpoints (`/health`, `/ready`)

- ✅ **Testing**
  - Integration tests for event processing
  - Authentication tests

- ✅ **.NET 9** solution
  - Targets `net9.0`
  - Uses modern C# 13 features
  - Cross-platform support

---

## 🔐 Authentication Credentials

### Three Separate Credential Sets

**CMS Webhook (Event Producer)**:
```
Username: cmswh_challenge
Password: a1b2c3d4-e5f6-7890-abcd-ef1234567890
Role: CMS_WEBHOOK
Access: POST /api/cms/events
```

**API User (Data Consumer)**:
```
Username: apiuser_demo
Password: f0e9d8c7-b6a5-4321-8765-fedcba987654
Role: API_USER
Access: GET /api/entities, GET /api/entities/{id}
```

**Admin (Management)**:
```
Username: admin
Password: 12345678-1234-1234-1234-123456789012
Role: ADMIN
Access: All API_USER endpoints + PUT /api/entities/{id}/disable + PUT /api/entities/{id}/enable
```

### DB Connection details

**PostgreSQL DB**:
- localhost:5432
- DB name: ChallengeDB
- DB User: challenge_user
- DB Password: Challenge123!@

### RabbitMQ management UI details: 

- http://localhost:15673/#/
- User:guest
- Password:guest

---

## 🧠 Event Processing Logic

### PUBLISH Event

**Trigger**: When content is ready for public view

```
If entity doesn't exist:
  ✅ Create entity
  ✅ Add version
  ✅ Mark as published
Else:
  ✅ Add new version
  ✅ Update current published version
  ✅ Keep published state
```

### UNPUBLISH Event

**Trigger**: When content should be hidden (soft-delete)

```
If entity doesn't exist OR version doest not exist:
  ✅ Log warning
  ✅ Persist event
Else If version is currently published:
  ✅ Mark version as unpublished
  ✅ Try to find previous published version
  ✅ If exists: Rollback to previous
  ✅ If not: Mark entity as unpublished
Else:
  ✅ Mark version as unpublished
```

### DELETE Event

**Trigger**: When content should be completely removed

```
Hard delete:
If entity doesn't exist:
  ✅ Log warning
  ✅ Persist event
Else:
  ✅ Remove entity
  ✅ Cascade delete all versions
```

---

## 🚦 Queue-Based Processing Decision

### Why Queueing?

Webhook requests enqueue events to RabbitMQ and return immediately. A background consumer processes events sequentially using `EventProcessingService`.

**Benefits:**
1. **Fast Webhook Response**: 202 Accepted returned quickly
2. **Isolation**: Failures don’t block incoming requests
3. **Idempotency**: Duplicate detection remains in the processing service
4. **Scalability**: Consumer can be scaled independently

---

## 🧰 Technical Stack

**Framework**: .NET 9  
**Language**: C# 13.0  
**Database**: PostgreSQL (default)  
**ORM**: Entity Framework Core 9.0  
**Validation**: FluentValidation 12.1  
**Logging**: Serilog 4.3  
**Authentication**: Custom Basic Auth handler  
**Queue**: RabbitMQ + RabbitMQ.Client  

**Packages**:
- Microsoft.EntityFrameworkCore
- Microsoft.EntityFrameworkCore.Npgsql
- FluentValidation
- Serilog
- Serilog.AspNetCore
- RabbitMQ.Client

---

## ⚠️ Assumptions and notes

- CMS is responsible for the ordering in the batch. What order it sends, we try to preserve
- The most recently published version is promoted as CurrentPublishedVersion, even if its version number is not the highest
- With single RabbitMQ consumer, prefetch=1, ack-after-processing, no requeue, we will process messages in queue order in normal operation
- Secrets are hardcoded for demo simplicity; in real deployments use env
- Everything is in one API project + one Test Project, fine for small demo project
- Controllers could be less busy
- No Mediatr usage, fine for small demos, but works well with cotext separation
- RabbitMQ is not a true event-streaming platform, but it is well suited for demos and effectively illustrates decoupling principles
- Not phisically tested on MacOS
---

## ✅ Summary

This is a **queue-based CMS webhook integration** that:
- ✅ Ingests events via secure webhook
- ✅ Validates batches (up to 1000)
- ✅ Queues events to RabbitMQ for background processing
- ✅ Manages entity versions with corner case handling
- ✅ Exposes REST API with role-based access
- ✅ Maintains full audit trail
- ✅ Includes health/readiness endpoints
- ✅ Targets .NET 9

Ready to deploy.
