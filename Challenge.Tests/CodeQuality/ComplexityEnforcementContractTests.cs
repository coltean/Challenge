using FluentAssertions;

namespace Challenge.Tests.CodeQuality;

public sealed class ComplexityEnforcementContractTests
{
    [Fact]
    public void ValidateScript_ShouldIgnoreNonCa1502ResultsAndClassifyGrandfatheredEntries()
    {
        var sarifPath = ComplexityValidationTestSupport.MaterializeSarifFixture();
        var fingerprint = ComplexityValidationTestSupport.ComputeFixtureFingerprint(
            "Challenge.Tests/CodeQuality/Fixtures/Sources/ComplexitySamples.cs",
            5,
            25,
            "Challenge.Tests.CodeQuality.Fixtures.Sources.ComplexitySamples.LegacyHighComplexity(int)");

        var exceptionsPath = ComplexityValidationTestSupport.CreateExceptionsFile(
            "exceptions.json",
            new ComplexityExceptionEntry(
                "grandfathered",
                "Challenge.Tests",
                "Challenge.Tests.CodeQuality.Fixtures.Sources.ComplexitySamples.LegacyHighComplexity(int)",
                "Challenge.Tests\\CodeQuality\\Fixtures\\Sources\\ComplexitySamples.cs",
                fingerprint,
                "Grandfathered during adoption",
                "2026-03-11",
                "initial-baseline",
                null));

        var result = ComplexityValidationTestSupport.RunValidateScript(sarifPath, exceptionsPath);

        result.ExitCode.Should().Be(2);

        var payload = ComplexityValidationTestSupport.ParseValidationResult(result.StdOut);
        payload.FindingCount.Should().Be(2);
        payload.GrandfatheredCount.Should().Be(1);
        payload.UnsanctionedCount.Should().Be(1);
        payload.Findings.Should().OnlyContain(finding => finding.RuleId == "CA1502");
    }

    [Fact]
    public void ValidateScript_ShouldReturnSymbolAndFileDetailsForUnsanctionedFindings()
    {
        var sarifPath = ComplexityValidationTestSupport.MaterializeSarifFixture();
        var exceptionsPath = ComplexityValidationTestSupport.CreateExceptionsFile("empty-exceptions.json");

        var result = ComplexityValidationTestSupport.RunValidateScript(sarifPath, exceptionsPath);
        var payload = ComplexityValidationTestSupport.ParseValidationResult(result.StdOut);

        payload.Result.Should().Be("FAIL");
        payload.Findings.Should().ContainSingle(finding =>
            finding.SymbolId.EndsWith("NewHighComplexity(int)", StringComparison.Ordinal) &&
            finding.FilePath.EndsWith("ComplexitySamples.cs", StringComparison.Ordinal));
    }
}