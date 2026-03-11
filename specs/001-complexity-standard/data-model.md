# Data Model: Enforce Cyclomatic Complexity Standard

## Entities

### Complexity Standard

- Purpose: Defines the repository-wide rule for acceptable cyclomatic complexity.
- Fields:
  - `ruleId`: analyzer rule identifier, expected to be `CA1502`
  - `threshold`: maximum allowed cyclomatic complexity, fixed at `15`
  - `severity`: contributor-facing diagnostic severity in normal analysis output
  - `enforcementMode`: repository pass/fail behavior for local validation and CI
  - `scope`: handwritten repository code, including tests
  - `excludedGeneratedPatterns`: generated code naming or path patterns treated as out of scope
- Validation rules:
  - `threshold` must be a positive integer and equal to the approved policy value
  - `scope` cannot exclude handwritten tests without an explicit policy change

### Compliance Finding

- Purpose: Represents a single CA1502 violation detected during analysis.
- Fields:
  - `project`: project containing the finding
  - `symbolId`: normalized symbol identity for the violating member
  - `filePath`: repository-relative source path
  - `line`: primary reported location
  - `measuredComplexity`: computed cyclomatic complexity
  - `threshold`: effective configured threshold
  - `fingerprint`: stable fingerprint of the violating implementation used for grandfathering checks
  - `state`: `new`, `grandfathered`, `approved-exception`, `resolved`, or `ignored-generated`
- Validation rules:
  - `measuredComplexity` must be greater than `threshold` for an active finding
  - `fingerprint` must be recomputable from repository state

### Exception Entry

- Purpose: Checked-in record authorizing a grandfathered or approved violation.
- Fields:
  - `entryType`: `grandfathered` or `approved-exception`
  - `project`: owning project
  - `symbolId`: target symbol
  - `filePath`: source path
  - `fingerprint`: expected fingerprint of the violating implementation
  - `justification`: required human-readable explanation
  - `createdOn`: creation date
  - `reviewReference`: review, issue, or approval reference
  - `expiresOn`: optional date for temporary exceptions
- Validation rules:
  - `justification` must be non-empty
  - `fingerprint` must match current code for the entry to remain valid
  - `approved-exception` entries should include a review reference

### Guidance Artifact

- Purpose: Any Speckit or governance artifact that must carry the complexity rule forward.
- Fields:
  - `artifactPath`: repository path to the template or guidance file
  - `artifactType`: constitution, plan template, tasks template, spec template, agent guidance, or generated feature artifact
  - `requiredStatement`: the policy statement or quality gate that must appear
  - `status`: `pending`, `updated`, or `verified`

## Relationships

- One `Complexity Standard` governs many `Compliance Finding` records.
- One `Exception Entry` may authorize at most one active `Compliance Finding` fingerprint at a time.
- One `Complexity Standard` governs many `Guidance Artifact` updates.

## State Transitions

### Compliance Finding State

1. `new` → `grandfathered`
   - Condition: finding matches an approved baseline entry and fingerprint.
2. `new` → `approved-exception`
   - Condition: finding matches an approved exception entry and fingerprint.
3. `grandfathered` → `new`
   - Condition: symbol remains over threshold but fingerprint changes.
4. `approved-exception` → `new`
   - Condition: exception expires or fingerprint changes without renewal.
5. `new` or `grandfathered` or `approved-exception` → `resolved`
   - Condition: code no longer violates CA1502.
6. Any state → `ignored-generated`
   - Condition: file is classified as generated and therefore out of scope.
