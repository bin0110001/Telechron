using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Telechron.Host.Agents;
using Telechron.Host.Agents.Grpc;
using Telechron.Host.Agents.Mtls;
using Telechron.Host.DesignDocuments;
using Telechron.Host.Health;
using Telechron.Host.Llm;
using Telechron.Host.Modules;
using Telechron.Host.Persistence;
using Telechron.Host.Reliability;
using Telechron.Host.Repair;
using Telechron.Host.Scheduling;
using Telechron.Host.Security.Audit;
using Telechron.Host.Security.Auth;
using Telechron.Host.Security.Logging;
using Telechron.Host.Security.Permissions;
using Telechron.Host.Security.Secrets;
using Telechron.Host.Storefront;
using Telechron.Host.Synthesis;
using Telechron.Host.Workflows;
using Telechron.Sdk.Security;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// R-SEC6: shared per-Project visibility check used by the frontend-facing
// REST controllers (Projects/Runs/DesignDocument/...).
builder.Services.AddScoped<Telechron.Host.Controllers.IProjectAccessChecker, Telechron.Host.Controllers.ProjectAccessChecker>();

var dataDirectory = builder.Configuration["Telechron:DataDirectory"]
    ?? Path.Combine(AppContext.BaseDirectory, "data");
Directory.CreateDirectory(dataDirectory);
var dbPath = Path.Combine(dataDirectory, "telechron.db");
var backupDirectory = Path.Combine(dataDirectory, "backups");
builder.Services.AddTelechronPersistence(dbPath, backupDirectory);
builder.Services.AddTelechronScheduledBackups();

// R-PER7: Artifact binary payloads live outside SQLite.
var artifactBlobDirectory = Path.Combine(dataDirectory, "artifacts");
builder.Services.AddTelechronArtifactBlobStore(artifactBlobDirectory);

// R-PER7: retention archival lands alongside artifacts — same out-of-SQLite filesystem.
var retentionArchiveDirectory = Path.Combine(dataDirectory, "retention-archive");
builder.Services.AddTelechronRetention(retentionArchiveDirectory);
builder.Services.AddTelechronScheduledRetention();

// R-SEC7: physically separate SQLite file from the operational DB above.
var auditDbPath = Path.Combine(dataDirectory, "telechron-audit.db");
builder.Services.AddTelechronAuditLog(auditDbPath);

builder.Services.AddTelechronSecretVault();

// R-SEC1 safety net: replace the default console provider with a
// redaction-wrapped one so log output is checked against currently in-scope
// secret values before reaching the console. The primary control is that raw
// secret values are never passed to ILogger by construction; this catches the
// accidental case (e.g. an exception message that embeds request content).
// ConsoleLoggerProvider itself is resolved from the same container (via its
// normal AddConsole registration below) so it participates in the app's
// actual DI graph rather than a throwaway nested one.
builder.Logging.ClearProviders();
builder.Services.Configure<ConsoleLoggerOptions>(_ => { });
builder.Services.AddSingleton<ConsoleLoggerProvider>();
builder.Services.AddSingleton<ILoggerProvider>(sp => new RedactingLoggerProvider(
    sp.GetRequiredService<ConsoleLoggerProvider>(),
    sp.GetRequiredService<ISecretFingerprintRegistry>()));

// R-SEC6: JWT bearer auth + per-Project RBAC for the human-facing REST API.
// Config-first (testable via WebApplicationFactory.UseSetting), env var as the
// normal deployment path (TELECHRON_JWT_SIGNING_KEY).
var jwtSigningKey = builder.Configuration["Telechron:JwtSigningKey"]
    ?? Environment.GetEnvironmentVariable("TELECHRON_JWT_SIGNING_KEY")
    ?? string.Empty;
builder.Services.AddTelechronApiAuth(new JwtOptions { SigningKey = jwtSigningKey });

// R-SEC6: one-time setup-token bootstrap for the first Admin User -- see
// SetupOptions/SetupController. No default token; unset means /api/setup
// is permanently disabled (a fresh deploy with no token configured cannot
// bootstrap itself, by design -- the operator must deliberately opt in).
var setupToken = builder.Configuration["Telechron:SetupToken"]
    ?? Environment.GetEnvironmentVariable("TELECHRON_SETUP_TOKEN");
builder.Services.Configure<SetupOptions>(o => o.SetupToken = setupToken);

// CORS restricted to explicitly configured origins (comma-separated); empty = deny all.
var allowedOrigins = (builder.Configuration["Telechron:AllowedOrigins"] ?? string.Empty)
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddTelechronCors(allowedOrigins);

builder.Services.AddTelechronRateLimiting();

builder.Services.AddTelechronPermissionMediation();

builder.Services.AddTelechronDesignDocuments();

// R-WF5/R-DM15: human approval bookkeeping has no Agent/mTLS dependency --
// registered unconditionally so the human-facing approval API always works,
// unlike AddTelechronWorkflows() below (needs IModuleRuntime, mTLS-gated).
builder.Services.AddTelechronApprovals();

// R-SCH1-5: SchedulerService/ResourceManager/PriorityQueue are pure
// in-memory bookkeeping with no Agent/mTLS dependency at construction time
// (SchedulerService only touches IWorkflowEngine, mTLS-gated, lazily inside
// TriggerScheduleAsync via IServiceScopeFactory) -- same reasoning as
// AddTelechronApprovals above. Registered unconditionally so
// SchedulingController's read endpoints work without Agent transport
// configured.
builder.Services.AddTelechronScheduling();

// R-SEC2/R-SCH3: Agent<->Host gRPC channel, authenticated by mTLS (transport)
// plus a per-Machine session token (application-level, see AgentServiceImpl).
var mtlsOptions = new MtlsOptions
{
    CaCertPath = builder.Configuration["Telechron:Mtls:CaCertPath"]
        ?? Environment.GetEnvironmentVariable("TELECHRON_MTLS_CA_PATH") ?? string.Empty,
    HostCertPfxPath = builder.Configuration["Telechron:Mtls:HostCertPfxPath"]
        ?? Environment.GetEnvironmentVariable("TELECHRON_MTLS_HOST_CERT_PATH") ?? string.Empty,
    HostCertPassword = builder.Configuration["Telechron:Mtls:HostCertPassword"]
        ?? Environment.GetEnvironmentVariable("TELECHRON_MTLS_HOST_CERT_PASSWORD") ?? string.Empty,
};
var enrollmentToken = builder.Configuration["Telechron:AgentEnrollmentToken"]
    ?? Environment.GetEnvironmentVariable("TELECHRON_AGENT_ENROLLMENT_TOKEN") ?? string.Empty;

var mtlsEnabled = !string.IsNullOrEmpty(mtlsOptions.CaCertPath);
if (mtlsEnabled)
{
    builder.Services.AddTelechronAgentMtls(mtlsOptions);
    builder.Services.AddTelechronAgentGrpc(enrollmentToken);
    builder.Services.AddTelechronStalledRunWatchdog();

    // R-MOD4a/R-MOD5a: module self-test dispatch needs IDispatchQueue/
    // ICommandResultCorrelator, which only exist once Agent gRPC is
    // registered above -- without an Agent transport there's nowhere to
    // dispatch a containerized self-test to.
    builder.Services.AddTelechronModules(o =>
    {
        var configuredKeys = builder.Configuration.GetSection("Telechron:Modules:TrustedPublisherKeys").Get<Dictionary<string, string>>();
        if (configuredKeys is { Count: > 0 })
            o.TrustedKeys = configuredKeys;
    });

    // R-LLM1/R-LLM4: the provider registry resolves engine modules via
    // IModuleRuntime, registered just above -- same dependency ordering
    // reason as AddTelechronModules itself.
    builder.Services.AddTelechronLlm(
        configureProviders: o =>
        {
            var configuredProviders = builder.Configuration.GetSection("Telechron:Llm:ProviderToModuleName").Get<Dictionary<string, string>>();
            if (configuredProviders is { Count: > 0 })
                o.ProviderToModuleName = configuredProviders;
        },
        configureSpendCaps: o =>
        {
            if (decimal.TryParse(builder.Configuration["Telechron:Llm:GlobalSpendCapUsd"], out var globalCap))
                o.GlobalCapUsd = globalCap;
        });

    // R-SYS5: Storefront, disabled by default -- see AddTelechronModules
    // comment above for why this needs the same Agent-gRPC precondition
    // (its R-MOD5b sandbox stage dispatches through IModuleTrustEvaluator,
    // which dispatches to a real Agent).
    builder.Services.AddTelechronStorefront(o =>
    {
        if (bool.TryParse(builder.Configuration["Telechron:Storefront:Enabled"], out var enabled))
            o.Enabled = enabled;
    });

    // R-WF1/R-WF4/R-WF5: workflow engine + durable-resume recovery.
    builder.Services.AddTelechronWorkflows();

    // R-NS2/R-FIX2/R-ENG4: the single repair pipeline's Project-independent
    // gates plus IRepairPipelineFactory (needs IModuleRuntime, registered
    // above, to resolve a Project's Toolchain/TestRunner).
    builder.Services.AddTelechronRepair();

    // R-REL3/R-REL4: Host Sentinel self-repair (needs IRepairPipelineFactory)
    // + scaling monitor (needs real agent/workflow-run repositories, already
    // available from AddTelechronPersistence).
    builder.Services.AddTelechronReliability();

    // R-BUILD1-5: intent planning + capability gap synthesis.
    builder.Services.AddTelechronSynthesis(o =>
    {
        var keyId = builder.Configuration["Telechron:Synthesis:IntegrityKeyId"];
        if (!string.IsNullOrEmpty(keyId))
            o.KeyId = keyId;
        var privateKey = builder.Configuration["Telechron:Synthesis:PrivateKeyPkcs8Base64"]
            ?? Environment.GetEnvironmentVariable("TELECHRON_SYNTHESIS_PRIVATE_KEY");
        if (!string.IsNullOrEmpty(privateKey))
            o.PrivateKeyPkcs8Base64 = privateKey;
    });

    // R-PER5: telemetry batching + periodic flush.
    builder.Services.AddSingleton<Telechron.Sdk.Telemetry.ITelemetryBatcher, Telechron.Host.Telemetry.TelemetryBatcher>();
    builder.Services.AddHostedService<Telechron.Host.Telemetry.TelemetryFlushHostedService>();

    // Kestrel's --urls/ASPNETCORE_URLS binding only applies when no endpoint
    // is configured via ConfigureKestrel — once we add the gRPC/mTLS
    // endpoint below, the human API endpoint must ALSO be declared here
    // explicitly, or it silently stops listening (Kestrel logs a warning,
    // not an error, so this is easy to miss).
    var humanApiPort = int.TryParse(builder.Configuration["Telechron:HumanApiPort"], out var configuredHumanApiPort)
        ? configuredHumanApiPort : 5280;
    var grpcPort = int.TryParse(builder.Configuration["Telechron:GrpcPort"], out var configuredGrpcPort)
        ? configuredGrpcPort : 5300;

    var trustedAgentCa = System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadCertificateFromFile(mtlsOptions.CaCertPath);
    builder.WebHost.ConfigureKestrel(kestrel =>
    {
        kestrel.ListenAnyIP(humanApiPort, listen =>
        {
            listen.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2;
        });
        kestrel.ListenAnyIP(grpcPort, listen =>
        {
            listen.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2;
            listen.UseHttps(https =>
            {
                https.ServerCertificate = System.Security.Cryptography.X509Certificates.X509CertificateLoader
                    .LoadPkcs12FromFile(mtlsOptions.HostCertPfxPath, mtlsOptions.HostCertPassword);
                https.ConfigureAgentMtlsTransport(trustedAgentCa);
            });
        });
    });
}

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TelechronDbContext>();
    db.Database.Migrate();

    var auditDb = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
    auditDb.Database.Migrate();
}

// R-DM16a: seed/refresh Telechron's own reflexive Design Document from
// TechDesign.md on every startup — idempotent, and best-effort (a missing
// source file logs a warning rather than failing Host startup, since e.g.
// test/deployment environments may not have the repo docs alongside them).
using (var scope = app.Services.CreateScope())
{
    var repoRoot = builder.Configuration["Telechron:RepoRoot"]
        ?? Directory.GetParent(AppContext.BaseDirectory)?.Parent?.Parent?.Parent?.Parent?.FullName;
    var techDesignPath = repoRoot is not null ? Path.Combine(repoRoot, "TechDesign.md") : null;

    if (techDesignPath is not null && File.Exists(techDesignPath))
    {
        var seeder = scope.ServiceProvider.GetRequiredService<ReflexiveDesignDocumentSeeder>();
        await seeder.SeedFromMarkdownAsync(await File.ReadAllTextAsync(techDesignPath), repoRoot!);
    }
    else
    {
        scope.ServiceProvider.GetRequiredService<ILogger<Program>>()
            .LogWarning("TechDesign.md not found at {Path}; skipping reflexive Design Document seeding (R-DM16a).", techDesignPath);
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// R-REL6: correlation ID established before anything else that might log
// or need it for the rest of the request pipeline.
app.UseMiddleware<Telechron.Host.Telemetry.CorrelationTracingMiddleware>();

app.UseCors(AuthServiceCollectionExtensions.CorsPolicyName);
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthEndpoints();

if (mtlsEnabled)
{
    app.MapGrpcService<AgentServiceImpl>();
}

app.Run();

// Test-visibility marker for WebApplicationFactory<Program> in integration tests.
public partial class Program;
