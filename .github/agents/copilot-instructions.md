# Challenge Development Guidelines

Auto-generated from all feature plans. Last updated: 2026-03-11

## Active Technologies

- C# 13 / .NET 9, PowerShell 7 for Speckit automation + .NET SDK analyzers (CA1502), CodeMetricsConfig.txt AdditionalFiles, xUnit, PowerShell validation scripts, existing Speckit templates/agents (001-complexity-standard)

## Project Structure

```text
Challenge.API/
Challenge.Tests/
.specify/
.github/
specs/
codequality/
```

## Commands

- `dotnet build .\Challenge.sln`
- `dotnet test .\Challenge.Tests\Challenge.Tests.csproj`
- `pwsh .\.specify\scripts\powershell\validate-complexity.ps1 -SolutionPath .\Challenge.sln -ExceptionsPath .\codequality\complexity-exceptions.json`
- `pwsh .\.specify\scripts\powershell\export-complexity-baseline.ps1 -SolutionPath .\Challenge.sln -OutputPath .\codequality\complexity-exceptions.json -Force`

## Code Style

C# 13 / .NET 9, PowerShell 7 for Speckit automation: Follow standard conventions

## Quality Gates

- Handwritten production and test code should remain at cyclomatic complexity 15 or lower.
- Refactor over-threshold code by default; use the checked-in complexity exception manifest only for reviewed exceptions or grandfathered findings.

## Recent Changes

- 001-complexity-standard: Added C# 13 / .NET 9, PowerShell 7 for Speckit automation + .NET SDK analyzers (CA1502), CodeMetricsConfig.txt AdditionalFiles, xUnit, PowerShell validation scripts, existing Speckit templates/agents

<!-- MANUAL ADDITIONS START -->
<!-- MANUAL ADDITIONS END -->
