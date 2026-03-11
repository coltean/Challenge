# Feature Specification: Enforce Cyclomatic Complexity Standard

**Feature Branch**: `001-complexity-standard`  
**Created**: 2026-03-11  
**Status**: Draft  
**Input**: User description: "I want to enforce cyclomatic complexity to 15 for all code in this code base and all future code that will be generated through speckit."

## Clarifications

### Session 2026-03-11

- Q: How should existing methods already above cyclomatic complexity 15 be handled at adoption time? → A: Grandfather existing violations, but fail any new or modified code above 15.
- Q: How should the standard be enforced in automation? → A: Fail validation and CI for new or modified code above 15.
- Q: What code should the complexity limit apply to? → A: Apply to all handwritten repository code, including tests; exclude compiler-generated and tool-generated files.

## User Scenarios & Testing *(mandatory)*

<!--
  IMPORTANT: User stories should be PRIORITIZED as user journeys ordered by importance.
  Each user story/journey must be INDEPENDENTLY TESTABLE - meaning if you implement just ONE of them,
  you should still have a viable MVP (Minimum Viable Product) that delivers value.
  
  Assign priorities (P1, P2, P3, etc.) to each story, where P1 is the most critical.
  Think of each story as a standalone slice of functionality that can be:
  - Developed independently
  - Tested independently
  - Deployed independently
  - Demonstrated to users independently
-->

### User Story 1 - Block Excessively Complex Code (Priority: P1)

As a maintainer, I want the repository to detect code with cyclomatic complexity above 15 so that newly introduced code remains understandable, reviewable, and safer to change.

**Why this priority**: This is the core policy outcome. Without repository-level detection, the standard remains advisory and cannot reliably protect the codebase.

**Independent Test**: Can be fully tested by introducing a method that exceeds the approved complexity limit and verifying that the repository reports the violation and prevents the change from being treated as compliant.

**Acceptance Scenarios**:

1. **Given** a contributor adds code with cyclomatic complexity of 16 or higher, **When** repository validation or CI is run, **Then** the violation is surfaced with enough detail for the contributor to identify the offending code and the change is treated as non-compliant.
2. **Given** a contributor adds code with cyclomatic complexity of 15 or lower, **When** repository validation is run, **Then** the code is not flagged for complexity-limit breach.

---

### User Story 2 - Apply the Same Standard to Generated Work (Priority: P2)

As a maintainer using Speckit, I want generated plans, tasks, and implementation workflows to reflect the same complexity limit so that future generated code follows the repository standard by default.

**Why this priority**: The user explicitly wants the rule applied to future Speckit-generated work, so governance and generation guidance must be aligned after repository enforcement exists.

**Independent Test**: Can be fully tested by generating a new feature workflow and verifying that the planning, tasking, and implementation guidance explicitly carries the complexity rule forward.

**Acceptance Scenarios**:

1. **Given** a new Speckit-generated feature is created after this change, **When** its planning and implementation artifacts are produced, **Then** they include the repository complexity standard as part of expected code quality guidance.
2. **Given** a generated implementation would exceed the approved limit, **When** the workflow is reviewed, **Then** the output directs the contributor to refactor or justify the exception instead of silently accepting the complexity.

---

### User Story 3 - Support Controlled Exceptions (Priority: P3)

As a reviewer, I want any exception to the complexity limit to be explicit and reviewable so that unavoidable complexity is documented rather than hidden.

**Why this priority**: Some edge cases may require exceptions, but exceptions must remain visible and deliberate instead of weakening the standard globally.

**Independent Test**: Can be fully tested by submitting code that exceeds the limit together with an exception path and verifying that the workflow requires explicit documentation rather than silently allowing the breach.

**Acceptance Scenarios**:

1. **Given** code exceeds the approved complexity limit for a justified reason, **When** it is proposed for review, **Then** the workflow requires an explicit exception record describing why the limit could not reasonably be met.
2. **Given** code exceeds the approved complexity limit without a documented exception, **When** it is reviewed, **Then** it is treated as non-compliant.

---

### Edge Cases

- Compiler-generated and tool-generated files are excluded from evaluation; handwritten repository code, including tests, remains in scope.
- Legacy methods already above the limit at adoption time are grandfathered, but any new or modified code above 15 is treated as non-compliant.
- What happens when multiple methods each stay under 15 individually but together still create an unreadable workflow?
- How are violations reported when the same logical change touches both production code and generated artifacts?
- What happens when the enforcement mechanism becomes unavailable or produces inconclusive results during validation?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The repository MUST define a cyclomatic complexity limit of 15 for code under its control.
- **FR-002**: The repository MUST evaluate new and modified code against that limit during local validation and CI workflows.
- **FR-003**: The repository MUST surface complexity violations in a way that identifies the offending code element for the contributor and reviewer.
- **FR-004**: Speckit-generated planning and implementation guidance MUST include the approved complexity standard for future generated work.
- **FR-005**: The workflow MUST require explicit documentation for any approved exception to the complexity limit.
- **FR-006**: The standard MUST apply to all handwritten repository code, including test code.
- **FR-007**: The workflow MUST grandfather methods already above the limit at adoption time while treating any new or modified code above 15 as non-compliant.
- **FR-008**: The workflow MUST prevent silent weakening of the standard by requiring any threshold change to go through the same governance path as other repository-wide coding standards.
- **FR-009**: CI MUST fail for new or modified code above cyclomatic complexity 15 unless an explicit approved exception is present.
- **FR-010**: Compiler-generated files and tool-generated files MUST be excluded from complexity enforcement unless they are explicitly converted into maintained handwritten code.

### Constitution Alignment *(mandatory)*

- **CA-001 Authentication Boundary**: This feature does not introduce or alter runtime authentication behavior; its scope is repository quality governance rather than API access control.
- **CA-002 Determinism and Idempotency**: Repository validation for the complexity rule MUST produce the same compliance result when run repeatedly against unchanged code.
- **CA-003 Contract and Version Safety**: Any changes to Speckit templates or repository policies MUST preserve a clear contract for what qualifies as compliant code and what requires an exception.
- **CA-004 Observability**: Compliance checks MUST provide visible feedback to contributors and reviewers when complexity violations occur in both local validation and CI execution.
- **CA-005 Secret Management**: This feature MUST NOT depend on embedded secrets or privileged credentials to evaluate compliance.

### Key Entities *(include if feature involves data)*

- **Complexity Standard**: The approved repository-wide rule, including threshold value, scope of application, and exception policy.
- **Compliance Result**: The outcome of evaluating code against the standard, including pass/fail state and violation details.
- **Exception Record**: The documented justification for code that is intentionally allowed to exceed the limit.
- **Generated Guidance Artifact**: A Speckit-produced specification, plan, task list, or instruction file that must carry the standard forward.

## Assumptions

- The approved threshold applies to both current handwritten code and future code generated through Speckit workflows.
- Existing code that already exceeds the limit at adoption time is grandfathered, but new and modified code must meet the standard unless an explicit exception is approved.
- Handwritten repository code includes both application code and tests.
- Compiler-generated files and tool-generated files are outside the enforcement scope unless the repository explicitly adopts them as maintained handwritten code.
- Repository-level enforcement is expected to be machine-checkable rather than purely advisory.
- Exception handling is allowed only when explicitly documented and reviewed.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of newly introduced methods above cyclomatic complexity 15 are reported as non-compliant unless accompanied by an explicit approved exception.
- **SC-001a**: 100% of grandfathered violations remain informational only until the affected code is modified, at which point the standard is re-applied.
- **SC-001b**: 100% of new or modified methods above cyclomatic complexity 15 cause CI to fail unless an explicit approved exception is present.
- **SC-001c**: 100% of generated files excluded by policy do not produce repository compliance failures solely because of their generated complexity.
- **SC-002**: 100% of newly generated Speckit feature artifacts reference the repository complexity standard in their quality or governance guidance.
- **SC-003**: Reviewers can determine whether a complexity violation exists and where it occurs within 2 minutes using the reported compliance output.
- **SC-004**: Threshold changes or exceptions can be traced to an explicit documented decision in 100% of cases.
