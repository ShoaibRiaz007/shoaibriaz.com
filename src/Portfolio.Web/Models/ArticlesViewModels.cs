using Portfolio.Domain.Entities;

namespace Portfolio.Models;

public record ArticlesViewModel : IPublicPage
{
    public required Bio Bio { get; init; }
    public required Article PillarArticle { get; init; }
    public required IReadOnlyList<Article> ClusterArticles { get; init; }
    public required PageMeta Meta { get; init; }
    public PersonFacts? Person { get; init; }
}

public record ArticleDetailViewModel : IPublicPage
{
    public required Bio Bio { get; init; }
    public required Article Article { get; init; }
    public Article? PillarArticle { get; init; }
    public required IReadOnlyList<Article> RelatedArticles { get; init; }
    public required IReadOnlyList<RichText.Block> Blocks { get; init; }
    public required PageMeta Meta { get; init; }
    public PersonFacts? Person { get; init; }
}
