# Contract: Speckit Propagation

## Purpose

Define the minimum guidance contract that future Speckit-generated artifacts must
carry for the cyclomatic complexity standard.

## In-Scope Artifacts

- `.specify/memory/constitution.md`
- `.specify/templates/spec-template.md`
- `.specify/templates/plan-template.md`
- `.specify/templates/tasks-template.md`
- agent guidance or context files that shape generated implementation behavior

## Required Guidance

Each relevant artifact MUST preserve the following repository rule set:

1. Handwritten repository code, including tests, is expected to remain at cyclomatic complexity 15 or lower.
2. Generated code is excluded unless explicitly adopted as maintained handwritten code.
3. Contributors must refactor or document an approved exception when the limit cannot reasonably be met.
4. Threshold changes are governance changes, not local implementation choices.

## Verification Rule

A generated feature is compliant only if its specification, plan, tasks, or implementation guidance does not contradict the repository complexity standard and includes enough quality guidance to prevent silent drift.
