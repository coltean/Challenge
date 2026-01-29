# Docker - Simple 3-Step Setup

## You Don't Need to Manually Apply Migrations!

Your `Program.cs` **already applies migrations automatically** when the API starts.

Here's all you need to do:

---

## Step 1: Build Docker Images

```bash
cd C:\Work\Challenge

docker-compose -f docker-compose.override.yml build
```

This takes 2-3 minutes. You'll see:
- PostgreSQL image being pulled
- Your .NET app being compiled and published

**Done when you see**: "Successfully built..."

---

## Step 2: Start Containers

```bash
docker-compose -f docker-compose.override.yml up -d
```

This starts:
- PostgreSQL container (database)
- Your API container

The `-d` means "detached" (runs in background).

**Immediately** you'll see something like:
```
Creating challenge-db ... done
Creating challenge-api ... done
```

---

## Step 3: Wait for Setup & Verify

Wait **20-30 seconds** for the database to be ready and migrations to apply.

Then verify everything works:

```bash
# Check containers are running
docker-compose -f docker-compose.override.yml ps
```

You should see:
```
NAME             STATUS              PORTS
challenge-api    Up 1 minute        0.0.0.0:5000->5000/tcp
challenge-db     Up 1 minute        0.0.0.0:5432->5432/tcp
```

---

## ? Verify It All Works

### Test 1: Health Check
```bash
curl http://localhost:5000/health
```

Should return:
```json
{"status":"healthy","timestamp":"2024-01-28T10:00:00Z","environment":"Production"}
```

### Test 2: Swagger UI
Open browser:
```
http://localhost:5000/swagger
```

You should see the API documentation.

### Test 3: Check Migrations Applied
```bash
docker-compose -f docker-compose.override.yml logs api | findstr /i "migrat"
```

You should see:
```
Applying database migrations...
Database migrations completed successfully
```

**If you see these messages, you're done!** ?

---

## ?? That's It!

Your API is now running with:
- ? PostgreSQL database
- ? Tables created
- ? Migrations applied
- ? Ready to accept requests

---

## ?? What Just Happened

```
1. docker-compose build
   ?? Built Docker image with your .NET 9 app
   ?? Downloaded PostgreSQL image

2. docker-compose up -d
   ?? Started PostgreSQL container
   ?? Started your API container
   ?? Connected them via network (challenge-network)
   ?? Exposed ports (5000, 5432)

3. Program.cs startup (automatic)
   ?? Waited for database to be ready
   ?? Applied migrations from Migrations/ folders
   ?? Created tables (Entities, EntityVersions, WebhookEvents)
   ?? Started API listening on :5000
```

---

## ?? Stop When Done

When you want to stop the containers:

```bash
docker-compose -f docker-compose.override.yml down
```

Your database data persists! Run `docker-compose up -d` again and it comes back.

---

## ?? Now You Can

### Test the Webhook
```bash
curl -X POST http://localhost:5000/api/cms/events \
  -H "Content-Type: application/json" \
  -H "Authorization: Basic Y21zd2hfY2hhbGxlbmdlOmExYjJjM2Q0LWU1ZjYtNzg5MC1hYmNkLWVmMTIzNDU2Nzg5MA==" \
  -d '[{
    "type": "publish",
    "id": "test-entity",
    "version": 1,
    "payload": {"name": "Test"},
    "timestamp": "2024-01-28T10:00:00Z"
  }]'
```

### View API Logs
```bash
docker-compose -f docker-compose.override.yml logs -f api
```

### Connect to Database
Use DBeaver or pgAdmin:
- Host: localhost
- Port: 5432
- User: challenge_user
- Password: Challenge123!@
- Database: ChallengeDB

---

## ? Something Not Working?

Check: **DOCKER_TROUBLESHOOTING.md**

Common issues:
1. **"Container not found"** ? Run `docker-compose up -d`
2. **"Cannot connect to database"** ? Wait 20-30 seconds
3. **"Permission denied"** ? Use PowerShell on Windows
4. **Migrations not applied** ? Check `docker-compose logs api | grep -i migrat`

---

## ?? Summary

```bash
# Build
docker-compose -f docker-compose.override.yml build

# Start
docker-compose -f docker-compose.override.yml up -d

# Wait 20-30 seconds

# Verify
curl http://localhost:5000/health

# Done! ??
```

That's your entire setup process. Everything else happens automatically!

Enjoy your containerized application! ??
