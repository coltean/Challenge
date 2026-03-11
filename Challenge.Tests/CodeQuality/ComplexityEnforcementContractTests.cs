using FluentAssertions;

namespace Challenge.Tests.CodeQuality;

public sealed class ComplexityEnforcementContractTests
{
    [Fact]
    public void ValidateScript_ShouldIgnoreNonCa1502ResultsAndClassifyGrandfatheredEntries()
    {
        var sarifPath = ComplexityValidationTestSupport.MaterializeSarifFixture();
        var baseline = ComplexityValidationTestSupport.ExportBaselineDocument(sarifPath);
        var exceptionsPath = ComplexityValidationTestSupport.CreateExceptionsFile(
            "exceptions.json",
            new ComplexityExceptionDocument(
                baseline.Version,
                baseline.RuleId,
                baseline.Threshold,
                new[] { baseline.Entries[0] }));

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