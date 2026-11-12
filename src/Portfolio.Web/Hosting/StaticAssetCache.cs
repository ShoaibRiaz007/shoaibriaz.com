using Microsoft.Extensions.Primitives;

namespace Portfolio.Hosting;

/// <summary>Long-lived immutable caching for content-hashed static files (?v=... from asp-append-version).</summary>
public static class StaticAssetCache
{
    public const string ImmutableHeaderValue = "public,max-age=31536000,immutable";

    public static void Apply(HttpContext context)
    {
        if (context.Request.Query.TryGetValue("v", out var version) && !StringValues.IsNullOrEmpty(version))
        {
            context.Response.Headers.CacheControl = ImmutableHeaderValue;
        }
    }
}
