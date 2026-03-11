#!/usr/bin/env pwsh

[CmdletBinding()]
param(
    [string]$SolutionPath = '.\Challenge.sln',
    [string]$OutputPath = '.\codequality\complexity-exceptions.json',
    [string]$ArtifactsDir = '.\codequality\artifacts',
    [string[]]$SarifPath,
    [switch]$SkipBuild,
    [switch]$OutputJson,
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

. "$PSScriptRoot\complexity-common.ps1"

try {
    $repoRoot = Get-ComplexityRepoRoot
    $threshold = Get-ComplexityThreshold -ConfigPath (Join-Path $repoRoot 'CodeMetricsConfig.txt')
    $resolvedOutputPath = Resolve-ComplexityPath -RepoRoot $repoRoot -Path $OutputPath

    if ((Test-Path $resolvedOutputPath) -and -not $Force) {
        throw "Output file already exists: $resolvedOutputPath. Re-run with -Force to overwrite."
    }

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

    $findings = Get-ComplexityFindingsFromSarif -SarifPaths $resolvedSarifPaths -RepoRoot $repoRoot -ConfiguredThreshold $threshold
    $entries = @($findings | Sort-Object SymbolId, FilePath | ForEach-Object {
        [PSCustomObject]@{
            entryType = 'grandfathered'
            project = $_.Project
            symbolId = $_.SymbolId
            filePath = $_.FilePath
            fingerprint = $_.Fingerprint
            justification = 'Grandfathered during complexity standard adoption'
            createdOn = (Get-Date).ToString('yyyy-MM-dd')
            reviewReference = 'initial-baseline'
            expiresOn = $null
        }
    })

    $document = [PSCustomObject]@{
        version = 1
        ruleId = 'CA1502'
        threshold = $threshold
        entries = $entries
    }

    $directory = Split-Path $resolvedOutputPath -Parent
    if (-not (Test-Path $directory)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }

    $json = $document | ConvertTo-Json -Depth 100
    Set-Content -Path $resolvedOutputPath -Value $json

    if ($OutputJson) {
        $document | ConvertTo-Json -Depth 100
    }
    else {
        Write-Host ("Exported {0} grandfathered complexity entries to {1}" -f $entries.Count, $resolvedOutputPath)
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