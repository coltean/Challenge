<!--
Sync Impact Report
- Version change: template -> 1.0.0
- Modified principles:
	- template principle 1 placeholder -> I. Authenticated Ingestion Boundary
	- template principle 2 placeholder -> II. Deterministic Processing and Idempotency
	- template principle 3 placeholder -> III. Contract-First Validation and Version Safety
	- template principle 4 placeholder -> IV. Observability and Operational Readiness
	- template principle 5 placeholder -> V. Security and Secret Management
- Added sections:
	- Technical Standards
	- Delivery Workflow and Quality Gates
- Removed sections:
	- None
- Templates requiring updates:
	- ✅ updated: .specify/templates/plan-template.md
	- ✅ updated: .specify/templates/spec-template.md
	- ✅ updated: .specify/templates/tasks-template.md
	- ⚠ pending: .specify/templates/commands/*.md (directory not present in repository)
- Deferred TODOs:
	- None
-->

# Challenge API Constitution

## Core Principles

### I. Authenticated Ingestion Boundary
Every CMS webhook ingestion endpoint MUST require authenticated access, MUST validate
caller role/intent, and MUST reject unauthorized requests before any payload processing.
Ingress handlers MUST perform coarse request checks (content type, batch size, schema shell)
so malformed traffic is stopped at the boundary.
Rationale: This API is an external ingestion surface and is the highest-risk entry point.

### II. Deterministic Processing and Idempotency
Event handling MUST be deterministic for a given ordered input and MUST be idempotent by
event identity. Duplicate event IDs MUST be safely ignored or no-op processed with audit
traceability. Background processing MUST preserve queue ordering guarantees within the
configured consumer strategy.
Rationale: CMS retries and network failures are normal; deterministic idempotent behavior
prevents data corruption and replay drift.

### III. Contract-First Validation and Version Safety
Changes to webhook payload contracts, event semantics, or entity version promotion rules MUST
include explicit contract examples and validation updates before implementation merge.
Publish, unpublish, and delete flows MUST define version-state transitions and edge-case
behavior in acceptance scenarios.
Rationale: Version state logic is domain-critical and must remain predictable across releases.

### IV. Observability and Operational Readiness
All ingestion and processing paths MUST emit structured logs with correlation identifiers and
outcome states. Health/readiness endpoints MUST reflect dependencies used in request handling
or background processing. Failures MUST be diagnosable from logs plus persisted audit data
without requiring source-level debugging.
Rationale: Background queue workflows fail asynchronously and require high-fidelity telemetry.

### V. Security and Secret Management
Credentials, tokens, and connection strings MUST NOT be hardcoded in production code paths.
Local demo defaults MAY exist only when clearly marked and overrideable by environment-based
configuration. Any new privileged endpoint MUST include explicit authorization policy tests.
Rationale: This service holds integration credentials and operational messaging access.

## Technical Standards

- Runtime baseline: .NET 9 and C# 13 for API and worker paths.
- Persistence: EF Core migrations MUST be additive and reversible when feasible.
- Messaging: Queue consumers MUST use explicit ack/nack behavior and bounded retry policy.
- Validation: Batch limits and payload validation rules MUST remain centralized and test-covered.
- Performance: Ingestion endpoint MUST favor fast accept-and-queue behavior over inline heavy
	processing for batch payloads.

## Delivery Workflow and Quality Gates

1. Specification first: each feature spec MUST include ingestion impact, processing impact,
	 and operational impact.
2. Test expectations: unit tests are REQUIRED for domain logic changes; integration tests are
	 REQUIRED for webhook contracts, auth flows, queue processing behavior, and version-state
	 transitions.
3. Pre-merge checks: build and relevant test suites MUST pass before merge.
4. Review discipline: pull requests MUST map changes to affected principles and note exceptions.
5. Release readiness: deployment notes MUST include config changes, migration notes, and rollback
	 considerations for data or queue semantics.

## Governance

This constitution supersedes conflicting informal practices for this repository.

- Amendment process: amendments MUST be proposed via pull request with a summary of changed
	principles, impacted templates, and migration implications.
- Approval: at least one maintainer approval is REQUIRED for PATCH/MINOR changes and two for
	MAJOR changes.
- Versioning policy: constitution versions follow semantic versioning.
	- MAJOR: principle removals, redefinitions, or governance changes that invalidate prior
		compliance expectations.
	- MINOR: new principle/section or materially expanded mandatory guidance.
	- PATCH: clarifications, wording improvements, and non-semantic refinements.
- Compliance review expectations: every implementation plan MUST include a Constitution Check;
	every task list MUST show how testing and observability obligations are satisfied; reviewers
	MUST block merges that violate mandatory rules unless an explicit, time-bound exception is
	documented.

**Version**: 1.0.0 | **Ratified**: 2026-03-10 | **Last Amended**: 2026-03-10
