#!/usr/bin/env pwsh

[CmdletBinding()]
param(
    [string]$SolutionPath = '.\Challenge.sln',
    [string]$ExceptionsPath = '.\codequality\complexity-exceptions.json',
    [string]$ArtifactsDir = '.\codequality\artifacts',
    [string[]]$SarifPath,
    [switch]$SkipBuild,
    [switch]$OutputJson
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

. "$PSScriptRoot\complexity-common.ps1"

try {
    $repoRoot = Get-ComplexityRepoRoot
    $threshold = Get-ComplexityThreshold -ConfigPath (Join-Path $repoRoot 'CodeMetricsConfig.txt')
    $resolvedArtifactsDir = Resolve-ComplexityPath -RepoRoot $repoRoot -Path $ArtifactsDir
    if (-not (Test-Path $resolvedArtifactsDir)) {
        New-Item -ItemType Directory -Path $resolvedArtifactsDir -Force | Out-Null
    }

    $resolvedSarifPaths = @()
    if ($SarifPath -and $SarifPath.Count -gt 0) {
        $resolvedSarifPaths = @($SarifPath | ForEach-Object { (Resolve-Path $_).Path })
    }
    elseif (-not $SkipBuild) {
        $resolvedSolutionPath = Resolve-ComplexityPath -RepoRoot $repoRoot -Path $SolutionPath
        $resolvedSarifPaths = Invoke-ComplexityBuild -RepoRoot $repoRoot -ArtifactsDir $resolvedArtifactsDir -SolutionPath $resolvedSolutionPath
    }
    else {
        throw 'SkipBuild requires at least one SARIF path.'
    }

    $resolvedExceptionsPath = Resolve-ComplexityPath -RepoRoot $repoRoot -Path $ExceptionsPath
    $exceptionDocument = Get-ComplexityExceptionDocument -ExceptionsPath $resolvedExceptionsPath -Threshold $threshold
    $findings = Get-ComplexityFindingsFromSarif -SarifPaths $resolvedSarifPaths -RepoRoot $repoRoot -ConfiguredThreshold $threshold
    $evaluation = Get-ComplexityEvaluation -Findings $findings -ExceptionDocument $exceptionDocument

    if ($OutputJson) {
        $evaluation | ConvertTo-Json -Depth 100
    }
    else {
        Write-Host ("Complexity validation result: {0}" -f $evaluation.result)
        Write-Host ("Threshold: {0}" -f $evaluation.threshold)
        Write-Host ("Findings: {0} total, {1} unsanctioned, {2} grandfathered, {3} approved exception" -f $evaluation.findingCount, $evaluation.unsanctionedCount, $evaluation.grandfatheredCount, $evaluation.approvedExceptionCount)
        foreach ($finding in $evaluation.findings) {
            Write-Host ("[{0}] {1} {2}:{3} (complexity {4})" -f $finding.state.ToUpperInvariant(), $finding.symbolId, $finding.filePath, $finding.line, $finding.measuredComplexity)
        }
    }

    if ($evaluation.result -eq 'FAIL') {
        exit 2
    }

    exit 0
}
catch {
    if ($OutputJson) {
        [PSCustomObject]@{
            result = 'ERROR'
            message = $_.Exception.Message
        } | ConvertTo-Json -Depth 10
    }
    else {
        Write-Error $_.Exception.Message
    }

    exit 1
}