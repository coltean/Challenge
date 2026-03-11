using FluentAssertions;

namespace Challenge.Tests.CodeQuality;

public sealed class ComplexityEnforcementWorkflowTests
{
    [Fact]
    public void ValidateScript_ShouldFailWhenUnsanctionedComplexityExists()
    {
        var sarifPath = ComplexityValidationTestSupport.MaterializeSarifFixture();
        var exceptionsPath = ComplexityValidationTestSupport.CreateExceptionsFile("workflow-empty.json");

        var result = ComplexityValidationTestSupport.RunValidateScript(sarifPath, exceptionsPath);

        result.ExitCode.Should().Be(2);

        var payload = ComplexityValidationTestSupport.ParseValidationResult(result.StdOut);
        payload.Result.Should().Be("FAIL");
        payload.UnsanctionedCount.Should().Be(2);
    }

    [Fact]
    public void ExportScript_ShouldCreateGrandfatheredBaselineEntries()
    {
        var sarifPath = ComplexityValidationTestSupport.MaterializeSarifFixture("baseline-fixture.sarif");
        var outputPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "complexity-exceptions.json");

        var result = ComplexityValidationTestSupport.RunExportScript(sarifPath, outputPath);

        result.ExitCode.Should().Be(0);

        var document = ComplexityValidationTestSupport.ParseExceptionDocument(outputPath);
        document.RuleId.Should().Be("CA1502");
        document.Threshold.Should().Be(15);
        document.Entries.Should().HaveCount(2);
        document.Entries.Should().OnlyContain(entry => entry.EntryType == "grandfathered");
    }
}