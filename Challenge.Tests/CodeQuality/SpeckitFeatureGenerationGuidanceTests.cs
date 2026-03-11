using FluentAssertions;

namespace Challenge.Tests.CodeQuality;

public sealed class SpeckitFeatureGenerationGuidanceTests
{
    [Fact]
    public void GeneratedFeatureArtifacts_ShouldCarryTheComplexityStandard()
    {
        var specPath = Path.Combine(ComplexityValidationTestSupport.RepoRoot, "specs", "001-complexity-standard", "spec.md");
        var planPath = Path.Combine(ComplexityValidationTestSupport.RepoRoot, "specs", "001-complexity-standard", "plan.md");
        var tasksPath = Path.Combine(ComplexityValidationTestSupport.RepoRoot, "specs", "001-complexity-standard", "tasks.md");

        File.ReadAllText(specPath).Should().ContainEquivalentOf("cyclomatic complexity");
        File.ReadAllText(planPath).Should().ContainEquivalentOf("CA1502");
        File.ReadAllText(tasksPath).Should().ContainEquivalentOf("cyclomatic complexity 15");
    }
}