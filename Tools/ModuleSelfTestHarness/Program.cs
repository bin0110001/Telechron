using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using Telechron.Sdk.Modules;

// R-SYS6/R-MOD4: the actual module-code-executing process. This binary is
// what runs INSIDE the container the Agent creates for a RunModuleSelfTest
// command -- ModuleRuntime's ALC (Host/Agent process) is lifecycle-only
// per R-MOD7 and never runs untrusted module code; this harness, running
// as the container's own process under Podman's isolation, is where that
// actually happens.
if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: ModuleSelfTestHarness <module-assembly-path>");
    return 2;
}

var assemblyPath = args[0];
if (!File.Exists(assemblyPath))
{
    Console.Error.WriteLine($"Module assembly not found: {assemblyPath}");
    return 2;
}

try
{
    var resolver = new AssemblyDependencyResolver(assemblyPath);
    var context = new AssemblyLoadContext("module-self-test", isCollectible: true);
    context.Resolving += (_, name) =>
    {
        // Telechron.Sdk defines IModule -- must resolve to the SAME Type
        // object this harness's own `typeof(IModule)` uses (the harness's
        // Default-context copy), or IsAssignableFrom below silently fails
        // even though the module's type genuinely implements IModule. See
        // Host/Modules/Runtime/ModuleLoadContext.cs for the identical fix.
        if (name.Name == "Telechron.Sdk")
            return null;

        var path = resolver.ResolveAssemblyToPath(name);
        return path is not null ? context.LoadFromAssemblyPath(path) : null;
    };

    var assembly = context.LoadFromAssemblyPath(assemblyPath);
    var moduleType = assembly.GetTypes().FirstOrDefault(t =>
        typeof(IModule).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface);

    if (moduleType is null)
    {
        WriteResult(ModuleSelfTestResult.Failure($"Assembly does not contain a type implementing {nameof(IModule)}."));
        return 1;
    }

    if (Activator.CreateInstance(moduleType) is not IModule module)
    {
        WriteResult(ModuleSelfTestResult.Failure($"Type '{moduleType.FullName}' could not be instantiated as an {nameof(IModule)}."));
        return 1;
    }

    var result = await module.RunSelfTestAsync();
    WriteResult(result);
    return result.Passed ? 0 : 1;
}
catch (Exception ex)
{
    WriteResult(ModuleSelfTestResult.Failure("Unhandled exception running self-test.", ex.ToString()));
    return 1;
}

static void WriteResult(ModuleSelfTestResult result) =>
    Console.WriteLine(JsonSerializer.Serialize(result));
