using System.Text.RegularExpressions;
using Telechron.Sdk.Domain;

namespace Telechron.Host.DesignDocuments;

// Parses R-XXX requirement blocks out of TechDesign.md-style markdown, for
// seeding Telechron's own reflexive Design Document (R-DM16a). A requirement
// block starts at a line matching HeaderPattern and runs up to (but not
// including) the next such line — this is the one reliable boundary in an
// otherwise loosely-structured document (blank lines, bullet-style single
// words, and "Contains:" lists all appear inside bodies and can't be used as
// delimiters). Two header shapes are supported:
//   "R-XXX — Title"   -> Title is everything after the em-dash
//   "R-XXX" (bare)    -> Title is derived from the first sentence of the body
public static partial class MarkdownRequirementParser
{
    [GeneratedRegex(@"^R-[A-Z]+\d+[a-z]?(\s*—\s*(?<title>.+))?$")]
    private static partial Regex HeaderPattern();

    public static IReadOnlyList<ParsedRequirement> Parse(string markdown)
    {
        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        var headerLineIndices = new List<(int LineIndex, string RequirementId, string? InlineTitle)>();

        for (var i = 0; i < lines.Length; i++)
        {
            var trimmed = lines[i].Trim();
            var match = HeaderPattern().Match(trimmed);
            if (!match.Success) continue;

            var requirementId = trimmed.Split([' ', '—'], 2)[0];
            var inlineTitle = match.Groups["title"].Success ? match.Groups["title"].Value.Trim() : null;
            headerLineIndices.Add((i, requirementId, inlineTitle));
        }

        var results = new List<ParsedRequirement>();
        for (var i = 0; i < headerLineIndices.Count; i++)
        {
            var (lineIndex, requirementId, inlineTitle) = headerLineIndices[i];
            var bodyStart = lineIndex + 1;
            var bodyEnd = i + 1 < headerLineIndices.Count ? headerLineIndices[i + 1].LineIndex : lines.Length;

            var bodyLines = lines[bodyStart..bodyEnd]
                .Select(l => l.Trim())
                .Where(l => l.Length > 0)
                .ToList();
            var body = string.Join("\n\n", bodyLines);

            var title = inlineTitle ?? DeriveTitleFromBody(bodyLines);
            results.Add(new ParsedRequirement(requirementId, title, body));
        }

        return results;
    }

    private static string DeriveTitleFromBody(IReadOnlyList<string> bodyLines)
    {
        if (bodyLines.Count == 0) return "(untitled)";
        var firstLine = bodyLines[0];
        var sentenceEnd = firstLine.IndexOf(". ", StringComparison.Ordinal);
        var title = sentenceEnd > 0 ? firstLine[..sentenceEnd] : firstLine.TrimEnd('.', ':');
        return title.Length > 100 ? title[..100] + "…" : title;
    }
}
