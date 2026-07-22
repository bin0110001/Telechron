namespace Telechron.Sdk.Security.Permissions;

// R-MOD8: the capability taxonomy modules/Personas declare against. Extend
// deliberately — this is the vocabulary the mediation primitive (R-MOD8a)
// checks every dispatch against.
public enum CapabilityKind
{
    FilesystemRead = 0,
    FilesystemWrite = 1,
    InternetAccess = 2,
    GitAccess = 3,
    ProcessExecution = 4,
    ConnectorAccess = 5,
    GpuAccess = 6,
    SecretAccess = 7,
    DeploymentAccess = 8,
    LlmAccess = 9,
    ToolInvocation = 10,
    WorkflowInvocation = 11,
}
