# Complexity Exception Workflow

## Standard

Handwritten production and test code must stay at cyclomatic complexity 15 or lower.
Repository validation uses CA1502 findings plus the checked-in manifest in `codequality/complexity-exceptions.json`.

## Entry Types

- `grandfathered`: Existing finding accepted during standards adoption until the implementation changes.
- `approved-exception`: Explicit reviewed exception for a current over-threshold handwritten symbol.

## Required Fields

- `entryType`: Either `grandfathered` or `approved-exception`.
- `project`: Project name that produced the finding.
- `symbolId`: Analyzer symbol identifier for the handwritten method.
- `filePath`: Repository-relative source path.
- `fingerprint`: Hash of the current source range used to detect implementation changes.
- `justification`: Short reviewer-facing reason the exception exists.
- `createdOn`: ISO date when the entry was recorded.
- `reviewReference`: Pull request, issue, or approval reference.
- `expiresOn`: Optional ISO date for time-bound approved exceptions.

## Reviewer Guidance

1. Prefer refactoring over adding or extending exception entries.
2. Use `grandfathered` only for adoption baselines, not for new code.
3. Use `approved-exception` only when the complexity is deliberate, documented, and reviewed.
4. Remove the entry once the code is refactored below the threshold.
5. If the fingerprint changes, treat the finding as new work and require a fresh review.

## Commands

```powershell
pwsh .\.specify\scripts\powershell\validate-complexity.ps1 -SolutionPath .\Challenge.sln -ExceptionsPath .\codequality\complexity-exceptions.json
pwsh .\.specify\scripts\powershell\export-complexity-baseline.ps1 -SolutionPath .\Challenge.sln -OutputPath .\codequality\complexity-exceptions.json -Force
```