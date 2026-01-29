# Quick Test Execution Guide

## Run All Tests (Recommended)

```bash
cd Challenge.Tests
dotnet test
```

Expected output:
```
Test Run Summary:
  Total tests: 83+
  Passed: 83+
  Failed: 0
  Skipped: 0
  Duration: ~5-10 seconds
```

---

## Run Tests by Category

### Event Processing Tests Only

```bash
dotnet test --filter "FullyQualifiedName~EventProcessingServiceTests"
```

**What it tests:**
- Publish event creates/updates entities
- Unpublish event disables versions
- Delete event hard-deletes entities
- Idempotency (duplicate detection)
- Transaction rollback
- Corner cases

**Expected: 17 tests pass**

---

### Authentication Tests Only

```bash
dotnet test --filter "FullyQualifiedName~BasicAuthenticationTests"
```

**What it tests:**
- Valid credentials accepted (CMS, API User, Admin)
- Invalid credentials rejected (wrong password, missing header)
- Role-based access control (users can't access admin endpoints)
- Case sensitivity
- Edge cases (empty credentials, malformed Base64)

**Expected: 21 tests pass**

---

### Validation Tests Only

```bash
dotnet test --filter "FullyQualifiedName~EventValidationTests"
```

**What it tests:**
- Event type validation (publish, unPublish, delete)
- Entity ID validation (length, format)
- Version validation (required for publish/unpublish)
- Payload validation (required/not required per type)
- Timestamp validation (not future-dated)
- Batch size validation (1-1000 events)

**Expected: 35+ tests pass**

---

### Integration Tests Only

```bash
dotnet test --filter "FullyQualifiedName~ApiIntegrationTests"
```

**What it tests:**
- End-to-end webhook ? database ? API flow
- User access control
- Admin operations (disable/enable)
- Error handling

**Expected: 10 tests pass**

---

## Run Specific Test

```bash
# Event processing corner case
dotnet test --filter "Name=ProcessUnpublishEvent_CornerCase_UnpublishOnlyVersionMakesEntityUnpublished"

# Authentication with invalid password
dotnet test --filter "Name=WebhookEndpoint_WithInvalidPassword_ReturnsUnauthorized"

# Validation with invalid entity ID
dotnet test --filter "Name=ValidateEvent_WithIdExceedingMaxLength_Fails"

# Integration test
dotnet test --filter "Name=SendPublishEvent_CreatesEntity_AndIsRetrievableByUser"
```

---

## Run with Detailed Output

```bash
dotnet test --verbosity detailed
```

Shows each test as it runs:
```
Starting test execution, please wait...
[xUnit.net 00:00:00.00] xunit.runner.visualstudio.TestAdapter v2.5.4 (.NET 9.0.0.0)
[xUnit.net 00:00:01.23]   Challenge.Tests.Services.EventProcessingServiceTests.ProcessPublishEvent_CreatesNewEntity_WhenEntityDoesNotExist (PASSED)
[xUnit.net 00:00:01.45]   Challenge.Tests.Services.EventProcessingServiceTests.ProcessPublishEvent_UpdatesExistingEntity_WithNewVersion (PASSED)
...
```

---

## Run with Code Coverage

```bash
dotnet test /p:CollectCoverage=true /p:CoverageFormat=cobertura
```

Generates coverage report in `coverage.cobertura.xml`

View coverage:
```bash
# macOS/Linux
open coverage.html

# Windows
start coverage.html
```

---

## Run Tests from Solution Root

```bash
# From Challenge directory
dotnet test Challenge.Tests/Challenge.Tests.csproj

# Or using solution
dotnet test
```

---

## Common Test Patterns

### Verify Event Processing Works

```bash
# All 17 event processing tests
dotnet test --filter "FullyQualifiedName~EventProcessingServiceTests"

# Just publish events
dotnet test --filter "ClassName=EventProcessingServiceTests&Name~Publish"

# Just corner cases
dotnet test --filter "ClassName=EventProcessingServiceTests&Name~CornerCase"
```

### Verify Authentication Works

```bash
# All 21 authentication tests
dotnet test --filter "FullyQualifiedName~BasicAuthenticationTests"

# Just valid credentials
dotnet test --filter "ClassName=BasicAuthenticationTests&Name~Valid"

# Just role-based access control
dotnet test --filter "ClassName=BasicAuthenticationTests&Name~Role"
```

### Verify Input Validation Works

```bash
# All 35+ validation tests
dotnet test --filter "FullyQualifiedName~EventValidationTests"

# Just entity ID validation
dotnet test --filter "ClassName=EventValidationTests&Name~Id"

# Just batch validation
dotnet test --filter "ClassName=EventValidationTests&Name~Batch"
```

---

## Troubleshooting

### Tests Won't Run

```bash
# Restore and rebuild
dotnet restore
dotnet build

# Then run tests
dotnet test
```

### Database Errors

```bash
# Reset database
cd ../Challenge.API
dotnet ef database drop --force
dotnet ef database update

# Go back to tests
cd ../Challenge.Tests
dotnet test
```

### Port Already in Use

Integration tests usually pick random ports, but if you hit issues:

```bash
# Force different port
dotnet test --environment Testing --settings .runsettings
```

### Slow Tests

Tests should complete in 5-10 seconds. If slower:

```bash
# Run without code coverage
dotnet test --no-build

# Or just run specific tests
dotnet test --filter "ClassName=EventProcessingServiceTests"
```

---

## CI/CD Integration

### GitHub Actions

```yaml
name: Tests
on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '9.0.x'
      - run: dotnet test Challenge.Tests/Challenge.Tests.csproj
```

### Azure Pipelines

```yaml
trigger:
  - master

pool:
  vmImage: 'ubuntu-latest'

steps:
  - task: UseDotNet@2
    inputs:
      version: '9.0.x'
  
  - script: dotnet test Challenge.Tests/Challenge.Tests.csproj
    displayName: Run Tests
```

---

## Test Success Criteria

? **All tests pass**: 83+ tests should pass  
? **No skipped tests**: All tests should execute  
? **No warnings**: Clean build output  
? **Reasonable duration**: Should complete in <10 seconds  
? **Code coverage**: Aim for >90% on critical paths  

---

## What Each Test Proves

### Event Processing Tests (17)
- ? **Publish works correctly** for new and existing entities
- ? **Unpublish handles** version disabling and rollback
- ? **Delete** completely removes entities
- ? **Corner cases** (unpublish without prior version) handled
- ? **Idempotency** (duplicates skipped)
- ? **Transactions** (atomic all-or-nothing)

### Authentication Tests (21)
- ? **Valid credentials** accepted for all roles
- ? **Invalid credentials** rejected with 401
- ? **Role-based access** enforced (users can't access admin endpoints)
- ? **Case sensitivity** implemented
- ? **Edge cases** (empty, malformed, etc.) handled

### Validation Tests (35+)
- ? **Event types** restricted to publish, unPublish, delete
- ? **Entity IDs** limited to 255 chars, alphanumeric + `-_.`
- ? **Versions** required for publish/unpublish, >0
- ? **Payloads** required for publish/unpublish, forbidden for delete
- ? **Timestamps** not future-dated, ISO 8601 format
- ? **Batches** sized 1-1000 events

### Integration Tests (10)
- ? **End-to-end workflow** from webhook to REST API
- ? **User access control** (only published visible)
- ? **Admin operations** (disable/enable)
- ? **Error handling** (bad requests, not found)

---

## Next Steps

1. **Run all tests**: `dotnet test`
2. **Verify they pass**: Should see "passed" count = total count
3. **Run specific category**: `dotnet test --filter "ClassName=YourTestClass"`
4. **Check coverage**: `dotnet test /p:CollectCoverage=true`
5. **Integrate into CI/CD**: Add test step to your pipeline

---

**The test suite comprehensively validates all ingestion constraints, authentication, and event processing logic. Run the tests to verify the system works correctly!** ?
