using Telechron.Sdk.Findings;

namespace Telechron.Sdk.Tests.Findings;

public class FileLengthLintCheckerTests
{
    [Fact]
    public void Check_FileOverThreshold_IsReported()
    {
        var tempRoot = Directory.CreateTempSubdirectory("telechron-lint-");
        try
        {
            File.WriteAllLines(Path.Combine(tempRoot.FullName, "Big.cs"), Enumerable.Repeat("// line", 850));

            var violations = FileLengthLintChecker.Check(tempRoot.FullName);

            var violation = Assert.Single(violations);
            Assert.Equal("Big.cs", violation.RelativePath);
            Assert.Equal(850, violation.LineCount);
        }
        finally
        {
            tempRoot.Delete(recursive: true);
        }
    }

    [Fact]
    public void Check_FileUnderThreshold_IsNotReported()
    {
        var tempRoot = Directory.CreateTempSubdirectory("telechron-lint-");
        try
        {
            File.WriteAllLines(Path.Combine(tempRoot.FullName, "Small.cs"), Enumerable.Repeat("// line", 10));

            var violations = FileLengthLintChecker.Check(tempRoot.FullName);

            Assert.Empty(violations);
        }
        finally
        {
            tempRoot.Delete(recursive: true);
        }
    }

    [Fact]
    public void Check_ExcludedDirectories_AreSkippedEvenWhenOverThreshold()
    {
        var tempRoot = Directory.CreateTempSubdirectory("telechron-lint-");
        try
        {
            var binDir = Path.Combine(tempRoot.FullName, "bin");
            Directory.CreateDirectory(binDir);
            File.WriteAllLines(Path.Combine(binDir, "Generated.cs"), Enumerable.Repeat("// line", 900));

            var migrationsDir = Path.Combine(tempRoot.FullName, "Migrations");
            Directory.CreateDirectory(migrationsDir);
            File.WriteAllLines(Path.Combine(migrationsDir, "20260101_Init.cs"), Enumerable.Repeat("// line", 900));

            var violations = FileLengthLintChecker.Check(tempRoot.FullName);

            Assert.Empty(violations);
        }
        finally
        {
            tempRoot.Delete(recursive: true);
        }
    }

    [Fact]
    public void Check_NonMatchingExtension_IsSkippedEvenWhenOverThreshold()
    {
        var tempRoot = Directory.CreateTempSubdirectory("telechron-lint-");
        try
        {
            File.WriteAllLines(Path.Combine(tempRoot.FullName, "data.json"), Enumerable.Repeat("line", 900));

            var violations = FileLengthLintChecker.Check(tempRoot.FullName);

            Assert.Empty(violations);
        }
        finally
        {
            tempRoot.Delete(recursive: true);
        }
    }

    [Fact]
    public void Check_RealTelechronRepoRoot_ExcludesMigrationsAndFindsNoUnexpectedFailure()
    {
        // Live, self-referential check (this session's established
        // preference for real targets over synthetic-only fixtures): run
        // the lint against the actual repo instead of only a temp fixture,
        // proving the exclusion list actually holds up against real EF
        // Migration Designer.cs files, which are routinely >800 lines.
        var repoRoot = FindRepoRoot();

        var violations = FileLengthLintChecker.Check(repoRoot);

        Assert.DoesNotContain(violations, v => v.RelativePath.Contains("/Migrations/", StringComparison.Ordinal));
        Assert.DoesNotContain(violations, v => v.RelativePath.Contains("/bin/", StringComparison.Ordinal));
        Assert.DoesNotContain(violations, v => v.RelativePath.Contains("/obj/", StringComparison.Ordinal));
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Telechron.slnx")))
            dir = dir.Parent;

        return dir?.FullName ?? throw new InvalidOperationException("Could not locate Telechron.slnx above the test output directory.");
    }
}
