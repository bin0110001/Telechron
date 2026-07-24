using Telechron.Agent.Containers;
using Telechron.Agent.Dispatch;
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

builder.Services.AddTelechronContainerExecution(
    configureConnection: o =>
    {
        o.Endpoint = builder.Configuration["Telechron:Podman:Endpoint"]
            ?? Environment.GetEnvironmentVariable("TELECHRON_PODMAN_ENDPOINT") ?? o.Endpoint;
    },
    configureAllowlist: o =>
    {
        var configured = builder.Configuration.GetSection("Telechron:Containers:AllowedRegistries").Get<string[]>();
        if (configured is { Length: > 0 })
            o.AllowedRegistries = configured;
    });

builder.Services.Configure<ModuleSelfTestHarnessOptions>(o =>
{
    o.HarnessPublishDirectory = builder.Configuration["Telechron:ModuleSelfTestHarnessPublishDirectory"]
        ?? Environment.GetEnvironmentVariable("TELECHRON_MODULE_SELFTEST_HARNESS_DIR") ?? o.HarnessPublishDirectory;
});
builder.Services.AddSingleton<ArtifactFetcher>();
builder.Services.AddSingleton<ArtifactUploader>();
builder.Services.AddSingleton<ModuleSelfTestHarnessLocator>();
builder.Services.AddSingleton<ICommandHandler, RunModuleSelfTestCommandHandler>();
builder.Services.AddSingleton<ICommandHandler, RunRepairVerifyCommandHandler>();
builder.Services.AddSingleton<ICommandHandler, RunCapabilitySynthesisBuildCommandHandler>();
builder.Services.AddSingleton<CommandHandlerRegistry>();

var host = builder.Build();
host.Run();
