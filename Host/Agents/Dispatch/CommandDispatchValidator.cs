using System.Text.Json;
using Json.Schema;
using Telechron.Sdk.Agents;
using Telechron.Sdk.Security;

namespace Telechron.Host.Agents.Dispatch;

// R-SEC2: "All commands dispatched to Agents must pass strict schema
// validation and parameter escaping to prevent command injection." Every
// command kind has a fixed JSON Schema (additionalProperties: false, so an
// unexpected field is rejected rather than silently passed through); an
// unrecognized command_kind is rejected outright, never dispatched as
// "best effort."
public sealed class CommandDispatchValidator : ICommandDispatchValidator
{
    private static readonly IReadOnlyDictionary<string, JsonSchema> Schemas = new Dictionary<string, JsonSchema>
    {
        [CommandKinds.RunTests] = JsonSchema.FromText("""
            {
              "type": "object",
              "additionalProperties": false,
              "required": ["projectRootRelativePath", "toolchainName"],
              "properties": {
                "projectRootRelativePath": { "type": "string", "pattern": "^(?!.*\\.\\.)[A-Za-z0-9_][A-Za-z0-9_./-]*$", "maxLength": 512 },
                "toolchainName": { "type": "string", "pattern": "^[A-Za-z0-9_-]+$", "maxLength": 64 },
                "testFilter": { "type": "string", "maxLength": 256 }
              }
            }
            """),
        [CommandKinds.Build] = JsonSchema.FromText("""
            {
              "type": "object",
              "additionalProperties": false,
              "required": ["projectRootRelativePath", "toolchainName"],
              "properties": {
                "projectRootRelativePath": { "type": "string", "pattern": "^(?!.*\\.\\.)[A-Za-z0-9_][A-Za-z0-9_./-]*$", "maxLength": 512 },
                "toolchainName": { "type": "string", "pattern": "^[A-Za-z0-9_-]+$", "maxLength": 64 },
                "configuration": { "type": "string", "enum": ["Debug", "Release"] }
              }
            }
            """),
        [CommandKinds.ExecuteWorkflowStep] = JsonSchema.FromText("""
            {
              "type": "object",
              "additionalProperties": false,
              "required": ["workflowRunId", "stepIndex", "functionId"],
              "properties": {
                "workflowRunId": { "type": "string", "format": "uuid" },
                "stepIndex": { "type": "integer", "minimum": 0 },
                "functionId": { "type": "string", "format": "uuid" }
              }
            }
            """),
        [CommandKinds.ApplyRepairPatch] = JsonSchema.FromText("""
            {
              "type": "object",
              "additionalProperties": false,
              "required": ["repairAttemptId", "snapshotRef"],
              "properties": {
                "repairAttemptId": { "type": "string", "format": "uuid" },
                "snapshotRef": { "type": "string", "pattern": "^(?!.*\\.\\.)[A-Za-z0-9_][A-Za-z0-9_./-]*$", "maxLength": 512 }
              }
            }
            """),
        [CommandKinds.RunModuleSelfTest] = JsonSchema.FromText("""
            {
              "type": "object",
              "additionalProperties": false,
              "required": ["moduleName", "moduleAssemblyBlobRef", "toolchainImageDigest"],
              "properties": {
                "moduleName": { "type": "string", "pattern": "^[A-Za-z0-9_.-]+$", "maxLength": 128 },
                "moduleAssemblyBlobRef": { "type": "string", "pattern": "^(?!.*\\.\\.)[A-Za-z0-9_][A-Za-z0-9_./-]*$", "maxLength": 512 },
                "toolchainImageDigest": { "type": "string", "pattern": "^[A-Za-z0-9.\\-/]+@sha256:[A-Fa-f0-9]{64}$", "maxLength": 512 },
                "maximallyRestricted": { "type": "boolean" },
                "declaredCapabilities": {
                  "type": "array",
                  "items": { "type": "string", "pattern": "^[A-Za-z]+$", "maxLength": 64 },
                  "maxItems": 32
                }
              }
            }
            """),
        [CommandKinds.RunRepairVerify] = JsonSchema.FromText("""
            {
              "type": "object",
              "additionalProperties": false,
              "required": ["workspaceBlobRef", "toolchainImageDigest", "testCommand", "testRunnerKind"],
              "properties": {
                "workspaceBlobRef": { "type": "string", "pattern": "^(?!.*\\.\\.)[A-Za-z0-9_][A-Za-z0-9_./-]*$", "maxLength": 512 },
                "toolchainImageDigest": { "type": "string", "pattern": "^[A-Za-z0-9.\\-/]+@sha256:[A-Fa-f0-9]{64}$", "maxLength": 512 },
                "testCommand": { "type": "string", "maxLength": 1024 },
                "testRunnerKind": { "type": "string", "pattern": "^[A-Za-z0-9_-]+$", "maxLength": 64 },
                "environmentRequirements": {
                  "type": "object",
                  "additionalProperties": { "type": "string", "maxLength": 512 }
                }
              }
            }
            """),
        [CommandKinds.RunCapabilitySynthesisBuild] = JsonSchema.FromText("""
            {
              "type": "object",
              "additionalProperties": false,
              "required": ["sourceBundleBlobRef", "toolchainImageDigest", "moduleName"],
              "properties": {
                "sourceBundleBlobRef": { "type": "string", "pattern": "^(?!.*\\.\\.)[A-Za-z0-9_][A-Za-z0-9_./-]*$", "maxLength": 512 },
                "toolchainImageDigest": { "type": "string", "pattern": "^[A-Za-z0-9.\\-/]+@sha256:[A-Fa-f0-9]{64}$", "maxLength": 512 },
                "moduleName": { "type": "string", "pattern": "^[A-Za-z0-9_.-]+$", "maxLength": 128 }
              }
            }
            """),
    };

    public CommandValidationResult Validate(string commandKind, string parametersJson)
    {
        if (!Schemas.TryGetValue(commandKind, out var schema))
            return CommandValidationResult.Invalid($"Unrecognized command kind '{commandKind}'.");

        JsonElement element;
        try
        {
            element = JsonDocument.Parse(parametersJson).RootElement;
        }
        catch (JsonException ex)
        {
            return CommandValidationResult.Invalid($"Parameters are not valid JSON: {ex.Message}");
        }

        var result = schema.Evaluate(element, new EvaluationOptions { OutputFormat = OutputFormat.List });
        if (result.IsValid)
            return CommandValidationResult.Valid;

        var errors = (result.Details ?? [])
            .Where(d => !d.IsValid && d.Errors is { Count: > 0 })
            .SelectMany(d => d.Errors!.Values)
            .ToList();
        return CommandValidationResult.Invalid(errors.Count > 0 ? errors.ToArray() : ["Schema validation failed."]);
    }

    public string EscapeForShellArgument(string value)
    {
        // Single-quote the whole value and escape embedded single quotes by
        // closing the quote, emitting an escaped quote, and reopening —
        // the standard POSIX-shell-safe quoting pattern. Values reaching
        // this point have already passed a restrictive pattern-validated
        // schema (see Schemas above), so this is defense in depth, not the
        // only control.
        return "'" + value.Replace("'", "'\\''") + "'";
    }
}
