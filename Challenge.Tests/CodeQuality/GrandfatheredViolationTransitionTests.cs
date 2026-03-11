using FluentAssertions;

namespace Challenge.Tests.CodeQuality;

public sealed class GrandfatheredViolationTransitionTests
{
    [Fact]
    public void ValidateScript_ShouldReopenGrandfatheredViolationWhenFingerprintChanges()
    {
        var sarifPath = ComplexityValidationTestSupport.MaterializeSarifFixture();
        var wrongFingerprint = new string('a', 64);

        var exceptionsPath = ComplexityValidationTestSupport.CreateExceptionsFile(
            "stale-grandfathered.json",
            new ComplexityExceptionEntry(
                "grandfathered",
                "Challenge.Tests",
                "Challenge.Tests.CodeQuality.Fixtures.Sources.ComplexitySamples.LegacyHighComplexity(int)",
                "Challenge.Tests\\CodeQuality\\Fixtures\\Sources\\ComplexitySamples.cs",
                wrongFingerprint,
                "Grandfathered during adoption",
                "2026-03-11",
                "initial-baseline",
                null));

        var result = ComplexityValidationTestSupport.RunValidateScript(sarifPath, exceptionsPath);

        result.ExitCode.Should().Be(2);

        var payload = ComplexityValidationTestSupport.ParseValidationResult(result.StdOut);
        payload.GrandfatheredCount.Should().Be(0);
        payload.UnsanctionedCount.Should().Be(2);
    }
}