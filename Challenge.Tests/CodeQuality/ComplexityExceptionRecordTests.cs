using FluentAssertions;

namespace Challenge.Tests.CodeQuality;

public sealed class ComplexityExceptionRecordTests
{
    [Fact]
    public void ValidateScript_ShouldTreatApprovedExceptionsAsSanctioned()
    {
        var sarifPath = ComplexityValidationTestSupport.MaterializeSarifFixture();
        var legacyFingerprint = ComplexityValidationTestSupport.ComputeFixtureFingerprint(
            "Challenge.Tests/CodeQuality/Fixtures/Sources/ComplexitySamples.cs",
            5,
            25,
            "Challenge.Tests.CodeQuality.Fixtures.Sources.ComplexitySamples.LegacyHighComplexity(int)");
        var newFingerprint = ComplexityValidationTestSupport.ComputeFixtureFingerprint(
            "Challenge.Tests/CodeQuality/Fixtures/Sources/ComplexitySamples.cs",
            27,
            47,
            "Challenge.Tests.CodeQuality.Fixtures.Sources.ComplexitySamples.NewHighComplexity(int)");

        var exceptionsPath = ComplexityValidationTestSupport.CreateExceptionsFile(
            "approved-exception.json",
            new ComplexityExceptionEntry(
                "grandfathered",
                "Challenge.Tests",
                "Challenge.Tests.CodeQuality.Fixtures.Sources.ComplexitySamples.LegacyHighComplexity(int)",
                "Challenge.Tests\\CodeQuality\\Fixtures\\Sources\\ComplexitySamples.cs",
                legacyFingerprint,
                "Grandfathered during adoption",
                "2026-03-11",
                "initial-baseline",
                null),
            new ComplexityExceptionEntry(
                "approved-exception",
                "Challenge.Tests",
                "Challenge.Tests.CodeQuality.Fixtures.Sources.ComplexitySamples.NewHighComplexity(int)",
                "Challenge.Tests\\CodeQuality\\Fixtures\\Sources\\ComplexitySamples.cs",
                newFingerprint,
                "Switch-heavy logic accepted temporarily",
                "2026-03-11",
                "PR-123",
                null));

        var result = ComplexityValidationTestSupport.RunValidateScript(sarifPath, exceptionsPath);

        result.ExitCode.Should().Be(0);

        var payload = ComplexityValidationTestSupport.ParseValidationResult(result.StdOut);
        payload.Result.Should().Be("PASS");
        payload.ApprovedExceptionCount.Should().Be(1);
    }
}