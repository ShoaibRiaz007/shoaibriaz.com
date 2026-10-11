using Microsoft.AspNetCore.Html;

namespace Portfolio.Models;

/// <summary>Inline stroke icons for the admin UI (decorative: always aria-hidden; the control carries the label).
/// Inline SVG keeps them themeable through currentColor without an icon font or extra requests.</summary>
public static class AdminIcons
{
    private static readonly Dictionary<string, string> Paths = new()
    {
        ["dashboard"] = "<rect x=\"3\" y=\"3\" width=\"7\" height=\"7\" rx=\"1.5\"/><rect x=\"14\" y=\"3\" width=\"7\" height=\"7\" rx=\"1.5\"/><rect x=\"14\" y=\"14\" width=\"7\" height=\"7\" rx=\"1.5\"/><rect x=\"3\" y=\"14\" width=\"7\" height=\"7\" rx=\"1.5\"/>",
        ["user"] = "<circle cx=\"12\" cy=\"8\" r=\"4\"/><path d=\"M4 21a8 8 0 0 1 16 0\"/>",
        ["folder"] = "<path d=\"M3 7a2 2 0 0 1 2-2h4l2 2h8a2 2 0 0 1 2 2v8a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z\"/>",
        ["briefcase"] = "<rect x=\"3\" y=\"7\" width=\"18\" height=\"13\" rx=\"2\"/><path d=\"M8 7V5a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2M3 13h18\"/>",
        ["graduation"] = "<path d=\"M22 10 12 5 2 10l10 5 10-5z\"/><path d=\"M6 12v5c3 2 9 2 12 0v-5\"/>",
        ["award"] = "<circle cx=\"12\" cy=\"9\" r=\"6\"/><path d=\"m8.5 14-1.5 8 5-3 5 3-1.5-8\"/>",
        ["wrench"] = "<path d=\"M14.7 6.3a4 4 0 0 0-5.4 5.4L3 18l3 3 6.3-6.3a4 4 0 0 0 5.4-5.4l-2.5 2.5-2.4-.6-.6-2.4z\"/>",
        ["layers"] = "<path d=\"m12 3 9 5-9 5-9-5z\"/><path d=\"m3 13 9 5 9-5\"/>",
        ["quote"] = "<path d=\"M5 7h5v5c0 3-1.5 5-4.5 6M14 7h5v5c0 3-1.5 5-4.5 6\"/>",
        ["key"] = "<circle cx=\"7.5\" cy=\"15.5\" r=\"4.5\"/><path d=\"m10.7 12.3 9.3-9.3M17 6l3 3M14 9l2 2\"/>",
        ["external"] = "<path d=\"M14 4h6v6M20 4l-9 9M18 14v5a1 1 0 0 1-1 1H5a1 1 0 0 1-1-1V7a1 1 0 0 1 1-1h5\"/>",
        ["logout"] = "<path d=\"M15 4h4a1 1 0 0 1 1 1v14a1 1 0 0 1-1 1h-4M10 17l5-5-5-5M15 12H3\"/>",
        ["menu"] = "<path d=\"M4 6h16M4 12h16M4 18h16\"/>",
        ["close"] = "<path d=\"M6 6l12 12M18 6 6 18\"/>",
        ["search"] = "<circle cx=\"11\" cy=\"11\" r=\"7\"/><path d=\"m20 20-3.5-3.5\"/>",
        ["plus"] = "<path d=\"M12 5v14M5 12h14\"/>",
        ["edit"] = "<path d=\"M4 20h4L19 9l-4-4L4 16z\"/><path d=\"m13.5 6.5 4 4\"/>",
        ["trash"] = "<path d=\"M4 7h16M10 11v6M14 11v6M6 7l1 13h10l1-13M9 7V4h6v3\"/>",
        ["eye"] = "<path d=\"M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7S2 12 2 12z\"/><circle cx=\"12\" cy=\"12\" r=\"3\"/>",
        ["eye-off"] = "<path d=\"M3 3l18 18M10.6 5.1A10 10 0 0 1 12 5c6.5 0 10 7 10 7a17 17 0 0 1-3 3.9M6.6 6.6A17 17 0 0 0 2 12s3.5 7 10 7a9.6 9.6 0 0 0 5.4-1.6M9.9 9.9a3 3 0 0 0 4.2 4.2\"/>",
        ["grip"] = "<circle cx=\"9\" cy=\"6\" r=\"1.3\"/><circle cx=\"15\" cy=\"6\" r=\"1.3\"/><circle cx=\"9\" cy=\"12\" r=\"1.3\"/><circle cx=\"15\" cy=\"12\" r=\"1.3\"/><circle cx=\"9\" cy=\"18\" r=\"1.3\"/><circle cx=\"15\" cy=\"18\" r=\"1.3\"/>",
        ["upload"] = "<path d=\"M12 15V3M7 8l5-5 5 5M4 15v4a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2v-4\"/>",
        ["check"] = "<path d=\"m5 12 5 5L20 7\"/>",
        ["alert"] = "<circle cx=\"12\" cy=\"12\" r=\"9\"/><path d=\"M12 8v4M12 16h.01\"/>",
        ["chevron-up"] = "<path d=\"m6 15 6-6 6 6\"/>",
        ["chevron-down"] = "<path d=\"m6 9 6 6 6-6\"/>",
        ["arrow-left"] = "<path d=\"M19 12H5M12 19l-7-7 7-7\"/>",
        ["arrow-right"] = "<path d=\"M5 12h14M12 5l7 7-7 7\"/>",
        ["file"] = "<path d=\"M14 3H6a1 1 0 0 0-1 1v16a1 1 0 0 0 1 1h12a1 1 0 0 0 1-1V8z\"/><path d=\"M14 3v5h5\"/>",
        ["image"] = "<rect x=\"3\" y=\"3\" width=\"18\" height=\"18\" rx=\"2\"/><circle cx=\"9\" cy=\"9\" r=\"2\"/><path d=\"m21 15-5-5L5 21\"/>",
        ["star"] = "<path d=\"m12 3 2.8 5.7 6.2.9-4.5 4.4 1 6.2L12 17.3l-5.5 2.9 1-6.2L3 9.6l6.2-.9z\"/>",
        ["activity"] = "<path d=\"M3 12h4l3-8 4 16 3-8h4\"/>",
        ["shield"] = "<path d=\"M12 3 4 6v6c0 4.5 3.4 8.3 8 9 4.6-.7 8-4.5 8-9V6z\"/>",
        ["video"] = "<rect x=\"3\" y=\"6\" width=\"13\" height=\"12\" rx=\"2\"/><path d=\"m16 10 5-3v10l-5-3\"/>",
    };

    public static IHtmlContent Get(string name, string? cssClass = null)
    {
        var body = Paths.TryGetValue(name, out var p) ? p : Paths["alert"];
        // "grip" is drawn with filled dots; everything else is a 1.75px stroke.
        var paint = name == "grip" ? "fill=\"currentColor\" stroke=\"none\"" : "fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.75\" stroke-linecap=\"round\" stroke-linejoin=\"round\"";
        return new HtmlString(
            $"<svg class=\"icon{(cssClass is null ? "" : " " + cssClass)}\" viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" {paint} aria-hidden=\"true\" focusable=\"false\">{body}</svg>");
    }
}
