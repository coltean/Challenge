#!/usr/bin/env pwsh

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

. "$PSScriptRoot\complexity-common.ps1"

$repoRoot = Get-ComplexityRepoRoot
$targets = @(
    '.specify/memory/constitution.md',
    '.specify/templates/spec-template.md',
    '.specify/templates/plan-template.md',
    '.specify/templates/tasks-template.md',
    '.specify/templates/agent-file-template.md',
    '.github/agents/copilot-instructions.md'
)

$missing = @()
foreach ($target in $targets) {
    $path = Join-Path $repoRoot ($target -replace '/', '\\')
    if (-not (Test-Path $path -PathType Leaf)) {
        $missing += "Missing file: $target"
        continue
    }

    $content = Get-Content -Path $path -Raw
    if ($content -notmatch 'cyclomatic complexity' -or $content -notmatch '\b15\b') {
        $missing += "Missing complexity guidance: $target"
    }
}

if ($missing.Count -gt 0) {
    $missing | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Host 'Speckit complexity propagation verified.'
exit 0