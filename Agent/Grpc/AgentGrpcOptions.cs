namespace Telechron.Agent.Grpc;

public sealed class AgentGrpcOptions
{
    public string HostAddress { get; set; } = "https://localhost:5300";
    public string CaCertPath { get; set; } = string.Empty;
    public string ClientCertPfxPath { get; set; } = string.Empty;
    public string ClientCertPassword { get; set; } = string.Empty;
    public string EnrollmentToken { get; set; } = string.Empty;
    public string MachineName { get; set; } = Environment.MachineName;
}
