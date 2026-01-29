# ? Docker Setup - FIXED & SIMPLIFIED

## Your Issue: `docker-compose exec` Command Not Working

**Good news**: You don't need that command!

---

## The Fix - Do This (3 Commands)

Run these from: `C:\Work\Challenge`

### 1. Build Docker Images
```bash
docker-compose -f docker-compose.override.yml build
```

**What to expect**: 
- Takes 2-3 minutes
- Downloads PostgreSQL image
- Compiles your .NET 9 app
- Creates Docker image

### 2. Start Containers
```bash
docker-compose -f docker-compose.override.yml up -d
```

**What to expect**:
- Instantly returns to prompt
- PostgreSQL starts
- Your API starts
- **Migrations apply automatically** (you don't do anything)

### 3. Wait & Verify
```bash
# Wait 20-30 seconds for database to be ready

# Then check health
curl http://localhost:5000/health
```

**What to expect**:
- Returns: `{"status":"healthy",...}`
- This means everything is working ?

---

## That's It! You're Done! ??

Your application is now running with:
- ? PostgreSQL database
- ? All migrations applied automatically
- ? API listening on http://localhost:5000
- ? Full documentation available at http://localhost:5000/swagger

---

## Why Migrations Are Automatic

Your `Program.cs` includes this code that runs on startup:

```csharp
// DATABASE INITIALIZATION
await dbContext.Database.MigrateAsync();
```

This means:
- **No manual migration steps needed**
- **Applied every time container starts**
- **Automatically creates all database tables**
- **Works in dev and production**

---

## Verify Everything Works

Run these commands to confirm:

```bash
# 1. Check both containers are running
docker-compose -f docker-compose.override.yml ps

# Should show:
#   NAME             STATUS
#   challenge-api    Up
#   challenge-db     Up

# 2. Check API is healthy
curl http://localhost:5000/health

# Should return JSON with "status":"healthy"

# 3. Confirm migrations applied
docker-compose -f docker-compose.override.yml logs api | findstr /i "migration"

# Should show:
#   Applying database migrations...
#   Database migrations completed successfully
```

All three working? ? You're done!

---

## Next: Test Your API

### Access Swagger UI
```
http://localhost:5000/swagger
```

### Send a Test Event
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

---

## Daily Usage

### Start Your App
```bash
docker-compose -f docker-compose.override.yml up -d
```

### View Logs
```bash
docker-compose -f docker-compose.override.yml logs -f api
```

### Stop Your App
```bash
docker-compose -f docker-compose.override.yml down
```

### Fresh Start (delete database)
```bash
docker-compose -f docker-compose.override.yml down -v
docker-compose -f docker-compose.override.yml up -d
```

---

## Common Questions

**Q: Do I need to manually apply migrations?**
A: No! They're applied automatically on startup.

**Q: What if the database isn't ready yet?**
A: The API waits for it automatically. Program.cs handles this.

**Q: Where's my data stored?**
A: In Docker volume `postgres-data`. Persists even when containers stop.

**Q: How do I access the database?**
A: Use DBeaver or pgAdmin:
- Host: localhost
- Port: 5432
- User: challenge_user
- Password: Challenge123!@
- Database: ChallengeDB

**Q: What if something goes wrong?**
A: Check `DOCKER_TROUBLESHOOTING.md`

---

## What NOT to Do

? Don't run this:
```bash
docker-compose -f docker-compose.override.yml exec api dotnet ef database update
```

? Do this instead:
```bash
docker-compose -f docker-compose.override.yml up -d
# Wait 20 seconds
# Done!
```

---

## File Structure

Everything is in place:
```
C:\Work\Challenge/
??? docker-compose.override.yml     ? (runs your containers)
??? Challenge.API/
?   ??? Dockerfile                  ? (builds API image)
?   ??? Program.cs                  ? (auto-applies migrations)
?   ??? Migrations/
?   ?   ??? ApplicationDb/          ? (PostgreSQL migrations)
?   ?   ??? ReadOnlyDb/             ? (PostgreSQL migrations)
?   ??? appsettings.json            ? (PostgreSQL connection)
```

All ready to go! ??

---

## Summary

```
Step 1: docker-compose -f docker-compose.override.yml build
Step 2: docker-compose -f docker-compose.override.yml up -d
Step 3: curl http://localhost:5000/health
Step 4: Done! ??
```

Your API is running with:
- ? Full database
- ? All tables created
- ? All migrations applied
- ? Ready to accept requests

Enjoy! ??
