# Quickstart: Enforce Cyclomatic Complexity Standard

## Goal

Validate that the repository enforces cyclomatic complexity 15 for handwritten code,
grandfathers existing violations until modified, and propagates the same rule through
Speckit guidance.

## Prerequisites

- .NET 9 SDK installed
- Repository cloned locally
- Active feature branch available

## Validation Flow

1. Restore and build the solution to ensure analyzer configuration is active.

```powershell
dotnet build .\Challenge.sln
```

2. Run the repository validation path that evaluates CA1502 findings against the checked-in baseline/exception record.

```powershell
pwsh .\.specify\scripts\powershell\validate-complexity.ps1 -SolutionPath .\Challenge.sln -ExceptionsPath .\codequality\complexity-exceptions.json
```

3. Run the code-quality test suite to verify SARIF parsing, baseline export, propagation guidance, and exception handling.

```powershell
dotnet test .\Challenge.Tests\Challenge.Tests.csproj --filter CodeQuality
```

4. Confirm that existing grandfathered violations do not fail validation unless the violating implementation has changed.

5. Introduce a temporary handwritten method with cyclomatic complexity above 15 in a test branch and rerun validation.
   Expected result: validation surfaces the offending symbol and fails.

6. Remove the temporary method or refactor it below the threshold and rerun validation.
   Expected result: the violation disappears.

7. If you intentionally grandfather current findings, refresh the checked-in manifest:

```powershell
pwsh .\.specify\scripts\powershell\export-complexity-baseline.ps1 -SolutionPath .\Challenge.sln -OutputPath .\codequality\complexity-exceptions.json -Force
```

8. Generate or update a Speckit feature artifact and confirm the complexity standard appears in the relevant guidance/template output.

## Expected Outcomes

- New or modified unsanctioned CA1502 findings fail validation.
- Unchanged grandfathered findings remain informational only.
- Generated-code exclusions do not create false failures for out-of-scope files.
- Speckit artifacts continue to carry the repository complexity rule forward.