using FluentAssertions;

namespace Challenge.Tests.CodeQuality;

public sealed class SpeckitPropagationTests
{
    [Theory]
    [InlineData(".specify/memory/constitution.md")]
    [InlineData(".specify/templates/spec-template.md")]
    [InlineData(".specify/templates/plan-template.md")]
    [InlineData(".specify/templates/tasks-template.md")]
    public void GuidanceArtifacts_ShouldReferenceCyclomaticComplexityThreshold(string relativePath)
    {
        var path = Path.Combine(ComplexityValidationTestSupport.RepoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));

        File.Exists(path).Should().BeTrue();
        var content = File.ReadAllText(path);

        content.Should().ContainEquivalentOf("cyclomatic complexity");
        content.Should().Contain("15");
    }
}