---

description: "Task list for enforcing cyclomatic complexity 15 across the repository and future Speckit output"
---

# Tasks: Enforce Cyclomatic Complexity Standard

**Input**: Design documents from `/specs/001-complexity-standard/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: Tests are required for this feature because it changes repository validation behavior, CI behavior, and Speckit guidance propagation.

**Organization**: Tasks are grouped by user story so each story can be implemented and validated independently.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create the shared repository files and folders required for analyzer-based complexity enforcement.

- [X] T001 Create the code quality workspace and seed checked-in exception storage in codequality/complexity-exceptions.json
- [X] T002 Create shared analyzer wiring for all projects in Directory.Build.props
- [X] T003 [P] Create CA1502 threshold configuration in CodeMetricsConfig.txt
- [X] T004 [P] Create repository analyzer and generated-code exclusions in .editorconfig
- [X] T005 [P] Create CI workflow scaffold for complexity validation in .github/workflows/complexity-validation.yml

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Build the reusable validation, baseline, and fixture infrastructure that all user stories depend on.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T006 Create local/CI CA1502 validation script in .specify/scripts/powershell/validate-complexity.ps1
- [X] T007 [P] Create baseline export/update script in .specify/scripts/powershell/export-complexity-baseline.ps1
- [X] T008 [P] Create analyzer fixture data in Challenge.Tests/CodeQuality/Fixtures/ca1502-findings.sarif
- [X] T009 [P] Create exception fixture data in Challenge.Tests/CodeQuality/Fixtures/complexity-exceptions.sample.json
- [X] T010 Create shared validation test support in Challenge.Tests/CodeQuality/ComplexityValidationTestSupport.cs
- [X] T011 Update repository command documentation for the new validation entry points in README.md

**Checkpoint**: Foundation ready. User story implementation can now proceed.

---

## Phase 3: User Story 1 - Block Excessively Complex Code (Priority: P1) 🎯 MVP

**Goal**: Fail local validation and CI when new or modified handwritten code exceeds cyclomatic complexity 15, while keeping grandfathered violations informational only.

**Independent Test**: Introduce a handwritten method above complexity 15, run the validation flow, and confirm the result fails with symbol/file detail; confirm unchanged grandfathered entries do not fail.

### Tests for User Story 1 ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T012 [P] [US1] Add contract tests for compliance result states in Challenge.Tests/CodeQuality/ComplexityEnforcementContractTests.cs
- [X] T013 [P] [US1] Add integration tests for local and CI failure behavior in Challenge.Tests/CodeQuality/ComplexityEnforcementWorkflowTests.cs

### Implementation for User Story 1

- [X] T014 [US1] Wire CA1502 analyzer inputs across Challenge.API/Challenge.API.csproj and Challenge.Tests/Challenge.Tests.csproj through Directory.Build.props and CodeMetricsConfig.txt
- [X] T015 [US1] Implement analyzer output parsing and unsanctioned-finding detection in .specify/scripts/powershell/validate-complexity.ps1
- [X] T016 [US1] Populate grandfathered baseline entries in codequality/complexity-exceptions.json
- [X] T017 [US1] Finish CI failure behavior in .github/workflows/complexity-validation.yml
- [X] T018 [US1] Update validation instructions for contributors in README.md and specs/001-complexity-standard/quickstart.md

**Checkpoint**: User Story 1 is complete when a new or modified >15 complexity method fails validation and CI, while unchanged baseline entries remain informational.

---

## Phase 4: User Story 2 - Apply the Same Standard to Generated Work (Priority: P2)

**Goal**: Ensure future Speckit-generated specifications, plans, tasks, and implementation guidance carry the same complexity rule forward.

**Independent Test**: Inspect the updated Speckit templates and guidance files and confirm they require handwritten code to remain at complexity 15 or lower and direct contributors toward refactoring or documented exceptions.

### Tests for User Story 2 ⚠️

- [X] T019 [P] [US2] Add propagation contract tests for guidance files in Challenge.Tests/CodeQuality/SpeckitPropagationTests.cs
- [X] T020 [P] [US2] Add integration tests for generated artifact guidance continuity in Challenge.Tests/CodeQuality/SpeckitFeatureGenerationGuidanceTests.cs

### Implementation for User Story 2

- [X] T021 [US2] Amend repository-wide policy to include the complexity standard in .specify/memory/constitution.md
- [X] T022 [US2] Update Speckit generation templates in .specify/templates/spec-template.md, .specify/templates/plan-template.md, and .specify/templates/tasks-template.md
- [X] T023 [US2] Update agent guidance in .github/agents/copilot-instructions.md and .specify/templates/agent-file-template.md
- [X] T024 [US2] Create propagation verification script in .specify/scripts/powershell/verify-speckit-complexity-propagation.ps1

**Checkpoint**: User Story 2 is complete when relevant Speckit artifacts explicitly preserve the complexity standard and no generated guidance contradicts it.

---

## Phase 5: User Story 3 - Support Controlled Exceptions (Priority: P3)

**Goal**: Require explicit, checked-in, reviewable exception records for approved over-threshold code and re-open violations when fingerprints change.

**Independent Test**: Add an exception record for an over-threshold symbol, confirm validation allows it while the fingerprint matches, then change the implementation and confirm validation treats it as a new violation.

### Tests for User Story 3 ⚠️

- [X] T025 [P] [US3] Add exception record contract tests in Challenge.Tests/CodeQuality/ComplexityExceptionRecordTests.cs
- [X] T026 [P] [US3] Add integration tests for grandfathered and exception fingerprint transitions in Challenge.Tests/CodeQuality/GrandfatheredViolationTransitionTests.cs

### Implementation for User Story 3

- [X] T027 [US3] Define the checked-in exception record format and reviewer guidance in codequality/complexity-exceptions.json and codequality/README.md
- [X] T028 [US3] Implement fingerprint generation and exception matching in .specify/scripts/powershell/export-complexity-baseline.ps1 and .specify/scripts/powershell/validate-complexity.ps1
- [X] T029 [US3] Add reviewer-facing exception workflow documentation in README.md and specs/001-complexity-standard/quickstart.md

**Checkpoint**: User Story 3 is complete when exceptions are centrally documented, reviewable, and invalidated automatically when the protected code changes.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final hardening, edge-case coverage, and end-to-end validation across all stories.

- [X] T030 [P] Add parser and edge-case unit tests in Challenge.Tests/CodeQuality/ComplexityParserEdgeCaseTests.cs
- [X] T031 Refine validation and baseline scripts for maintainability in .specify/scripts/powershell/validate-complexity.ps1 and .specify/scripts/powershell/export-complexity-baseline.ps1
- [X] T032 [P] Validate generated-code exclusions and CI output clarity in .editorconfig and .github/workflows/complexity-validation.yml
- [X] T033 [P] Run and document the final quickstart validation path in specs/001-complexity-standard/quickstart.md

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies; can start immediately.
- **Foundational (Phase 2)**: Depends on Setup completion; blocks all user stories.
- **User Story 1 (Phase 3)**: Depends on Foundational completion; delivers the MVP enforcement path.
- **User Story 2 (Phase 4)**: Depends on Foundational completion; can run in parallel with US1 if staffed, but benefits from US1 decisions being stable.
- **User Story 3 (Phase 5)**: Depends on Foundational completion and should build on the validation path established in US1.
- **Polish (Phase 6)**: Depends on all desired user stories being complete.

### User Story Dependencies

- **User Story 1 (P1)**: Can start immediately after Foundational and should be completed first for MVP value.
- **User Story 2 (P2)**: Can start after Foundational; independent in outcome, but should align with the enforcement shape delivered by US1.
- **User Story 3 (P3)**: Depends conceptually on the validation pipeline from US1 because exceptions are evaluated against that pipeline.

### Within Each User Story

- Tests MUST be written and fail before implementation.
- Shared configuration before validation logic.
- Validation logic before CI/documentation wiring.
- Exception storage before fingerprint-based exception matching.

### Parallel Opportunities

- T003, T004, and T005 can run in parallel after T001/T002 planning is understood.
- T007, T008, and T009 can run in parallel with each other after T006 is scoped.
- US1 tests T012 and T013 can run in parallel.
- US2 tests T019 and T020 can run in parallel.
- US3 tests T025 and T026 can run in parallel.
- Polish tasks T030, T032, and T033 can run in parallel.

---

## Parallel Example: User Story 1

```text
Task: "Add contract tests for compliance result states in Challenge.Tests/CodeQuality/ComplexityEnforcementContractTests.cs"
Task: "Add integration tests for local and CI failure behavior in Challenge.Tests/CodeQuality/ComplexityEnforcementWorkflowTests.cs"
```

```text
Task: "Create CA1502 threshold configuration in CodeMetricsConfig.txt"
Task: "Create repository analyzer and generated-code exclusions in .editorconfig"
Task: "Create CI workflow scaffold for complexity validation in .github/workflows/complexity-validation.yml"
```

---

## Parallel Example: User Story 2

```text
Task: "Add propagation contract tests for guidance files in Challenge.Tests/CodeQuality/SpeckitPropagationTests.cs"
Task: "Add integration tests for generated artifact guidance continuity in Challenge.Tests/CodeQuality/SpeckitFeatureGenerationGuidanceTests.cs"
```

---

## Parallel Example: User Story 3

```text
Task: "Add exception record contract tests in Challenge.Tests/CodeQuality/ComplexityExceptionRecordTests.cs"
Task: "Add integration tests for grandfathered and exception fingerprint transitions in Challenge.Tests/CodeQuality/GrandfatheredViolationTransitionTests.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Setup.
2. Complete Foundational work.
3. Complete User Story 1.
4. Validate with a deliberately over-threshold handwritten method and confirm failure behavior.
5. Stop and review before expanding into propagation and exception handling.

### Incremental Delivery

1. Deliver enforcement for handwritten code first (US1).
2. Add Speckit propagation so future generated work inherits the standard (US2).
3. Add explicit exception management and fingerprint invalidation (US3).
4. Finish with edge-case hardening and quickstart verification.

### Parallel Team Strategy

1. One developer handles root config and validation scripts.
2. One developer handles test coverage for CA1502 parsing and workflow behavior.
3. One developer handles Speckit template/constitution propagation once the enforcement contract is stable.

---

## Notes

- All tasks include exact target file paths.
- The MVP is User Story 1 because it delivers actual repository enforcement.
- User Story 2 and User Story 3 remain independently testable increments after Foundational work.
- Threshold changes are governance work and should not be folded into normal implementation tasks.