namespace Telechron.Host.Synthesis;

using System.IO.Compression;
using System.Reflection;
using Telechron.Sdk.Synthesis;

// Assembles the zip a synthesis build dispatches to an Agent: the
// LLM-generated module source, its self-test as an xunit test project, a
// minimal .csproj for each referencing the Host's own compiled
// Telechron.Sdk.dll (not a ProjectReference to source the container
// doesn't have), and an xunit-runnable test project so `dotnet test`
// inside the container is a real, falsifiable check (R-MOD4a) rather than
// a hand-rolled pass/fail harness.
internal static class SynthesisBundleBuilder
{
    public static void WriteBundleZip(string zipPath, SynthesizedCapabilityResult synthesizedModule)
    {
        var stagingDir = Path.Combine(Path.GetTempPath(), $"telechron-synthesis-stage-{Guid.NewGuid():N}");
        Directory.CreateDirectory(stagingDir);
        try
        {
            var sdkAssemblyPath = Assembly.Load("Telechron.Sdk").Location;

            File.WriteAllText(Path.Combine(stagingDir, $"{synthesizedModule.ModuleName}.cs"), synthesizedModule.SourceCode);
            File.WriteAllText(Path.Combine(stagingDir, $"{synthesizedModule.ModuleName}.csproj"), BuildModuleCsproj(synthesizedModule.ModuleName, sdkAssemblyPath));

            var selfTestDir = Path.Combine(stagingDir, "SelfTest");
            Directory.CreateDirectory(selfTestDir);
            File.WriteAllText(Path.Combine(selfTestDir, $"{synthesizedModule.ModuleName}.SelfTest.cs"), synthesizedModule.SelfTestCode);
            File.WriteAllText(Path.Combine(selfTestDir, $"{synthesizedModule.ModuleName}.SelfTest.csproj"),
                BuildSelfTestCsproj(synthesizedModule.ModuleName, sdkAssemblyPath));

            var sdkCopyPath = Path.Combine(stagingDir, Path.GetFileName(sdkAssemblyPath));
            File.Copy(sdkAssemblyPath, sdkCopyPath, overwrite: true);
            var sdkCopyPathForSelfTest = Path.Combine(selfTestDir, Path.GetFileName(sdkAssemblyPath));
            File.Copy(sdkAssemblyPath, sdkCopyPathForSelfTest, overwrite: true);

            if (File.Exists(zipPath))
                File.Delete(zipPath);
            ZipFile.CreateFromDirectory(stagingDir, zipPath, CompressionLevel.Fastest, includeBaseDirectory: false);
        }
        finally
        {
            Directory.Delete(stagingDir, recursive: true);
        }
    }

    private static string BuildModuleCsproj(string moduleName, string sdkAssemblyPath) => $$"""
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
            <ImplicitUsings>enable</ImplicitUsings>
            <Nullable>enable</Nullable>
            <AssemblyName>{{moduleName}}</AssemblyName>
          </PropertyGroup>
          <ItemGroup>
            <Reference Include="Telechron.Sdk">
              <HintPath>{{Path.GetFileName(sdkAssemblyPath)}}</HintPath>
            </Reference>
          </ItemGroup>
        </Project>
        """;

    private static string BuildSelfTestCsproj(string moduleName, string sdkAssemblyPath) => $$"""
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
            <ImplicitUsings>enable</ImplicitUsings>
            <Nullable>enable</Nullable>
            <IsPackable>false</IsPackable>
          </PropertyGroup>
          <ItemGroup>
            <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
            <PackageReference Include="xunit" Version="2.9.3" />
            <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
          </ItemGroup>
          <ItemGroup>
            <Using Include="Xunit" />
            <Compile Include="../{{moduleName}}.cs" />
          </ItemGroup>
          <ItemGroup>
            <Reference Include="Telechron.Sdk">
              <HintPath>{{Path.GetFileName(sdkAssemblyPath)}}</HintPath>
            </Reference>
          </ItemGroup>
        </Project>
        """;
}
