using Portfolio.Models;
using Xunit;

namespace Portfolio.Tests;

public class ArticlesTests
{
    [Fact]
    public void DefaultArticles_HasPillarAndThreeClusters()
    {
        var all = DefaultArticles.All;
        Assert.Equal(4, all.Count);

        var pillar = Assert.Single(all, a => a.IsPillar);
        Assert.Equal(DefaultArticles.PillarSlug, pillar.Slug);
        Assert.Equal("Enterprise .NET 9 Desktop Architecture & AI Agent Tooling", pillar.Title);
        Assert.NotEmpty(pillar.Highlights);
        Assert.NotEmpty(pillar.TargetKeyword);

        var clusters = all.Where(a => !a.IsPillar).ToList();
        Assert.Equal(3, clusters.Count);
        Assert.All(clusters, c =>
        {
            Assert.Equal(DefaultArticles.PillarSlug, c.PillarSlug);
            Assert.NotEmpty(c.Highlights);
            Assert.NotEmpty(c.Summary);
            Assert.NotEmpty(c.Body);
            Assert.NotEmpty(c.TargetKeyword);
        });
    }

    [Theory]
    [InlineData("enterprise-dotnet-9-desktop-architecture-ai-tooling", true)]
    [InlineData("low-latency-json-rpc-named-pipe-ipc-winui-3", true)]
    [InlineData("automating-level-design-claude-api-unity", true)]
    [InlineData("office-add-ins-dotnet-9-wpf-react-webviews", true)]
    [InlineData("LOW-LATENCY-JSON-RPC-NAMED-PIPE-IPC-WINUI-3", true)]
    [InlineData("non-existent-slug", false)]
    public void DefaultArticles_FindBySlug_FindsCaseInsensitively(string slug, bool expectedFound)
    {
        var article = DefaultArticles.FindBySlug(slug);
        Assert.Equal(expectedFound, article is not null);
        if (expectedFound)
        {
            Assert.Equal(slug.ToLowerInvariant(), article!.Slug.ToLowerInvariant());
        }
    }

    [Fact]
    public void TopicCluster_ArticlesHaveUniqueSlugsAndTargetKeywords()
    {
        var slugs = DefaultArticles.All.Select(a => a.Slug).ToList();
        Assert.Equal(slugs.Distinct(StringComparer.OrdinalIgnoreCase).Count(), slugs.Count);

        var keywords = DefaultArticles.All.Select(a => a.TargetKeyword).ToList();
        Assert.Equal(keywords.Distinct(StringComparer.OrdinalIgnoreCase).Count(), keywords.Count);
    }
}
