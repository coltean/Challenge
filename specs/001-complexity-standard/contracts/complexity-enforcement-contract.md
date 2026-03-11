# Contract: Complexity Enforcement

## Purpose

Define the repository-facing contract for how cyclomatic complexity is configured,
measured, grandfathered, excepted, and enforced.

## Inputs

- Root `CodeMetricsConfig.txt` containing the CA1502 threshold.
- Root `.editorconfig` containing CA1502 severity and generated-code patterns.
- Shared `Directory.Build.props` wiring analyzer configuration into all projects.
- Checked-in baseline/exception record under a repository-managed `codequality/` path.
- Analyzer output produced during repository validation.

## Validation Rules

1. The effective CA1502 threshold is 15 for all handwritten repository code, including tests.
2. Compiler-generated and tool-generated files are excluded when classified as generated code.
3. A finding is allowed only if it matches a checked-in baseline/exception record with a current fingerprint.
4. A grandfathered finding becomes non-compliant when the associated fingerprint changes and the finding still exceeds the threshold.
5. Any new or modified unsanctioned finding causes repository validation and CI to fail.

## Required Output

Validation output MUST provide:

- project name
- symbol identity
- source file path
- measured complexity and threshold
- compliance state
- remediation message or exception guidance

## Failure Semantics

- `PASS`: no unsanctioned findings exist in handwritten in-scope code.
- `FAIL`: at least one new or modified finding exists without a valid checked-in exception record.
- `ERROR`: validation cannot determine compliance because required inputs or analyzer output are missing or invalid.
