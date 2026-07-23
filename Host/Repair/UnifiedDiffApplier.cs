using System.Text;
using System.Text.RegularExpressions;

namespace Telechron.Host.Repair;

// Applies a single-file unified diff (the format LibGit2Sharp's
// Patch.Content/PatchEntryChanges.Patch produces, and the format a
// Generate Fix LLM call is prompted to emit) to original file content.
// Deliberately minimal -- supports standard @@ hunk headers with
// context/add/remove lines, not the full git extended-diff header set
// (rename/mode-change metadata), since patches here always target an
// existing file's content, never file renames.
public static partial class UnifiedDiffApplier
{
    [GeneratedRegex(@"^@@ -(?<oldStart>\d+)(?:,(?<oldCount>\d+))? \+(?<newStart>\d+)(?:,(?<newCount>\d+))? @@")]
    private static partial Regex HunkHeaderPattern();

    public static string Apply(string originalContent, string unifiedDiff)
    {
        var originalLines = SplitLines(originalContent);
        var resultLines = new List<string>();
        var diffLines = unifiedDiff.Replace("\r\n", "\n").Split('\n');

        var originalIndex = 0; // 0-based cursor into originalLines
        var i = 0;
        while (i < diffLines.Length)
        {
            var line = diffLines[i];
            var headerMatch = HunkHeaderPattern().Match(line);
            if (!headerMatch.Success)
            {
                i++;
                continue; // skip file headers (---/+++) and any blank trailer lines
            }

            var oldStart = int.Parse(headerMatch.Groups["oldStart"].Value) - 1; // to 0-based
            // Copy unchanged lines between the previous hunk (or start) and this hunk's start.
            while (originalIndex < oldStart && originalIndex < originalLines.Count)
            {
                resultLines.Add(originalLines[originalIndex]);
                originalIndex++;
            }

            i++;
            while (i < diffLines.Length && !HunkHeaderPattern().IsMatch(diffLines[i]))
            {
                var hunkLine = diffLines[i];
                if (hunkLine.Length == 0)
                {
                    i++;
                    continue;
                }

                switch (hunkLine[0])
                {
                    case ' ':
                        resultLines.Add(hunkLine[1..]);
                        originalIndex++;
                        break;
                    case '-':
                        originalIndex++; // consumed from original, not emitted
                        break;
                    case '+':
                        resultLines.Add(hunkLine[1..]);
                        break;
                    case '\\':
                        break; // "\ No newline at end of file" marker -- not content
                    default:
                        throw new InvalidOperationException($"Unrecognized unified diff line: '{hunkLine}'.");
                }

                i++;
            }
        }

        // Trailing unchanged lines after the last hunk.
        while (originalIndex < originalLines.Count)
        {
            resultLines.Add(originalLines[originalIndex]);
            originalIndex++;
        }

        return string.Join('\n', resultLines) + (resultLines.Count > 0 ? "\n" : string.Empty);
    }

    private static List<string> SplitLines(string content)
    {
        if (content.Length == 0)
            return [];
        var normalized = content.Replace("\r\n", "\n");
        var lines = normalized.Split('\n').ToList();
        // A trailing newline produces one trailing empty split entry --
        // drop it so line counts match what the diff's line numbers expect.
        if (lines.Count > 0 && lines[^1].Length == 0)
            lines.RemoveAt(lines.Count - 1);
        return lines;
    }
}
