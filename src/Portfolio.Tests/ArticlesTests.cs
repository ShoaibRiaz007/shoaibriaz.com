using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Portfolio.Application.Facades;
using Portfolio.Caching;
using Portfolio.Controllers;
using Portfolio.Domain.Entities;
using Portfolio.Models;
using Portfolio.Tests.Fakes;
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

    [Fact]
    public async Task HomeController_Index_PopulatesFeaturedArticles()
    {
        var bios = new FakeRepository<Bio>();
        bios.Items.Add(new Bio { FullName = "Shoaib Riaz", Website = "https://www.shoaibriaz.com" });
        var content = new PublicContentFacade(
            bios,
            new FakeRepository<Service>(),
            new FakeRepository<Experience>(),
            new FakeRepository<Project>(),
            new FakeRepository<Skill>(),
            new FakeRepository<Testimonial>(),
            new FakeRepository<Education>(),
            new FakeRepository<Certification>(),
            new FakeRepository<ProjectMedia>());
        var pages = new SitePageFacade(new FakeRepository<SitePage>(), new FakeClock(DateTimeOffset.UtcNow));
        var controller = new HomeController(content, new NotFoundCache(), new ConfigurationBuilder().Build(), pages);

        var result = await controller.Index(CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<HomeViewModel>(view.Model);
        Assert.Equal(4, model.Articles.Count);
        Assert.Single(model.Articles, a => a.IsPillar);
        Assert.Equal(3, model.Articles.Count(a => !a.IsPillar));
    }

    [Fact]
    public async Task HomeController_Article_ExcludesPillarFromRelated_AndTruncatesMetaDescription()
    {
        var bios = new FakeRepository<Bio>();
        bios.Items.Add(new Bio { FullName = "Shoaib Riaz", Website = "https://www.shoaibriaz.com" });
        var content = new PublicContentFacade(
            bios,
            new FakeRepository<Service>(),
            new FakeRepository<Experience>(),
            new FakeRepository<Project>(),
            new FakeRepository<Skill>(),
            new FakeRepository<Testimonial>(),
            new FakeRepository<Education>(),
            new FakeRepository<Certification>(),
            new FakeRepository<ProjectMedia>());
        var pages = new SitePageFacade(new FakeRepository<SitePage>(), new FakeClock(DateTimeOffset.UtcNow));
        var controller = new HomeController(content, new NotFoundCache(), new ConfigurationBuilder().Build(), pages);

        var result = await controller.Article(DefaultArticles.ClusterIpc.Slug, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ArticleDetailViewModel>(view.Model);
        Assert.NotNull(model.PillarArticle);
        Assert.Equal(DefaultArticles.PillarSlug, model.PillarArticle!.Slug);
        Assert.DoesNotContain(model.RelatedArticles, a => a.IsPillar || a.Slug == DefaultArticles.PillarSlug);
        Assert.Equal(2, model.RelatedArticles.Count);
        Assert.InRange(model.Meta.Description.Length, 1, PublicSite.DescriptionMax);
    }

    [Fact]
    public void DefaultArticles_CodeBlocksAndBody_HaveStrippedRawIndentation()
    {
        foreach (var article in DefaultArticles.All)
        {
            var codeBlocks = RichText.Parse(article.Body).OfType<RichText.CodeBlock>().ToList();
            Assert.NotEmpty(codeBlocks);
            Assert.All(codeBlocks, cb =>
            {
                var nonEmpty = cb.Code.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
                Assert.Contains(nonEmpty, l => !char.IsWhiteSpace(l[0]));
            });
            Assert.DoesNotContain("$V_y", article.Body);
        }
    }
}
