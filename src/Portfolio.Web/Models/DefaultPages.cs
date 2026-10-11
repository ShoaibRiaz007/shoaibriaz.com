namespace Portfolio.Models;

/// <summary>Built-in text for editable pages, shown until the page is edited in the admin (and after a reset).
/// Written in <see cref="RichText"/> markup; the placeholders are listed in <see cref="PrivacyPlaceholders"/>.</summary>
public static class DefaultPages
{
    public const string PrivacyTitle = "What this site records";

    public const string PrivacyBody = """
        Short version: anonymous visit counts, no cookies for visitors, no ads, no third-party analytics, and nothing at all if your browser asks not to be tracked.

        ## Visit statistics
        Each page you open sends one small report to this site's own server, so I can see how many people visit and how they found it. A report holds:
        - the page you opened, and the site that linked you here (without its query string, apart from utm_ campaign tags);
        - an approximate country, worked out from your browser's time zone or language, never from your address;
        - your browser's time zone and language, your screen width, and the device type, browser and operating system your browser announces;
        - an anonymous visitor ID, so one person reading five pages counts as one visitor.

        The visitor ID is a one-way hash of your IP address and browser, keyed with a random secret that exists only in the server's memory and is replaced every day. Your IP address itself is never stored, and once the day ends nobody, including me, can tell whose visit an ID was or link it to your visits on other days.

        ## What it doesn't do
        - **No cookies:** the site sets none for visitors, and nothing is stored in your browser for the statistics.
        - No advertising, no tracking pixels and no third-party analytics: the reports go only to this site.
        - Nothing is sold or shared.
        - If your browser sends **Do Not Track** or **Global Privacy Control**, no report is sent, and the server ignores one if it arrives anyway.

        ## Stored in your browser
        Your light or dark theme choice, and on the 3D island your journal, planted flowers and settings, are kept in your browser's local storage so they're there next time. They never leave your device; clearing your browser's site data removes them.

        ## How long
        Visit reports are deleted automatically once they are {retention_days} days old, by a clean-up that runs daily while the site is up.

        ## Services the site relies on
        Like any website, these services see your IP address when your browser contacts them, under their own privacy policies:
        - **Render** hosts the site;
        - **Neon** hosts its database;
        - **Cloudinary** stores and serves the uploaded images, videos and CV;
        - **Google Fonts** serves the typeface;
        - **YouTube** or **Vimeo** load only on project pages that embed a video, in their privacy modes: YouTube's no-cookie player, which YouTube says doesn't use cookies to track you unless you play the video, and Vimeo with Do Not Track on. When a player loads, it is told this site's web address (not which page), which it needs before it will play.

        An image linked from another website loads from that website.

        ## Signing in
        The sign-in page is for the site's owner. It and the admin area set only the cookies they need: one that protects the sign-in form, the session once signed in, and a short-lived one for "saved" messages in the admin area. For security, each sign-in attempt records the time, the admin account it was for (if any), the IP address, the browser and an approximate country from its language. Passwords are never recorded. These records are deleted on the same schedule as visit reports.

        ## Questions
        Email [{email}](mailto:{email}). Because visit records hold no name, address or lasting identifier, there is nothing that can be looked up by person, but I'm happy to explain anything here.
        """;

    /// <summary>Placeholders the privacy text can use, with what each is replaced by.</summary>
    public static readonly IReadOnlyList<(string Name, string Meaning)> PrivacyPlaceholders =
    [
        ("retention_days", "how many days visit and sign-in records are kept (from the server setting)"),
        ("email", "the email address on your profile"),
        ("name", "your name from the profile"),
    ];

    /// <summary>The saved privacy title and body, or the built-in ones where nothing was saved.</summary>
    public static (string Title, string Body) Privacy(Portfolio.Domain.Entities.SitePage? saved)
        => (string.IsNullOrWhiteSpace(saved?.Title) ? PrivacyTitle : saved.Title,
            string.IsNullOrWhiteSpace(saved?.Body) ? PrivacyBody : saved.Body);

    public static IReadOnlyDictionary<string, string> PrivacyValues(Portfolio.Domain.Entities.Bio bio, int retentionDays) => new Dictionary<string, string>
    {
        ["retention_days"] = retentionDays.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["email"] = bio.Email ?? "",
        ["name"] = string.IsNullOrWhiteSpace(bio.FullName) ? "the site owner" : bio.FullName,
    };
}
