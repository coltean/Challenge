# Docker Setup - Troubleshooting & Fix Guide

## Issue: `docker-compose exec` command not working

When you run:
```bash
docker-compose -f docker-compose.override.yml exec api dotnet ef database update --context ApplicationDbContext
```

You might get errors like:
- "Container not found"
- "Command failed"
- "Cannot connect to database"

---

## ? Solution: Step-by-Step Fix

### Step 1: Verify Containers Are Running

First, check if your containers are actually running:

```bash
docker-compose -f docker-compose.override.yml ps
```

You should see output like:
```
NAME              STATUS              PORTS
challenge-api     Up 2 minutes        0.0.0.0:5000->5000/tcp
challenge-db      Up 2 minutes        0.0.0.0:5432->5432/tcp
```

**If containers are NOT running:**
```bash
# Start them
docker-compose -f docker-compose.override.yml up -d

# Wait 15-20 seconds for PostgreSQL to be fully ready
# Then check status again
docker-compose -f docker-compose.override.yml ps
```

### Step 2: Check Database Health

Verify the database is healthy:

```bash
docker-compose -f docker-compose.override.yml logs database
```

You should see:
```
database | database system is ready to accept connections
```

If you see errors, wait another 10-20 seconds and check again.

### Step 3: Verify API Container Can See Database

Test the network connection:

```bash
docker-compose -f docker-compose.override.yml exec api ping database
```

You should see successful pings. If this fails, there's a network issue.

### Step 4: Apply Migrations (Corrected Command)

Now run the migration command. Here are the correct versions:

#### Option A: Using docker-compose exec (recommended)
```bash
docker-compose -f docker-compose.override.yml exec api bash
```

Then inside the container:
```bash
cd Challenge.API
dotnet ef database update --context ApplicationDbContext
dotnet ef database update --context ReadOnlyDbContext
exit
```

#### Option B: Direct exec (might not work on Windows)
On macOS/Linux:
```bash
docker-compose -f docker-compose.override.yml exec -T api dotnet ef database update --context ApplicationDbContext
docker-compose -f docker-compose.override.yml exec -T api dotnet ef database update --context ReadOnlyDbContext
```

The `-T` flag disables pseudo-TTY allocation (helps on some systems).

#### Option C: Migrations Run Automatically
Actually, **migrations run automatically on startup** in your Program.cs!

You don't need to manually run them. When the container starts:
1. Program.cs initializes the database
2. Migrations are applied automatically
3. Database is ready to use

To verify this worked:
```bash
# Check the API logs
docker-compose -f docker-compose.override.yml logs api | grep -i migrat

# You should see:
# "Applying database migrations..."
# "Database migrations completed successfully"
```

---

## ?? Complete Setup Flow (The Right Way)

### Step 1: Build Images
```bash
docker-compose -f docker-compose.override.yml build
```

### Step 2: Start Containers in Background
```bash
docker-compose -f docker-compose.override.yml up -d
```

### Step 3: Wait for Database to Be Ready
```bash
# Watch the logs until you see "ready to accept connections"
docker-compose -f docker-compose.override.yml logs -f database

# Press Ctrl+C when done
```

### Step 4: Check API Started Successfully
```bash
docker-compose -f docker-compose.override.yml logs api | grep -i migrat
```

You should see:
```
Applying database migrations...
Database migrations completed successfully
```

### Step 5: Verify API Is Healthy
```bash
curl http://localhost:5000/health
```

You should get:
```json
{"status":"healthy","timestamp":"2024-01-28T10:00:00Z"}
```

**If you get any errors**, check Step 6.

### Step 6: If Migrations Didn't Run
If you don't see migration logs in step 4:

```bash
# Get detailed logs
docker-compose -f docker-compose.override.yml logs api

# Look for any error messages
# Common issues:
# - "Cannot connect to database" - DB not ready yet, wait longer
# - "Connection refused" - Check docker-compose.yml connection string
# - "Migration failed" - Check if migrations exist in Challenge.API/Migrations/
```

---

## ?? Common Errors & Fixes

### Error: "Container challenge-api does not exist"
**Cause**: Containers not started

**Fix**:
```bash
docker-compose -f docker-compose.override.yml up -d
```

### Error: "Connection refused" or "Cannot connect to database"
**Cause**: Database not ready

**Fix**:
```bash
# Wait for database
docker-compose -f docker-compose.override.yml logs database
# Wait until you see "ready to accept connections"

# Then check health
docker-compose -f docker-compose.override.yml ps
# database should show "healthy"
```

### Error: "No such file or directory"
**Cause**: Using wrong path

**Fix**: Run from root of your project (where docker-compose.override.yml is):
```bash
# Correct location
C:\Work\Challenge> docker-compose -f docker-compose.override.yml ps

# Wrong location
C:\Work\Challenge\Challenge.API> docker-compose -f docker-compose.override.yml ps
```

### Error: "Cannot find Challenge.API.dll"
**Cause**: Docker image wasn't built with your code

**Fix**: Rebuild the image
```bash
docker-compose -f docker-compose.override.yml build --no-cache api
docker-compose -f docker-compose.override.yml up -d
```

### Error on Windows: "permission denied"
**Cause**: Shell issue

**Fix**: Try using PowerShell instead of CMD:
```powershell
# PowerShell
docker-compose -f docker-compose.override.yml exec api bash
```

---

## ? Quick Verification Commands

Run these to verify everything is working:

```bash
# 1. Containers running?
docker-compose -f docker-compose.override.yml ps

# 2. Database healthy?
docker-compose -f docker-compose.override.yml ps | grep database
# Should show "healthy"

# 3. API responding?
curl http://localhost:5000/health

# 4. Migrations applied?
docker-compose -f docker-compose.override.yml logs api | grep -i migrat

# 5. Can connect to database?
docker-compose -f docker-compose.override.yml exec api bash
# Inside container:
psql -h database -U challenge_user -d ChallengeDB -c "SELECT 1"
exit
```

All of these should succeed without errors.

---

## ?? Checklist for Success

After setup, you should be able to:

- [ ] `docker-compose -f docker-compose.override.yml ps` shows both containers
- [ ] Both containers show "Up" status
- [ ] Database shows "healthy"
- [ ] `curl http://localhost:5000/health` returns 200
- [ ] API logs show "Database migrations completed successfully"
- [ ] Can POST events to `http://localhost:5000/api/cms/events`
- [ ] Can access Swagger at `http://localhost:5000/swagger`

If all checks pass, you're done! ??

---

## ?? Summary

**You DO NOT need to manually run migrations.**

The Program.cs automatically:
1. Waits for database to be ready
2. Applies migrations
3. Creates tables
4. Starts the API

Just:
1. Build: `docker-compose build`
2. Start: `docker-compose up -d`
3. Wait: 15-20 seconds
4. Verify: `curl http://localhost:5000/health`

Done! Your application is running with a fully migrated database. ??
