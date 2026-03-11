using FluentAssertions;

namespace Challenge.Tests.CodeQuality;

public sealed class ComplexityExceptionRecordTests
{
    [Fact]
    public void ValidateScript_ShouldTreatApprovedExceptionsAsSanctioned()
    {
        var sarifPath = ComplexityValidationTestSupport.MaterializeSarifFixture();
        var baseline = ComplexityValidationTestSupport.ExportBaselineDocument(sarifPath);
        var first = baseline.Entries[0];
        var second = baseline.Entries[1];
        var exceptionsPath = ComplexityValidationTestSupport.CreateExceptionsFile(
            "approved-exception.json",
            new ComplexityExceptionDocument(
                baseline.Version,
                baseline.RuleId,
                baseline.Threshold,
                new[]
                {
                    first,
                    new ComplexityExceptionEntry(
                        "approved-exception",
                        second.Project,
                        second.SymbolId,
                        second.FilePath,
                        second.Fingerprint,
                        "Switch-heavy logic accepted temporarily",
                        second.CreatedOn,
                        "PR-123",
                        second.ExpiresOn)
                }));

        var result = ComplexityValidationTestSupport.RunValidateScript(sarifPath, exceptionsPath);

        result.ExitCode.Should().Be(0);

        var payload = ComplexityValidationTestSupport.ParseValidationResult(result.StdOut);
        payload.Result.Should().Be("PASS");
        payload.ApprovedExceptionCount.Should().Be(1);
    }
}