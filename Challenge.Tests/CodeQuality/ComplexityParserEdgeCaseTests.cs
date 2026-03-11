using FluentAssertions;

namespace Challenge.Tests.CodeQuality;

public sealed class ComplexityParserEdgeCaseTests
{
    [Fact]
    public void ValidateScript_ShouldPassWhenSarifContainsNoCa1502Results()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var sarifPath = Path.Combine(directory, "empty.sarif");
        File.WriteAllText(
            sarifPath,
            """
            {
              "$schema": "http://json.schemastore.org/sarif-1.0.0",
              "version": "1.0.0",
              "runs": [
                {
                  "tool": { "name": "Compiler" },
                  "results": [
                    {
                      "ruleId": "CA2254",
                      "level": "note",
                      "message": "Other analyzer",
                      "locations": []
                    }
                  ]
                }
              ]
            }
            """);

        var exceptionsPath = ComplexityValidationTestSupport.CreateExceptionsFile("no-ca1502.json");
        var result = ComplexityValidationTestSupport.RunValidateScript(sarifPath, exceptionsPath);

        result.ExitCode.Should().Be(0);

        var payload = ComplexityValidationTestSupport.ParseValidationResult(result.StdOut);
        payload.FindingCount.Should().Be(0);
        payload.Result.Should().Be("PASS");
    }
}