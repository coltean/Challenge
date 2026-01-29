# Docker Setup - Step-by-Step Instructions

## Before You Start

You now have a fully containerized application ready to work with Docker on Windows and macOS. But we need to clean up the old SQL Server migrations first.

---

## Step 1: Remove Old SQL Server Migrations

The old migrations were for SQL Server. Since we're switching to PostgreSQL, we need to remove them:

### Windows
```powershell
# Remove old migration folders
Remove-Item -Recurse -Force "Challenge.API\Migrations"
```

### macOS/Linux
```bash
# Remove old migration folders
rm -rf Challenge.API/Migrations
```

---

## Step 2: Create Fresh PostgreSQL Migrations

Now create new migrations for PostgreSQL:

```bash
cd Challenge.API

# Create migration for ApplicationDbContext
dotnet ef migrations add InitialCreate --context ApplicationDbContext --output-dir Migrations/ApplicationDb

# Create migration for ReadOnlyDbContext
dotnet ef migrations add InitialCreate --context ReadOnlyDbContext --output-dir Migrations/ReadOnlyDb
```

This will create new migration files compatible with PostgreSQL.

---

## Step 3: Build Docker Images

```bash
# Build the containers (this takes 2-3 minutes first time)
docker-compose -f docker-compose.override.yml build

# You should see:
# - PostgreSQL image being pulled
# - Your .NET application being built
# - Final Docker image created
```

---

## Step 4: Start Containers

```bash
# Start PostgreSQL and API containers
docker-compose -f docker-compose.override.yml up

# You should see:
# - PostgreSQL starting and becoming healthy
# - API starting
# - Health checks passing
# - API ready to accept requests
```

Leave this terminal open to see logs.

---

## Step 5: Apply Database Migrations

In another terminal:

```bash
# Apply migrations to ApplicationDbContext
docker-compose -f docker-compose.override.yml exec api dotnet ef database update --context ApplicationDbContext

# Apply migrations to ReadOnlyDbContext
docker-compose -f docker-compose.override.yml exec api dotnet ef database update --context ReadOnlyDbContext

# You should see:
# - Migrations being applied
# - Database tables created
# - Schema ready for use
```

---

## Step 6: Verify Everything Works

Test your endpoints:

### Health Check
```bash
curl http://localhost:5000/health
```

### Swagger UI
```
http://localhost:5000/swagger
```

### Send Test Event
```bash
curl -X POST http://localhost:5000/api/cms/events \
  -H "Content-Type: application/json" \
  -H "Authorization: Basic Y21zd2hfY2hhbGxlbmdlOmExYjJjM2Q0LWU1ZjYtNzg5MC1hYmNkLWVmMTIzNDU2Nzg5MA==" \
  -d '[{
    "type": "publish",
    "id": "test-entity",
    "version": 1,
    "payload": {"name": "Test Entity"},
    "timestamp": "2024-01-28T10:00:00Z"
  }]'
```

---

## Step 7: View Logs

To see what's happening:

```bash
# All logs
docker-compose -f docker-compose.override.yml logs -f

# Just API logs
docker-compose -f docker-compose.override.yml logs -f api

# Just database logs
docker-compose -f docker-compose.override.yml logs -f database
```

---

## Complete Checklist

- [ ] Remove old migrations (Step 1)
- [ ] Create new PostgreSQL migrations (Step 2)
- [ ] Build Docker images (Step 3)
- [ ] Start containers (Step 4)
- [ ] Apply database migrations (Step 5)
- [ ] Test health endpoint (Step 6)
- [ ] Access Swagger UI (Step 6)
- [ ] Send test event (Step 6)
- [ ] Verify logs (Step 7)

---

## Troubleshooting

### Error: "SqlServerModelBuilderExtensions not found"
This means old SQL Server migrations still exist.
```bash
# Clean up
rm -rf Challenge.API/Migrations
dotnet clean
dotnet restore
dotnet build
```

### Error: "Database connection refused"
PostgreSQL might not be ready yet. Wait a few seconds and try again.
```bash
# Check database health
docker-compose -f docker-compose.override.yml ps
# database should show "healthy"
```

### Error: "Port 5000 already in use"
Change the port in `docker-compose.override.yml`:
```yaml
ports:
  - "5001:5000"  # Use 5001 instead of 5000
```

### Want to start completely fresh
```bash
# Stop and remove everything
docker-compose -f docker-compose.override.yml down -v

# Clean Docker
docker system prune -a

# Remove migrations
rm -rf Challenge.API/Migrations

# Start from Step 2
```

---

## Quick Reference Commands

```bash
# Build
docker-compose -f docker-compose.override.yml build

# Start
docker-compose -f docker-compose.override.yml up

# Stop
docker-compose -f docker-compose.override.yml down

# Logs
docker-compose -f docker-compose.override.yml logs -f

# Execute command
docker-compose -f docker-compose.override.yml exec api [COMMAND]

# Fresh start
docker-compose -f docker-compose.override.yml down -v && docker-compose -f docker-compose.override.yml up --build
```

---

## What You Now Have

? **Containerized API** - Runs in Docker  
? **Containerized Database** - PostgreSQL in Docker  
? **Cross-Platform** - Works identically on Windows/macOS  
? **Health Checks** - Docker knows when service is ready  
? **Persistent Storage** - Database data survives container restart  
? **Logging** - All output visible in terminal or docker logs  
? **Easy Deployment** - One command to start everything  

---

## Next Steps

1. Once everything is working locally with Docker:
   ```bash
   git add .
   git commit -m "Add Docker containerization with PostgreSQL"
   git push
   ```

2. Update your README with Docker setup instructions

3. Your teammates can now clone and do:
   ```bash
   docker-compose -f docker-compose.override.yml up
   ```
   And have a fully working environment!

---

## Files Changed Summary

| File | Change | Reason |
|------|--------|--------|
| `Challenge.API/Challenge.API.csproj` | SQL Server ? PostgreSQL provider | Cross-platform support |
| `Challenge.API/Program.cs` | UseSqlServer ? UseNpgsql | PostgreSQL configuration |
| `Challenge.API/appsettings.json` | Connection string updated | PostgreSQL connection |
| `Challenge.API/Data/ApplicationDbContext.cs` | nvarchar ? text | PostgreSQL column types |
| `Challenge.API/Data/ReadOnlyDbContext.cs` | nvarchar ? text | PostgreSQL column types |
| `Challenge.API/Migrations/` | REMOVED | SQL Server specific |
| `Challenge.API/Dockerfile` | NEW | Container image definition |
| `docker-compose.override.yml` | NEW | Service orchestration |
| `.dockerignore` | NEW | Build optimization |
| `Challenge.API/Controllers/HealthController.cs` | NEW | Health checks for Docker |

---

## You're Ready!

Follow these 7 steps and you'll have a fully working containerized application. Let me know if you run into any issues!
