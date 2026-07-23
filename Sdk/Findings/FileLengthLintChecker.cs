namespace Telechron.Sdk.Findings;

public sealed record FileLengthViolation(string RelativePath, int LineCount);

// R-ENG1: "No source file exceeds ~800 lines... enforced by a lint/CI
// Function that emits a code-quality Finding on violation -- advisory,
// not a hard build failure, so the system can auto-repair its own
// violations." Native C# port of scripts/Check-FileLength.ps1 so the
// repair pipeline doesn't need to shell out to PowerShell at runtime --
// same threshold, same exclusions, kept in sync deliberately.
public static class FileLengthLintChecker
{
    private const int DefaultMaxLines = 800;

    private static readonly string[] ExcludedDirectorySegments = ["bin", "obj", "node_modules", ".git", "dist", "build", "Migrations"];
    private static readonly string[] Extensions = [".cs", ".ts", ".tsx", ".js", ".jsx"];

    public static IReadOnlyList<FileLengthViolation> Check(string repoRoot, int maxLines = DefaultMaxLines)
    {
        var violations = new List<FileLengthViolation>();
        var normalizedRoot = Path.GetFullPath(repoRoot);

        foreach (var filePath in Directory.EnumerateFiles(normalizedRoot, "*", SearchOption.AllDirectories))
        {
            if (!Extensions.Contains(Path.GetExtension(filePath), StringComparer.OrdinalIgnoreCase))
                continue;

            var relativePath = Path.GetRelativePath(normalizedRoot, filePath);
            var segments = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (segments.Any(s => ExcludedDirectorySegments.Contains(s, StringComparer.Ordinal)))
                continue;

            int lineCount;
            try
            {
                lineCount = File.ReadLines(filePath).Count();
            }
            catch (IOException)
            {
                continue; // file locked/unreadable -- skip rather than fail the whole scan
            }

            if (lineCount > maxLines)
                violations.Add(new FileLengthViolation(relativePath.Replace('\\', '/'), lineCount));
        }

        return violations;
    }
}
