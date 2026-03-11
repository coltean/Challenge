#!/usr/bin/env pwsh

Set-StrictMode -Version Latest

function Get-ComplexityRepoRoot {
    param(
        [string]$StartPath = $PSScriptRoot
    )

    $current = (Resolve-Path $StartPath).Path
    while ($true) {
        if (Test-Path (Join-Path $current '.git') -PathType Container) {
            return $current
        }

        if (Test-Path (Join-Path $current 'Challenge.sln') -PathType Leaf) {
            return $current
        }

        $parent = Split-Path $current -Parent
        if ($parent -eq $current) {
            throw 'Unable to locate repository root for complexity tooling.'
        }

        $current = $parent
    }
}

function Get-ComplexityThreshold {
    param(
        [string]$ConfigPath
    )

    if (-not (Test-Path $ConfigPath -PathType Leaf)) {
        throw "Code metrics configuration not found: $ConfigPath"
    }

    $line = Get-Content -Path $ConfigPath | Where-Object { $_ -match '^CA1502:\s*\d+$' } | Select-Object -First 1
    if (-not $line) {
        throw "Unable to locate CA1502 threshold in $ConfigPath"
    }

    return [int]($line.Split(':')[1].Trim())
}

function Get-ProjectBuildTargets {
    param(
        [string]$RepoRoot,
        [string]$SolutionPath
    )

    if ($SolutionPath) {
        $resolved = Resolve-Path $SolutionPath -ErrorAction Stop
        if ($resolved.Path.EndsWith('.csproj', [System.StringComparison]::OrdinalIgnoreCase)) {
            return @(Get-Item $resolved.Path)
        }

        if ($resolved.Path.EndsWith('.sln', [System.StringComparison]::OrdinalIgnoreCase)) {
            return Get-ChildItem -Path $RepoRoot -Recurse -Filter *.csproj | Where-Object {
                $_.FullName -notmatch '\\(bin|obj)\\'
            } | Sort-Object FullName
        }
    }

    return Get-ChildItem -Path $RepoRoot -Recurse -Filter *.csproj | Where-Object {
        $_.FullName -notmatch '\\(bin|obj)\\'
    } | Sort-Object FullName
}

function Invoke-ComplexityBuild {
    param(
        [string]$RepoRoot,
        [string]$ArtifactsDir,
        [string]$SolutionPath
    )

    $targets = Get-ProjectBuildTargets -RepoRoot $RepoRoot -SolutionPath $SolutionPath
    if (-not $targets -or $targets.Count -eq 0) {
        throw 'No project files were found for complexity validation.'
    }

    $sarifFiles = @()

    foreach ($target in $targets) {
        $sarifPath = Join-Path $ArtifactsDir ("{0}.sarif" -f $target.BaseName)
        $command = @('build', $target.FullName, '--nologo', "/p:ErrorLog=$sarifPath")
        & dotnet @command
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet build failed for $($target.FullName)"
        }

        $sarifFiles += $sarifPath
    }

    return $sarifFiles
}

function Get-RelativeRepoPath {
    param(
        [string]$RepoRoot,
        [string]$AbsolutePath
    )

    $repoUri = New-Object System.Uri((Resolve-Path $RepoRoot).Path.TrimEnd('\') + '\')
    $fileUri = New-Object System.Uri((Resolve-Path $AbsolutePath).Path)
    $relative = $repoUri.MakeRelativeUri($fileUri).ToString()
    return [System.Uri]::UnescapeDataString($relative).Replace('/', '\')
}

function Get-HashString {
    param(
        [AllowEmptyString()]
        [string]$Value
    )

    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($Value)
        $hash = $sha.ComputeHash($bytes)
        return ([System.BitConverter]::ToString($hash)).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $sha.Dispose()
    }
}

function Get-ComplexityFingerprint {
    param(
        [string]$FilePath,
        [int]$StartLine,
        [int]$EndLine,
        [string]$SymbolId
    )

    if (-not (Test-Path $FilePath -PathType Leaf)) {
        return Get-HashString -Value "$SymbolId|missing-file|$StartLine|$EndLine"
    }

    $lines = Get-Content -Path $FilePath
    if ($lines.Count -eq 0) {
        return Get-HashString -Value "$SymbolId|empty-file"
    }

    $startIndex = [Math]::Max($StartLine - 1, 0)
    $endIndex = if ($EndLine -gt 0) { [Math]::Min($EndLine - 1, $lines.Count - 1) } else { $startIndex }

    $snippet = ($lines[$startIndex..$endIndex] | ForEach-Object { $_.TrimEnd() }) -join "`n"
    return Get-HashString -Value "$SymbolId|$snippet"
}

function Get-ComplexityResultDetails {
    param(
        [string]$Message,
        [int]$ConfiguredThreshold,
        [string]$FallbackFilePath,
        [int]$FallbackLine
    )

    $symbolId = "{0}:{1}" -f $FallbackFilePath, $FallbackLine
    $complexity = $null

    if ($Message -match "'(?<symbol>[^']+)' has a cyclomatic complexity of '(?<complexity>\d+)'") {
        $symbolId = $matches.symbol
        $complexity = [int]$matches.complexity
    }

    return [PSCustomObject]@{
        SymbolId = $symbolId
        MeasuredComplexity = $complexity
        Threshold = $ConfiguredThreshold
    }
}

function Get-ComplexityFindingsFromSarif {
    param(
        [string[]]$SarifPaths,
        [string]$RepoRoot,
        [int]$ConfiguredThreshold
    )

    $findings = @()

    foreach ($sarifPath in $SarifPaths) {
        if (-not (Test-Path $sarifPath -PathType Leaf)) {
            continue
        }

        $content = Get-Content -Path $sarifPath -Raw | ConvertFrom-Json -Depth 100
        foreach ($run in $content.runs) {
            foreach ($result in $run.results) {
                if ($result.ruleId -ne 'CA1502') {
                    continue
                }

                if (-not $result.locations -or $result.locations.Count -eq 0) {
                    continue
                }

                $location = $result.locations[0].resultFile
                $uri = $location.uri
                $absolutePath = if ($uri -like 'file:///*') {
                    ([System.Uri]$uri).LocalPath
                }
                else {
                    Join-Path $RepoRoot $uri
                }

                $relativePath = if (Test-Path $absolutePath -PathType Leaf) {
                    Get-RelativeRepoPath -RepoRoot $RepoRoot -AbsolutePath $absolutePath
                }
                else {
                    $absolutePath.Replace($RepoRoot.TrimEnd('\') + '\', '')
                }

                $startLine = [int]$location.region.startLine
                $endLine = if ($location.region.endLine) { [int]$location.region.endLine } else { $startLine }
                $details = Get-ComplexityResultDetails -Message $result.message -ConfiguredThreshold $ConfiguredThreshold -FallbackFilePath $relativePath -FallbackLine $startLine
                $fingerprint = Get-ComplexityFingerprint -FilePath $absolutePath -StartLine $startLine -EndLine $endLine -SymbolId $details.SymbolId
                $projectName = [System.IO.Path]::GetFileNameWithoutExtension($sarifPath)

                $findings += [PSCustomObject]@{
                    Project = $projectName
                    RuleId = 'CA1502'
                    SymbolId = $details.SymbolId
                    FilePath = $relativePath
                    AbsolutePath = $absolutePath
                    Line = $startLine
                    EndLine = $endLine
                    MeasuredComplexity = $details.MeasuredComplexity
                    Threshold = $details.Threshold
                    Fingerprint = $fingerprint
                    Message = $result.message
                }
            }
        }
    }

    return $findings
}

function Get-ComplexityExceptionDocument {
    param(
        [string]$ExceptionsPath,
        [int]$Threshold
    )

    if (-not (Test-Path $ExceptionsPath -PathType Leaf)) {
        return [PSCustomObject]@{
            version = 1
            ruleId = 'CA1502'
            threshold = $Threshold
            entries = @()
        }
    }

    return Get-Content -Path $ExceptionsPath -Raw | ConvertFrom-Json -Depth 100
}

function Get-ComplexityEvaluation {
    param(
        [object[]]$Findings,
        [pscustomobject]$ExceptionDocument
    )

    $evaluated = @()
    $entries = @($ExceptionDocument.entries)

    foreach ($finding in $Findings) {
        $entry = $entries | Where-Object {
            $_.symbolId -eq $finding.SymbolId -and
            $_.filePath -eq $finding.FilePath -and
            $_.fingerprint -eq $finding.Fingerprint
        } | Select-Object -First 1

        $state = 'new'
        if ($entry) {
            $state = if ($entry.entryType -eq 'approved-exception') { 'approved-exception' } else { 'grandfathered' }
        }

        $evaluated += [PSCustomObject]@{
            project = $finding.Project
            ruleId = $finding.RuleId
            symbolId = $finding.SymbolId
            filePath = $finding.FilePath
            line = $finding.Line
            endLine = $finding.EndLine
            measuredComplexity = $finding.MeasuredComplexity
            threshold = $finding.Threshold
            fingerprint = $finding.Fingerprint
            state = $state
            message = $finding.Message
        }
    }

    $unsanctioned = @($evaluated | Where-Object { $_.state -eq 'new' })
    return [PSCustomObject]@{
        result = if ($unsanctioned.Count -gt 0) { 'FAIL' } else { 'PASS' }
        threshold = $ExceptionDocument.threshold
        findingCount = $evaluated.Count
        unsanctionedCount = $unsanctioned.Count
        grandfatheredCount = @($evaluated | Where-Object { $_.state -eq 'grandfathered' }).Count
        approvedExceptionCount = @($evaluated | Where-Object { $_.state -eq 'approved-exception' }).Count
        findings = $evaluated
    }
}

function Resolve-ComplexityPath {
    param(
        [string]$RepoRoot,
        [string]$Path
    )

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return $RepoRoot
    }

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return $Path
    }

    return Join-Path $RepoRoot ($Path -replace '^[.][\\/]', '')
}