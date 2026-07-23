using Telechron.Host.DesignDocuments;

namespace Telechron.Host.Persistence.Tests.DesignDocuments;

public sealed class MarkdownRequirementParserTests
{
    [Fact]
    public void Parse_ExtractsInlineTitle_WhenPresent()
    {
        const string markdown = "R-NS1 — Modularize Everything\nEvery leaf capability lives in a module.\n\nR-NS2 — One Repair Loop\nText.";

        var results = MarkdownRequirementParser.Parse(markdown);

        Assert.Equal(2, results.Count);
        Assert.Equal("R-NS1", results[0].RequirementId);
        Assert.Equal("Modularize Everything", results[0].Title);
        Assert.Contains("Every leaf capability lives in a module.", results[0].Body);
    }

    [Fact]
    public void Parse_DerivesTitle_WhenHeaderIsBare()
    {
        const string markdown = "R-RUN2\nTest runners are pluggable and provided through modules.\n\nR-RUN3\nRuns emit heartbeats while active.";

        var results = MarkdownRequirementParser.Parse(markdown);

        Assert.Equal(2, results.Count);
        Assert.Equal("R-RUN2", results[0].RequirementId);
        Assert.Equal("Test runners are pluggable and provided through modules", results[0].Title);
    }

    [Fact]
    public void Parse_BodyStopsAtNextHeader_NotAtBlankLinesOrBulletLikeContent()
    {
        const string markdown = """
            R-DM2 — Run
            Represents test execution.

            Lifecycle:

            Pending
            Running
            Stalled

            R-DM3 — Finding
            Represents any discovered issue.
            """;

        var results = MarkdownRequirementParser.Parse(markdown);

        Assert.Equal(2, results.Count);
        Assert.Contains("Pending", results[0].Body);
        Assert.Contains("Stalled", results[0].Body);
        Assert.DoesNotContain("Represents any discovered issue", results[0].Body);
        Assert.Equal("R-DM3", results[1].RequirementId);
    }

    [Fact]
    public void Parse_IgnoresNonRequirementHeaders()
    {
        const string markdown = "3. Core Capabilities\n   3.1 Test & Run Execution\nR-RUN1\nThe Host dispatches Runs.";

        var results = MarkdownRequirementParser.Parse(markdown);

        Assert.Single(results);
        Assert.Equal("R-RUN1", results[0].RequirementId);
    }

    [Fact]
    public void Parse_RealTechDesignDocument_ProducesExpectedKnownRequirements()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "TechDesign.md");
        var markdown = File.ReadAllText(Path.GetFullPath(path));

        var results = MarkdownRequirementParser.Parse(markdown);

        Assert.True(results.Count >= 60, $"Expected at least 60 requirements, got {results.Count}.");

        var byId = results.ToDictionary(r => r.RequirementId);
        Assert.True(byId.ContainsKey("R-NS1"));
        Assert.Equal("Modularize Everything", byId["R-NS1"].Title);
        Assert.True(byId.ContainsKey("R-DM16"));
        Assert.Contains("Design Document", byId["R-DM16"].Title, StringComparison.Ordinal);
        Assert.True(byId.ContainsKey("R-SEC9"));
        Assert.True(byId.ContainsKey("R-FIX13"));

        // No duplicate IDs — every requirement in the doc is uniquely keyed.
        Assert.Equal(results.Count, byId.Count);
    }
}
