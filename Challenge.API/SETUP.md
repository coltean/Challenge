# Setup and Running Instructions

## Quick Start (2 minutes)

### Prerequisites
- .NET 9 SDK ([Download](https://dotnet.microsoft.com/download/dotnet/9.0))
- Git
- SQL Server LocalDB (Windows) OR PostgreSQL/Docker (Mac/Linux)

### Steps

1. **Clone repository**
```bash
git clone https://github.com/coltean/Challenge.git
cd Challenge/Challenge.API
```

2. **Restore packages**
```bash
dotnet restore
```

3. **Create database**
```bash
dotnet ef database drop --force  # if exists
dotnet ef database update
```

4. **Run application**
```bash
dotnet run
```

5. **Open API**
```
https://localhost:5001/swagger
```

---

## Detailed Setup by Platform

### Windows (SQL Server LocalDB)

**Prerequisites**:
- Visual Studio 2022 with SQL Server LocalDB, or
- SQL Server Express LocalDB standalone

**Setup**:
```bash
# Clone
git clone https://github.com/coltean/Challenge.git
cd Challenge/Challenge.API

# Restore NuGet packages
dotnet restore

# Create database (LocalDB)
dotnet ef database drop --force
dotnet ef database update

# Run
dotnet run
```

**Verify**:
- Browse to `https://localhost:5001/swagger`
- Check `logs/cms-webhook-YYYY-MM-DD.txt` for activity

---

### macOS (PostgreSQL via Docker or Local)

#### Option A: Using Docker (Recommended)

```bash
# Start PostgreSQL container
docker run --name postgres-cms \
  -e POSTGRES_DB=challengedb \
  -e POSTGRES_USER=postgres \
  -e POSTGRES_PASSWORD=postgres \
  -p 5432:5432 \
  -d postgres:16-alpine

# Wait for container to be ready
sleep 5

# Clone and setup
git clone https://github.com/coltean/Challenge.git
cd Challenge/Challenge.API

# Update connection string in appsettings.json
cat > appsettings.json << 'EOF'
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=challengedb;Username=postgres;Password=postgres"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
EOF

# Restore and update database
dotnet restore
dotnet ef database drop --force
dotnet ef database update

# Run
dotnet run
```

#### Option B: Using Homebrew PostgreSQL

```bash
# Install PostgreSQL
brew install postgresql

# Start PostgreSQL
brew services start postgresql

# Create database
createdb challengedb

# Rest same as Option A (update connection string)
```

---

### Linux (PostgreSQL)

```bash
# Install PostgreSQL
sudo apt-get update
sudo apt-get install postgresql postgresql-contrib

# Start service
sudo systemctl start postgresql
sudo systemctl enable postgresql

# Create database and user
sudo -u postgres psql << EOF
CREATE DATABASE challengedb;
CREATE USER postgres WITH PASSWORD 'postgres';
ALTER ROLE postgres SET client_encoding TO 'utf8';
ALTER ROLE postgres SET default_transaction_isolation TO 'read committed';
ALTER ROLE postgres SET default_transaction_deferrable TO on;
ALTER ROLE postgres SET default_transaction_level TO 'read committed';
ALTER ROLE postgres SUPERUSER;
GRANT ALL PRIVILEGES ON DATABASE challengedb TO postgres;
EOF

# Clone and setup (same as macOS Option A)
git clone https://github.com/coltean/Challenge.git
cd Challenge/Challenge.API

# Update connection string in appsettings.json:
# "DefaultConnection": "Host=localhost;Database=challengedb;Username=postgres;Password=postgres"

dotnet restore
dotnet ef database drop --force
dotnet ef database update
dotnet run
```

---

## Configuration

### appsettings.json

**Default (SQL Server LocalDB on Windows)**:
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
  },
  "AllowedHosts": "*"
}
```

**PostgreSQL**:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=challengedb;Username=postgres;Password=postgres"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

**Docker SQL Server**:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost,1433;Database=ChallengeDB;User Id=sa;Password=YourPassword123!;"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

---

## Running the Application

### Development Mode

```bash
dotnet run
```

Outputs:
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: https://localhost:5001
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to stop.
```

### Production Mode

```bash
dotnet run --configuration Release
```

### Custom Port

```bash
dotnet run --urls "https://localhost:5555;http://localhost:5000"
```

---

## Accessing the API

### Swagger UI (Development)

Open browser: `https://localhost:5001/swagger`

You'll see interactive API documentation with Try It Out buttons.

### Direct API Calls

All endpoints require Basic Authentication.

**Credentials**:
- CMS Webhook: `cmswh_challenge` / `a1b2c3d4-e5f6-7890-abcd-ef1234567890`
- API User: `apiuser_demo` / `f0e9d8c7-b6a5-4321-8765-fedcba987654`
- Admin: `admin` / `12345678-1234-1234-1234-123456789012`

#### Test Webhook Endpoint

```bash
# Using curl
curl -X POST https://localhost:5001/api/cms/events \
  -H "Content-Type: application/json" \
  -H "Authorization: Basic $(echo -n 'cmswh_challenge:a1b2c3d4-e5f6-7890-abcd-ef1234567890' | base64)" \
  -d '[
    {
      "type": "publish",
      "id": "test-entity-1",
      "version": 1,
      "payload": {"name": "Test Entity", "description": "Created for testing"},
      "timestamp": "2024-01-28T10:00:00Z"
    }
  ]' \
  --insecure  # Skip SSL verification for localhost
```

#### Test GET Entities

```bash
curl https://localhost:5001/api/entities \
  -H "Authorization: Basic $(echo -n 'apiuser_demo:f0e9d8c7-b6a5-4321-8765-fedcba987654' | base64)" \
  --insecure
```

#### Using Postman

1. Open Postman
2. Create new Request
3. Method: `POST`
4. URL: `https://localhost:5001/api/cms/events`
5. Headers tab:
   - Key: `Authorization`
   - Value: `Basic cmNtc3doX2NoYWxsZW5nZTphMWIyYzNkNC1lNWY2LTc4OTAtYWJjZC1lZjEyMzQ1Njc4OTA=`
   (This is base64 of `cmswh_challenge:a1b2c3d4-e5f6-7890-abcd-ef1234567890`)
6. Body tab: Select `raw` ? `JSON`
7. Paste event batch

---

## Database Migrations

### Create New Migration

```bash
dotnet ef migrations add MigrationName
```

Example:
```bash
dotnet ef migrations add AddNewColumn
```

### Apply Migrations

```bash
# Automatic (on app start)
# Applied in Program.cs before app.Run()

# Manual
dotnet ef database update
```

### View Migration History

```bash
# List migrations
dotnet ef migrations list

# Show SQL
dotnet ef migrations script
```

### Rollback Migration

```bash
# Rollback to previous migration
dotnet ef database update PreviousMigrationName

# Drop entire database
dotnet ef database drop
```

---

## Testing

### Run All Tests

```bash
cd ../Challenge.Tests  # If tests project exists
dotnet test
```

### Run Specific Test Class

```bash
dotnet test --filter "TestClass=EventProcessingServiceTests"
```

### Run with Coverage

```bash
dotnet test /p:CollectCoverage=true
```

---

## Logging

### Log Locations

**Console** (while running):
```
[2024-01-28 10:00:15] [INF] Received batch of 4 events from CMS
[2024-01-28 10:00:15] [INF] Authentication successful for user 'cmswh_challenge'
[2024-01-28 10:00:15] [INF] Successfully processed publish event for entity test-1
```

**File** (`logs/cms-webhook-YYYY-MM-DD.txt`):
```
[2024-01-28 10:00:15.123] [INF] Received batch of 4 events from CMS
[2024-01-28 10:00:15.125] [INF] Authentication successful for user 'cmswh_challenge'
```

### Change Log Level

Edit `appsettings.json`:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",  // Change to Debug/Verbose
      "Microsoft.EntityFrameworkCore": "Debug"
    }
  }
}
```

---

## Troubleshooting

### Issue: "Cannot connect to database"

**Windows + LocalDB**:
```bash
# Check if SQL Server is running
sqllocaldb info mssqllocaldb

# Start if not running
sqllocaldb start mssqllocaldb
```

**PostgreSQL**:
```bash
# Check if running
psql -U postgres -d postgres -c "SELECT 1"

# Start if stopped
brew services start postgresql  # macOS
sudo systemctl start postgresql  # Linux
```

### Issue: "Port 5001 already in use"

```bash
# Use different port
dotnet run --urls "https://localhost:5555"

# Or kill process using port
# Windows
netstat -ano | findstr :5001
taskkill /PID <PID> /F

# macOS/Linux
lsof -i :5001
kill -9 <PID>
```

### Issue: "SSL certificate error"

For development/testing, disable SSL verification:
```bash
# curl
curl --insecure https://localhost:5001/...

# PowerShell
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = {$true}
```

### Issue: "Migration pending"

```bash
# Apply pending migrations
dotnet ef database update

# Or reset and apply fresh
dotnet ef database drop
dotnet ef database update
```

### Issue: "Authorization failed"

**Check**:
1. Base64 encoding of credentials: `username:password` ? base64
2. Header format: `Authorization: Basic <base64_string>`
3. Credentials in Program.cs match your request
4. HTTPS being used (not HTTP)

**Test with curl** (shows decoded auth):
```bash
curl -u cmswh_challenge:a1b2c3d4-e5f6-7890-abcd-ef1234567890 \
  https://localhost:5001/api/entities \
  --insecure
```

### Issue: Swagger not loading

1. Check running on `https://localhost:5001` (not 5000)
2. Verify app is in Development mode: `ASPNETCORE_ENVIRONMENT=Development`
3. Check Serilog isn't silencing startup logs

---

## Docker Support

### Run via Docker Compose

```bash
# Start SQL Server
docker compose up -d sqlserver

# Or PostgreSQL
docker compose up -d postgres

# Then update appsettings.json with docker connection string
# Apply migrations
dotnet ef database update

# Run app
dotnet run
```

### Build Docker Image

```bash
# Create Dockerfile
cat > Dockerfile << 'EOF'
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore
RUN dotnet build -c Release

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /src/bin/Release/net9.0/publish .
EXPOSE 5000 5001
ENTRYPOINT ["dotnet", "Challenge.API.dll"]
EOF

# Build
docker build -t cms-challenge:latest .

# Run
docker run -p 5001:5001 \
  -e ConnectionStrings__DefaultConnection="Server=db;Database=ChallengeDB;User Id=sa;Password=YourPassword123!;" \
  cms-challenge:latest
```

---

## Performance Tips

### Local Development

1. **Disable HTTPS redirect in Development**:
   ```csharp
   if (!app.Environment.IsDevelopment())
   {
       app.UseHttpsRedirection();
   }
   ```

2. **Use HTTP instead of HTTPS**:
   ```bash
   dotnet run --urls "http://localhost:5000"
   ```

3. **Suppress Entity Framework logs if too verbose**:
   ```json
   {
     "Logging": {
       "LogLevel": {
         "Microsoft.EntityFrameworkCore": "Warning"
       }
     }
   }
   ```

### Production Optimization

1. **Use connection pooling**:
   ```json
   "DefaultConnection": "Server=prod-db;...;Max Pool Size=100;"
   ```

2. **Enable query caching**:
   ```csharp
   options.EnableSensitiveDataLogging(false)
   ```

3. **Publish in Release mode**:
   ```bash
   dotnet publish -c Release
   ```

---

## Environment Variables

Override settings via environment variables:

```bash
# Windows
$env:ASPNETCORE_ENVIRONMENT="Development"
$env:ConnectionStrings__DefaultConnection="..."

# Linux/macOS
export ASPNETCORE_ENVIRONMENT=Development
export ConnectionStrings__DefaultConnection="..."
```

---

## CI/CD Integration

### GitHub Actions Example

```yaml
name: Build and Test

on: [push, pull_request]

jobs:
  build:
    runs-on: ubuntu-latest
    
    services:
      postgres:
        image: postgres:16
        env:
          POSTGRES_DB: challengedb
          POSTGRES_PASSWORD: postgres
        options: >-
          --health-cmd pg_isready
          --health-interval 10s
          --health-timeout 5s
          --health-retries 5
        ports:
          - 5432:5432

    steps:
      - uses: actions/checkout@v3
      - uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '9.0.x'
      
      - name: Restore
        run: dotnet restore
      
      - name: Build
        run: dotnet build --configuration Release
      
      - name: Test
        run: dotnet test
        env:
          ConnectionStrings__DefaultConnection: "Host=localhost;Database=challengedb;Username=postgres;Password=postgres"
```

---

## Next Steps

1. ? Clone and setup complete
2. ? Review [README.md](README.md) for feature overview
3. ? Read [EVENT_SEMANTICS.md](EVENT_SEMANTICS.md) for event type details
4. ? Read [SYNC_VS_ASYNC_DECISION.md](SYNC_VS_ASYNC_DECISION.md) for architecture decisions
5. ?? Test with provided credentials and example events
6. ?? Integrate with your CMS
7. ?? Deploy to production

---

## Support

**Documentation**:
- [README.md](README.md) - Feature overview
- [EVENT_SEMANTICS.md](EVENT_SEMANTICS.md) - Event type details
- [SYNC_VS_ASYNC_DECISION.md](SYNC_VS_ASYNC_DECISION.md) - Architecture rationale

**Issues**:
1. Check logs in `logs/` directory
2. Review WebhookEvents table for failed events
3. Open issue on GitHub with logs

**Questions**:
- GitHub Discussions
- Project Wiki (if created)

---

**Last Updated**: 2024-01-28  
**Version**: 1.0.0  
**Status**: Production Ready ?
