using Microsoft.Extensions.DependencyInjection;
using Telechron.Host.Persistence.Tests.Fixtures;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Persistence;

namespace Telechron.Host.Persistence.Tests.Phase3;

public sealed class NoDependencyEntityTests : IAsyncLifetime
{
    private SqliteTestDatabase _db = null!;

    public Task InitializeAsync()
    {
        _db = new SqliteTestDatabase();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _db.DisposeAsync();

    [Fact]
    public async Task Machine_RoundTrips()
    {
        var machine = new Machine
        {
            Id = Guid.NewGuid(),
            Name = "Build Box",
            Hostname = "build-box.local",
            RegisteredAtUtc = DateTimeOffset.UtcNow,
            IsOnline = true,
        };

        using (var scope = _db.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IMachineRepository>().AddAsync(machine);

        using var verifyScope = _db.CreateScope();
        var repo = verifyScope.ServiceProvider.GetRequiredService<IMachineRepository>();
        var loaded = await repo.GetByIdAsync(machine.Id);

        Assert.NotNull(loaded);
        Assert.Equal(machine.Hostname, loaded.Hostname);
        Assert.True(loaded.IsOnline);

        var online = await repo.GetOnlineAsync();
        Assert.Contains(online, m => m.Id == machine.Id);
    }

    [Fact]
    public async Task LlmConnection_RoundTrips()
    {
        var connection = new LlmConnection
        {
            Id = Guid.NewGuid(),
            Name = "Claude Prod",
            Provider = "Claude",
            ConfigurationJson = """{"model":"claude-sonnet-5"}""",
            SecretHandle = "sec_abc123",
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

        using (var scope = _db.CreateScope())
            await scope.ServiceProvider.GetRequiredService<ILlmConnectionRepository>().AddAsync(connection);

        using var verifyScope = _db.CreateScope();
        var loaded = await verifyScope.ServiceProvider.GetRequiredService<ILlmConnectionRepository>().GetByIdAsync(connection.Id);

        Assert.NotNull(loaded);
        Assert.Equal("Claude", loaded.Provider);
        Assert.Equal("sec_abc123", loaded.SecretHandle);
    }

    [Fact]
    public async Task Toolchain_RoundTrips()
    {
        var toolchain = new Toolchain
        {
            Id = Guid.NewGuid(),
            Name = ".NET",
            ModuleId = Guid.NewGuid(),
            BuildCommand = "dotnet build",
            TestCommand = "dotnet test",
            VerifyCommand = "dotnet build --configuration Release",
            ExportCommand = null,
            DeployCommand = null,
            EnvironmentRequirementsJson = """{"sdk":"10.0"}""",
        };

        using (var scope = _db.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IToolchainRepository>().AddAsync(toolchain);

        using var verifyScope = _db.CreateScope();
        var loaded = await verifyScope.ServiceProvider.GetRequiredService<IToolchainRepository>().GetByIdAsync(toolchain.Id);

        Assert.NotNull(loaded);
        Assert.Equal("dotnet build", loaded.BuildCommand);
        Assert.Null(loaded.ExportCommand);
    }

    [Fact]
    public async Task Module_RoundTrips_AndGetByName()
    {
        var module = new Module
        {
            Id = Guid.NewGuid(),
            Name = "dotnet-toolchain",
            Kind = "Assembly",
            VersionMajor = 1,
            VersionMinor = 2,
            VersionPatch = 3,
            CapabilitiesJson = """["ProcessExecution"]""",
            TestCommand = "self-test.sh",
            SourceCodeRef = "modules/dotnet-toolchain/src",
            InstalledAtUtc = DateTimeOffset.UtcNow,
        };

        using (var scope = _db.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IModuleRepository>().AddAsync(module);

        using var verifyScope = _db.CreateScope();
        var repo = verifyScope.ServiceProvider.GetRequiredService<IModuleRepository>();
        var loaded = await repo.GetByIdAsync(module.Id);

        Assert.NotNull(loaded);
        Assert.Equal(1, loaded.VersionMajor);

        var byName = await repo.GetByNameAsync("dotnet-toolchain");
        Assert.NotNull(byName);
        Assert.Equal(module.Id, byName.Id);
    }

    [Fact]
    public async Task Function_RoundTrips_AndGetByModule()
    {
        var moduleId = Guid.NewGuid();
        var function = new Function
        {
            Id = Guid.NewGuid(),
            Name = "RunTests",
            ModuleId = moduleId,
            Kind = "Run",
            InputArtifactTypesJson = "[]",
            OutputArtifactTypesJson = """["Report"]""",
            IsDeprecated = false,
            ModuleVersionMajor = 1,
            ModuleVersionMinor = 0,
        };

        using (var scope = _db.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IFunctionRepository>().AddAsync(function);

        using var verifyScope = _db.CreateScope();
        var repo = verifyScope.ServiceProvider.GetRequiredService<IFunctionRepository>();
        var loaded = await repo.GetByIdAsync(function.Id);

        Assert.NotNull(loaded);
        Assert.False(loaded.IsDeprecated);

        var byModule = await repo.GetByModuleAsync(moduleId);
        Assert.Contains(byModule, f => f.Id == function.Id);
    }

    [Fact]
    public async Task Connector_RoundTrips_GlobalAndProjectScoped()
    {
        var globalConnector = new Connector
        {
            Id = Guid.NewGuid(),
            Name = "GitHub",
            ModuleId = Guid.NewGuid(),
            Kind = "GitHub",
            ConfigurationJson = "{}",
            SecretHandle = "sec_gh_pat",
            IsDeprecated = false,
            ProjectId = null,
        };

        using (var scope = _db.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IConnectorRepository>().AddAsync(globalConnector);

        using var verifyScope = _db.CreateScope();
        var repo = verifyScope.ServiceProvider.GetRequiredService<IConnectorRepository>();
        var loaded = await repo.GetByIdAsync(globalConnector.Id);

        Assert.NotNull(loaded);
        Assert.Null(loaded.ProjectId);

        var globalConnectors = await repo.GetByProjectAsync(null);
        Assert.Contains(globalConnectors, c => c.Id == globalConnector.Id);
    }

    [Fact]
    public async Task Resource_RoundTrips_WithExclusiveGroup()
    {
        Guid machineId;
        using (var scope = _db.CreateScope())
            machineId = await scope.SeedMachineAsync();

        var resource = new Resource
        {
            Id = Guid.NewGuid(),
            MachineId = machineId,
            Kind = "gpu",
            Name = "GPU 0",
            ExclusiveGroup = "gpu-pool",
        };

        using (var scope = _db.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IResourceRepository>().AddAsync(resource);

        using var verifyScope = _db.CreateScope();
        var repo = verifyScope.ServiceProvider.GetRequiredService<IResourceRepository>();
        var loaded = await repo.GetByIdAsync(resource.Id);

        Assert.NotNull(loaded);
        Assert.Equal("gpu-pool", loaded.ExclusiveGroup);

        var byMachine = await repo.GetByMachineAsync(machineId);
        Assert.Contains(byMachine, r => r.Id == resource.Id);

        var byGroup = await repo.GetByExclusiveGroupAsync("gpu-pool");
        Assert.Contains(byGroup, r => r.Id == resource.Id);
    }
}
