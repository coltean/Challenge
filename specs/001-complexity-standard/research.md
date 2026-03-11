# Research: Enforce Cyclomatic Complexity Standard

## Decision 1: Use .NET CA1502 as the source of complexity diagnostics

- Decision: Use the built-in .NET code analysis rule CA1502 (`Avoid excessive complexity`) as the authoritative source for cyclomatic complexity findings.
- Rationale: CA1502 is the native .NET analyzer for cyclomatic complexity, integrates with solution builds, and supports configurable thresholds through `CodeMetricsConfig.txt`.
- Alternatives considered: A custom Roslyn analyzer was rejected because it would duplicate platform behavior and increase maintenance burden. Third-party reporting tools were rejected for this phase because the requirement is enforcement and propagation, not enterprise reporting.

## Decision 2: Configure the threshold through a shared `CodeMetricsConfig.txt` and shared build wiring

- Decision: Add a root `CodeMetricsConfig.txt` with `CA1502: 15` and wire it into all relevant projects via a shared `Directory.Build.props` entry using `AdditionalFiles`.
- Rationale: Microsoft documents CA1502 threshold configuration through `CodeMetricsConfig.txt`, not through a simple `.editorconfig` numeric option. A shared props file avoids duplicating configuration across both projects and future projects.
- Alternatives considered: Per-project `AdditionalFiles` entries were rejected because they are easy to drift. Relying on the CA1502 default threshold of 25 was rejected because it does not satisfy the feature requirement.

## Decision 3: Surface CA1502 in the IDE/build as a warning, but enforce pass/fail through repository validation logic

- Decision: Configure CA1502 with visible analyzer severity and make repository validation/CI responsible for the final pass/fail decision on new or modified code.
- Rationale: A direct analyzer-as-error configuration would immediately fail the whole solution if any existing violations are present, which conflicts with the approved grandfathering policy. Keeping the rule visible while applying custom repository validation preserves both developer feedback and staged adoption.
- Alternatives considered: Making CA1502 an unconditional build error was rejected because it cannot distinguish grandfathered violations from new ones. Review-only enforcement was rejected because the user explicitly chose automation failure.

## Decision 4: Store grandfathered and approved exceptions in a checked-in repository record tied to symbols and fingerprints

- Decision: Maintain a checked-in exception/baseline record for existing or approved violations, including project, symbol identity, file path, and a fingerprint of the violating implementation.
- Rationale: The policy requires explicit documented exceptions and also requires grandfathered violations to be re-evaluated when modified. A fingerprinted baseline allows unchanged historical violations to remain informational while forcing re-approval or remediation when the implementation changes.
- Alternatives considered: Pure `GlobalSuppressions.cs` entries were rejected as the primary mechanism because they do not naturally distinguish unchanged grandfathered violations from modified ones. Inline `#pragma` suppression was rejected because it is too easy to hide and does not create a central reviewable exception record.

## Decision 5: Exclude generated code through analyzer conventions and explicit generated-code patterns

- Decision: Rely on default analyzer generated-code exclusions and add explicit `.editorconfig` `generated_code = true` patterns for repository-specific generated naming conventions when needed.
- Rationale: The policy excludes compiler-generated and tool-generated code. The .NET analyzer system already understands common generated-code patterns and supports additional scoped exclusions without disabling analysis for handwritten code.
- Alternatives considered: Excluding folders wholesale through ad hoc scripts was rejected because it would be harder to keep aligned with analyzer behavior. Including generated code in enforcement was rejected because it conflicts with the approved scope.

## Decision 6: Propagate the standard through Speckit governance and template assets, not only runtime build config

- Decision: Update the constitution, Speckit templates, and agent guidance so future generated specs, plans, tasks, and implementations treat the 15-threshold policy as a default repository standard.
- Rationale: The user explicitly requires future Speckit-generated code to follow the same rule. Repository tooling alone does not guarantee generated artifacts will mention the policy or design around it.
- Alternatives considered: Relying only on human memory or one-time documentation was rejected because it would not reliably shape future generated work.
