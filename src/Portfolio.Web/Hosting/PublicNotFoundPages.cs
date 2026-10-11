using Microsoft.AspNetCore.Diagnostics;
using Portfolio.Models;

namespace Portfolio.Hosting;

public static class PublicNotFoundPages
{
    /// <summary>
    /// Unmatched public URLs get the designed 404 page (re-executed at <paramref name="notFoundPath"/>). The JSON
    /// API and admin keep their plain status codes, and so do static files: a missing image or script shouldn't
    /// render a database-backed page. Only a 404 is re-executed; a 400 (expired antiforgery token), 405 or 429
    /// (login rate limit) keeps its own status instead of becoming "Page not found".
    /// </summary>
    public static IApplicationBuilder UsePublicNotFoundPages(this IApplicationBuilder app, string notFoundPath = "/not-found")
        => app.UseWhen(
            ctx => !ctx.Request.Path.StartsWithSegments("/api") && !ctx.Request.Path.StartsWithSegments("/Admin")
                   && !PublicSite.IsStaticAssetPath(ctx.Request.Path),
            branch =>
            {
                branch.UseStatusCodePagesWithReExecute(notFoundPath);
                // Runs inside the status-code middleware, so it can opt a non-404 response out before that
                // middleware looks at it.
                branch.Use(async (ctx, next) =>
                {
                    await next();
                    if (ctx.Response.StatusCode != StatusCodes.Status404NotFound
                        && ctx.Features.Get<IStatusCodePagesFeature>() is { } pages)
                        pages.Enabled = false;
                });
            });
}
