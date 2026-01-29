# Event Type Semantics and Implementation

## Clarification: Naming Confusion

The original requirements use different terminology than the example schema. This document clarifies the mapping and ensures the implementation is correct.

---

## Original Requirements vs Actual Event Types

### Original Specification
```
- Add ? new entities are being created
- Update ? an existing entity was successfully changed
- Delete ? an entity was removed or unpublished (soft-delete)
```

### Actual Event Schema (from example)
```json
[
  { "type": "publish", "id": "X", "payload": {...}, "version": 2 },
  { "type": "delete", "id": "Y" },
  { "type": "unPublish", "id": "Z", "payload": {...}, "version": 4 }
]
```

### Semantic Mapping

| Original Term | Actual Event Type | Behavior | Database Impact |
|---------------|-------------------|----------|-----------------|
| **Add** | `publish` (v1) | Create new entity with initial version | Insert Entity + EntityVersion |
| **Update** | `publish` (v2+) | Update entity with new version | Update Entity + Insert EntityVersion |
| **Delete** | `unPublish` | Unpublish/disable entity (soft-delete) | Update EntityVersion.IsPublished = false |
| **Hard Delete** | `delete` | Completely remove entity (cascade) | DELETE Entity (cascade to versions) |

---

## Three Event Types in Implementation

### 1. PUBLISH Event

**Purpose**: Create or update entity content  
**Applies To**: Both new entities (Add) and updates (Update)  
**Required Fields**: `id`, `version`, `payload`, `timestamp`

**Example - Add (First Version)**:
```json
{
  "type": "publish",
  "id": "product-101",
  "version": 1,
  "payload": {
    "title": "Product A",
    "price": 99.99,
    "description": "Initial version"
  },
  "timestamp": "2024-01-28T10:00:00Z"
}
```

**Example - Update (New Version)**:
```json
{
  "type": "publish",
  "id": "product-101",
  "version": 2,
  "payload": {
    "title": "Product A (Updated)",
    "price": 89.99,
    "description": "Price reduced"
  },
  "timestamp": "2024-01-28T11:00:00Z"
}
```

**Implementation Logic**:
```csharp
// Publish event processing
private async Task HandlePublishEventAsync(CmsEventDto @event)
{
    // Case 1: Entity doesn't exist ? Create (Add scenario)
    if (entity == null)
    {
        entity = new Entity { Id = @event.Id };
        context.Entities.Add(entity);
    }
    
    // Case 2: Entity exists ? Update (Update scenario)
    // Add new version regardless of case
    var version = new EntityVersion
    {
        EntityId = @event.Id,
        VersionNumber = @event.Version.Value,
        Payload = SerializePayload(@event.Payload),
        IsPublished = true,
        PublishedAt = @event.Timestamp
    };
    entity.Versions.Add(version);
    entity.CurrentPublishedVersion = @event.Version.Value;
    entity.IsPublished = true;
}
```

### 2. UNPUBLISH Event

**Purpose**: Unpublish/disable a specific version (soft-delete)  
**Applied To**: Disabling content without removing it  
**Required Fields**: `id`, `version`, `payload`, `timestamp`  
**Note**: Payload is included so you can see what was unpublished

**Example**:
```json
{
  "type": "unPublish",
  "id": "product-101",
  "version": 2,
  "payload": {
    "title": "Product A (Updated)",
    "price": 89.99
  },
  "timestamp": "2024-01-28T11:30:00Z"
}
```

**Implementation Logic**:
```csharp
// Unpublish event processing
private async Task HandleUnpublishEventAsync(CmsEventDto @event)
{
    var entity = await context.Entities
        .Include(e => e.Versions)
        .FirstOrDefaultAsync(e => e.Id == @event.Id);

    // Mark the specific version as unpublished
    var versionToUnpublish = entity.Versions
        .FirstOrDefault(v => v.VersionNumber == @event.Version);
    
    if (versionToUnpublish != null)
    {
        versionToUnpublish.IsPublished = false;
        versionToUnpublish.UnpublishedAt = @event.Timestamp;
    }

    // CORNER CASE: If unpublishing current published version
    if (entity.CurrentPublishedVersion == @event.Version)
    {
        // Try to find previous published version
        var previousVersion = entity.Versions
            .Where(v => v.VersionNumber < @event.Version && v.IsPublished)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefault();

        if (previousVersion != null)
        {
            // Rollback to previous version
            entity.CurrentPublishedVersion = previousVersion.VersionNumber;
        }
        else
        {
            // No previous version: entity becomes unpublished
            entity.IsPublished = false;
            entity.CurrentPublishedVersion = 0;
        }
    }
}
```

**Data Preservation**: 
- ? Version data remains in database
- ? Can view version history
- ? Can republish later if needed

### 3. DELETE Event

**Purpose**: Hard-delete entity (remove completely)  
**Applied To**: Permanently removing content  
**Required Fields**: `id`, `timestamp`  
**Note**: No `version` or `payload` (removing everything)

**Example**:
```json
{
  "type": "delete",
  "id": "product-101",
  "timestamp": "2024-01-28T12:00:00Z"
}
```

**Implementation Logic**:
```csharp
// Delete event processing
private async Task HandleDeleteEventAsync(CmsEventDto @event)
{
    var entity = await context.Entities.FirstOrDefaultAsync(e => e.Id == @event.Id);
    
    if (entity != null)
    {
        // Hard delete: Remove entity and cascade delete all versions
        context.Entities.Remove(entity);
    }
}
```

**Data Removal**:
- ? Entity deleted completely
- ? All versions deleted (cascade)
- ? Cannot recover

---

## Corner Case: Unpublish Without Prior Published Version

### Scenario

```
Timeline:
?? 10:00 - Publish v1 (entity created and published)
?
?? 10:30 - Publish v2 (entity updated to v2)
?
?? 10:45 - Unpublish v2 (v2 is disabled)
?  ?? What's the current published version now?
?     ?? Rollback to v1 (previous published version exists) ?
?
?? 11:00 - Publish v3, then Unpublish v3
   ?? What's the current published version?
      ?? Rollback to v2 ?
```

### Edge Case: Never-Published Scenario

```
Timeline:
?? 10:00 - Publish v1 (entity created, v1 published)
?
?? 10:30 - Publish v2 (entity updated, v2 published)
?
?? 10:45 - Unpublish v2 (v2 disabled)
?          ?? Rollback to v1 ? (previous published exists)
?
?? 10:50 - Unpublish v1 (v1 disabled)
?          ?? No previous published version exists!
?          ?? Entity becomes unpublished (CurrentPublishedVersion = 0)
?          ?? IsPublished = false
?          ?? Data preserved in database ?
?
?? Users cannot see this entity anymore
  ?? But it can be republished if needed
```

### Even Stranger Edge Case: Unpublish Before Any Publish

```
Timeline:
?? 10:00 - Unpublish v2 (entity never existed!)
?
?? Expected behavior (implemented):
?  ?? Create entity with v2
?  ?? Mark v2 as unpublished
?  ?? Entity created but not published
?  ?? IsPublished = false
?
?? Later: Publish v1 (new version published)
?  ?? Entity is now published with v1
?  ?? v2 remains unpublished (in history)
```

**This is handled correctly** in the implementation:
```csharp
if (entity == null)
{
    // Create entity with unpublished version
    entity = new Entity
    {
        Id = @event.Id,
        IsPublished = false,  // Not published yet
        CurrentPublishedVersion = 0
    };
    
    var version = new EntityVersion
    {
        VersionNumber = @event.Version.Value,
        IsPublished = false,
        UnpublishedAt = @event.Timestamp
    };
    
    entity.Versions.Add(version);
    context.Entities.Add(entity);
}
```

---

## Event Validation Rules

| Event Type | Required | Validation |
|-----------|----------|-----------|
| `publish` | `id` | Max 255 chars, alphanumeric + `-_.` |
| | `version` | Must be > 0 |
| | `payload` | Must not be null |
| | `timestamp` | Must not be future-dated |
| `unPublish` | `id` | Max 255 chars, alphanumeric + `-_.` |
| | `version` | Must be > 0 |
| | `payload` | Must not be null |
| | `timestamp` | Must not be future-dated |
| `delete` | `id` | Max 255 chars, alphanumeric + `-_.` |
| | `version` | Must be null (not allowed) |
| | `payload` | Must be null (not allowed) |
| | `timestamp` | Must not be future-dated |

---

## State Transitions

### Entity State Machine

```
???????????????????????
?  Non-Existent       ?
???????????????????????
           ?
           ? Publish v1 / Unpublish v1
           ?
???????????????????????
? Published (v=1)     ?  ? Can be seen by users
? IsPublished = true  ?
???????????????????????
           ?
           ? Publish v2
           ?
???????????????????????
? Published (v=2)     ?  ? Can be seen by users (v2)
? IsPublished = true  ?
???????????????????????
    ???????????????
    ?             ?
  Unpub v2     Publish v3
    ?             ?
    ?             ?
Published (v=1)  Published (v=3)
Rolled back      New version

???????????????????????
? Unpublished         ?  ? Cannot be seen by users
? IsPublished = false ?
???????????????????????
           ?
           ? Publish vN
           ?
    Re-published (vN)
```

---

## Database State Examples

### Example 1: Simple Publish ? Unpublish ? Republish

**Events**:
```json
[
  {"type": "publish", "id": "prod-1", "version": 1, "payload": {"title": "v1"}, "timestamp": "2024-01-01T10:00:00Z"},
  {"type": "publish", "id": "prod-1", "version": 2, "payload": {"title": "v2"}, "timestamp": "2024-01-01T11:00:00Z"},
  {"type": "unPublish", "id": "prod-1", "version": 2, "payload": {"title": "v2"}, "timestamp": "2024-01-01T12:00:00Z"},
  {"type": "publish", "id": "prod-1", "version": 3, "payload": {"title": "v3"}, "timestamp": "2024-01-01T13:00:00Z"}
]
```

**Entity Table**:
| Id | CurrentPublishedVersion | IsPublished |
|----|-------------------------|------------|
| prod-1 | 3 | true |

**EntityVersion Table**:
| Id | EntityId | VersionNumber | IsPublished | UnpublishedAt |
|----|----------|---------------|------------|---------------|
| 1 | prod-1 | 1 | true | null |
| 2 | prod-1 | 2 | false | 2024-01-01T12:00:00Z |
| 3 | prod-1 | 3 | true | null |

**User sees**: v3 (latest published)

---

### Example 2: Corner Case - Unpublish All Versions

**Events**:
```json
[
  {"type": "publish", "id": "prod-2", "version": 1, "payload": {"title": "v1"}, "timestamp": "2024-01-01T10:00:00Z"},
  {"type": "publish", "id": "prod-2", "version": 2, "payload": {"title": "v2"}, "timestamp": "2024-01-01T11:00:00Z"},
  {"type": "unPublish", "id": "prod-2", "version": 2, "payload": {"title": "v2"}, "timestamp": "2024-01-01T12:00:00Z"},
  {"type": "unPublish", "id": "prod-2", "version": 1, "payload": {"title": "v1"}, "timestamp": "2024-01-01T13:00:00Z"}
]
```

**Entity Table**:
| Id | CurrentPublishedVersion | IsPublished |
|----|-------------------------|------------|
| prod-2 | 0 | false |

**EntityVersion Table**:
| Id | EntityId | VersionNumber | IsPublished | UnpublishedAt |
|----|----------|---------------|------------|---------------|
| 1 | prod-2 | 1 | false | 2024-01-01T13:00:00Z |
| 2 | prod-2 | 2 | false | 2024-01-01T12:00:00Z |

**User sees**: Nothing (entity unpublished, but data preserved!)

---

## REST API Visibility

### GET /api/entities (Regular User)

Returns only published entities:
```csharp
var entities = await context.Entities
    .Where(e => e.IsPublished && !e.IsDisabledByAdmin)
    .ToListAsync();
```

### GET /api/entities (Admin User)

Returns all entities including unpublished:
```csharp
var entities = await context.Entities.ToListAsync();
```

**Returns**:
- ? Published entities (IsPublished = true)
- ? Unpublished entities (IsPublished = false) - Admin only
- ? Admin-disabled entities - Admin only
- ? Deleted entities - None (hard-deleted, not in DB)

---

## Summary

| Operation | Event Type | Effect | Data Preserved |
|-----------|-----------|--------|-----------------|
| Create entity (Add) | `publish` v1 | Entity created + v1 published | ? Yes |
| Update entity (Update) | `publish` v2+ | v1 remains, v2 published | ? Yes |
| Disable version (Unpublish) | `unPublish` | Version marked unpublished, rollback if needed | ? Yes |
| Remove entity (Delete) | `delete` | Entity hard-deleted | ? No |

**The current implementation correctly handles all cases and corner cases.**
