using Telechron.Agent.Grpc;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<AgentGrpcOptions>(o =>
{
    o.HostAddress = builder.Configuration["Telechron:HostAddress"]
        ?? Environment.GetEnvironmentVariable("TELECHRON_HOST_ADDRESS") ?? "https://localhost:5300";
    o.CaCertPath = builder.Configuration["Telechron:Mtls:CaCertPath"]
        ?? Environment.GetEnvironmentVariable("TELECHRON_MTLS_CA_PATH") ?? string.Empty;
    o.ClientCertPfxPath = builder.Configuration["Telechron:Mtls:ClientCertPfxPath"]
        ?? Environment.GetEnvironmentVariable("TELECHRON_MTLS_AGENT_CERT_PATH") ?? string.Empty;
    o.ClientCertPassword = builder.Configuration["Telechron:Mtls:ClientCertPassword"]
        ?? Environment.GetEnvironmentVariable("TELECHRON_MTLS_AGENT_CERT_PASSWORD") ?? string.Empty;
    o.EnrollmentToken = builder.Configuration["Telechron:AgentEnrollmentToken"]
        ?? Environment.GetEnvironmentVariable("TELECHRON_AGENT_ENROLLMENT_TOKEN") ?? string.Empty;
    o.MachineName = builder.Configuration["Telechron:MachineName"] ?? Environment.MachineName;
});

builder.Services.AddSingleton<AgentChannelFactory>();
builder.Services.AddHostedService<AgentConnectionWorker>();

var host = builder.Build();
host.Run();
