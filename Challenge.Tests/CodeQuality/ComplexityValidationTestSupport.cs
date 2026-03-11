using System.Diagnostics;
using System.Text.Json;

namespace Challenge.Tests.CodeQuality;

internal static class ComplexityValidationTestSupport
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static string RepoRoot { get; } = GetRepoRoot();

    public static string FixturesRoot => Path.Combine(RepoRoot, "Challenge.Tests", "CodeQuality", "Fixtures");

    public static string OutputFixturesRoot => Path.Combine(AppContext.BaseDirectory, "CodeQuality", "Fixtures");

    public static string ScriptsRoot => Path.Combine(RepoRoot, ".specify", "scripts", "powershell");

    public static string MaterializeSarifFixture(string targetFileName = "fixture.sarif")
    {
        var templatePath = ResolveFixturePath("ca1502-findings.sarif");
        var outputPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), targetFileName);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        var rootUri = new Uri(RepoRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar).AbsoluteUri.TrimEnd('/');
        var content = File.ReadAllText(templatePath).Replace("__ROOT__", rootUri);
        File.WriteAllText(outputPath, content);
        return outputPath;
    }

    public static string CreateExceptionsFile(string fileName, params ComplexityExceptionEntry[] entries)
    {
        var outputPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        var document = new ComplexityExceptionDocument(
            1,
            "CA1502",
            15,
            entries);

        File.WriteAllText(outputPath, JsonSerializer.Serialize(document, JsonOptions));
        return outputPath;
    }

    public static string ComputeFixtureFingerprint(string relativeSourcePath, int startLine, int endLine, string symbolId)
    {
        var relativeSegments = relativeSourcePath.Replace('/', Path.DirectorySeparatorChar);
        var absolutePath = Path.Combine(RepoRoot, relativeSegments);
        if (!File.Exists(absolutePath))
        {
            var fixtureRelativePath = relativeSegments.StartsWith($"Challenge.Tests{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                ? relativeSegments[$"Challenge.Tests{Path.DirectorySeparatorChar}".Length..]
                : relativeSegments;
            absolutePath = Path.Combine(AppContext.BaseDirectory, fixtureRelativePath);
        }

        var lines = File.ReadAllLines(absolutePath);
        var startIndex = Math.Max(startLine - 1, 0);
        var endIndex = Math.Min(Math.Max(endLine - 1, startIndex), lines.Length - 1);

        var snippet = string.Join(
            "\n",
            lines[startIndex..(endIndex + 1)].Select(static line => line.TrimEnd()));

        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes($"{symbolId}|{snippet}");
        var hash = sha.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static ScriptRunResult RunValidateScript(string sarifPath, string exceptionsPath)
    {
        return RunScript(
            "validate-complexity.ps1",
            $"-SkipBuild -SarifPath \"{sarifPath}\" -ExceptionsPath \"{exceptionsPath}\" -OutputJson");
    }

    public static ScriptRunResult RunExportScript(string sarifPath, string outputPath)
    {
        return RunScript(
            "export-complexity-baseline.ps1",
            $"-SkipBuild -SarifPath \"{sarifPath}\" -OutputPath \"{outputPath}\" -Force -OutputJson");
    }

    public static ComplexityValidationResult ParseValidationResult(string stdout)
    {
        return JsonSerializer.Deserialize<ComplexityValidationResult>(stdout, JsonOptions)
            ?? throw new InvalidOperationException("Validation result could not be parsed.");
    }

    public static ComplexityExceptionDocument ParseExceptionDocument(string path)
    {
        return JsonSerializer.Deserialize<ComplexityExceptionDocument>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidOperationException("Exception document could not be parsed.");
    }

    private static string ResolveFixturePath(string fileName)
    {
        var repoPath = Path.Combine(FixturesRoot, fileName);
        if (File.Exists(repoPath))
        {
            return repoPath;
        }

        var outputPath = Path.Combine(OutputFixturesRoot, fileName);
        if (File.Exists(outputPath))
        {
            return outputPath;
        }

        throw new FileNotFoundException($"Could not locate fixture '{fileName}' in repository or test output.", repoPath);
    }

    private static ScriptRunResult RunScript(string scriptName, string arguments)
    {
        var scriptPath = Path.Combine(ScriptsRoot, scriptName);
        var psi = new ProcessStartInfo
        {
            FileName = "pwsh",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" {arguments}",
            WorkingDirectory = RepoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("PowerShell process could not be started.");
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return new ScriptRunResult(process.ExitCode, stdout.Trim(), stderr.Trim());
    }

    private static string GetRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Challenge.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}

internal sealed record ScriptRunResult(int ExitCode, string StdOut, string StdErr);

internal sealed record ComplexityValidationResult(
    string Result,
    int Threshold,
    int FindingCount,
    int UnsanctionedCount,
    int GrandfatheredCount,
    int ApprovedExceptionCount,
    IReadOnlyList<ComplexityFindingResult> Findings);

internal sealed record ComplexityFindingResult(
    string Project,
    string RuleId,
    string SymbolId,
    string FilePath,
    int Line,
    int EndLine,
    int? MeasuredComplexity,
    int Threshold,
    string Fingerprint,
    string State,
    string Message);

internal sealed record ComplexityExceptionDocument(
    int Version,
    string RuleId,
    int Threshold,
    IReadOnlyList<ComplexityExceptionEntry> Entries);

internal sealed record ComplexityExceptionEntry(
    string EntryType,
    string Project,
    string SymbolId,
    string FilePath,
    string Fingerprint,
    string Justification,
    string CreatedOn,
    string ReviewReference,
    string? ExpiresOn);