using System.Text.Json;
using Portfolio.Domain.Entities;
using Portfolio.Models;
using Xunit;

namespace Portfolio.Tests;

public class StructuredDataTests
{
    private const string Origin = "https://www.shoaibriaz.com";

    private static Bio Bio() => new()
    {
        FullName = "Shoaib Riaz",
        Headline = "Team Lead · C# / .NET · AI/LLM",
        Tagline = "I build AI-powered .NET software and lead the team that ships it.",
        Location = "Lahore, Punjab, Pakistan",
        LinkedIn = "https://www.linkedin.com/in/example",
        GitHub = "",
    };

    private static PersonFacts Facts() => PersonFacts.From(
        [
            new Experience { Role = "Senior Engineer", Company = "Old Co", StartDate = new(2022, 1, 1), EndDate = new(2025, 1, 1) },
            new Experience { Role = "Engineering Manager", Company = " Acme ", StartDate = new(2025, 2, 1) },
        ],
        [new Skill { Name = "C#" }, new Skill { Name = " Unity " }, new Skill { Name = "c#" }],
        [new Education { Institution = "GIFT University" }]);

    private static JsonElement Node(string json, string type)
        => JsonDocument.Parse(json).RootElement.GetProperty("@graph").EnumerateArray().Single(n => n.GetProperty("@type").GetString() == type);

    [Fact]
    public void Facts_TakeTheCurrentRole_AndDistinctSkills()
    {
        var f = Facts();
        Assert.Equal(("Engineering Manager", "Acme"), (f.Role, f.Employer));
        Assert.Equal(["C#", "Unity"], f.Skills);
        Assert.Equal(["GIFT University"], f.Schools);
    }

    [Fact]
    public void HomePage_DescribesThePersonAndTheProfile()
    {
        var json = PublicSite.StructuredData(Bio(), Origin, Origin + "/", "https://img/x.jpg", Facts(), profilePage: true);

        var person = Node(json, "Person");
        Assert.Equal("Shoaib Riaz", person.GetProperty("name").GetString());
        Assert.Equal("Engineering Manager", person.GetProperty("jobTitle").GetString());
        Assert.Equal("Acme", person.GetProperty("worksFor").GetProperty("name").GetString());
        Assert.Equal("GIFT University", person.GetProperty("alumniOf")[0].GetProperty("name").GetString());
        Assert.Equal(2, person.GetProperty("knowsAbout").GetArrayLength());
        var address = person.GetProperty("address");
        Assert.Equal(("Lahore", "Punjab", "Pakistan"), (address.GetProperty("addressLocality").GetString(),
            address.GetProperty("addressRegion").GetString(), address.GetProperty("addressCountry").GetString()));
        Assert.Equal(["https://www.linkedin.com/in/example"], person.GetProperty("sameAs").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal(Origin + "/#person", Node(json, "ProfilePage").GetProperty("mainEntity").GetProperty("@id").GetString());
        Assert.Equal(Origin, Node(json, "WebSite").GetProperty("url").GetString());
    }

    [Fact]
    public void OtherPages_LeaveOutTheProfilePage_AndTheCareerDetails()
    {
        var json = PublicSite.StructuredData(Bio(), Origin, Origin + "/projects", null, null, profilePage: false);

        Assert.DoesNotContain("ProfilePage", json);
        var person = Node(json, "Person");
        Assert.False(person.TryGetProperty("jobTitle", out _));   // only pages with the facts state one
        Assert.False(person.TryGetProperty("image", out _));
        Assert.False(person.TryGetProperty("worksFor", out _));
    }

    [Fact]
    public void AdminText_CannotCloseTheScriptElement()
    {
        var bio = Bio();
        bio.Tagline = "</script><script>alert(1)</script>";
        var json = PublicSite.StructuredData(bio, Origin, Origin, null, null, profilePage: false);
        Assert.DoesNotContain("</script>", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<", json);
    }

    [Fact]
    public void WithoutACurrentRole_TheHeadlineGivesTheTitle()
    {
        var facts = new PersonFacts(null, null, [], []);
        var bio = Bio();
        bio.Headline = "Team Lead • C# / .NET";
        Assert.Equal("Team Lead", Node(PublicSite.StructuredData(bio, Origin, Origin, null, facts, true), "Person").GetProperty("jobTitle").GetString());
    }

    [Fact]
    public void TheCurrentRole_IsTheOneTheHomePageShows()
    {
        // The hero names the first dated role with no end; an undated legacy "... - Present" row isn't it.
        var f = PersonFacts.From(
            [new Experience { Role = "Lead", Company = "Old Co", Period = "2021 - Present" },
             new Experience { Role = "Manager", Company = "New Co", StartDate = new(2025, 1, 1) }], [], []);
        Assert.Equal(("Manager", "New Co"), (f.Role, f.Employer));
    }

    [Fact]
    public void Schools_DedupeIgnoringCase()
        => Assert.Single(PersonFacts.From([], [], [new Education { Institution = "GIFT University" }, new Education { Institution = "Gift University" }]).Schools);

    [Theory]
    [InlineData("Lahore, Punjab, Pakistan", "Lahore", "Punjab", "Pakistan", "Lahore, Pakistan")]
    [InlineData("Lahore, Pakistan", "Lahore", null, "Pakistan", "Lahore, Pakistan")]
    [InlineData("Lahore, Punjab", "Lahore", "Punjab", null, "Lahore, Punjab")]
    [InlineData("Pakistan", null, null, "Pakistan", "Pakistan")]
    [InlineData("Lahore", "Lahore", null, null, "Lahore")]
    [InlineData("Los Angeles, CA", "Los Angeles", "CA", null, "Los Angeles, CA")]
    [InlineData("Austin, Texas, USA", "Austin", "Texas", "USA", "Austin, USA")]
    public void Location_ReadsTheCountryOnlyWhenItIsOne(string location, string? locality, string? region, string? country, string place)
    {
        Assert.Equal((locality, region, country), PublicSite.ParseLocation(location));
        Assert.Equal(place, PublicSite.Place(location));
    }

    [Fact]
    public void NoLocation_NoAddressOrPlace()
    {
        var bio = Bio();
        bio.Location = "";
        Assert.Null(PublicSite.Place(bio.Location));
        Assert.False(Node(PublicSite.StructuredData(bio, Origin, Origin, null, null, false), "Person").TryGetProperty("address", out _));
    }

    [Theory]
    [InlineData("https://www.youtube.com/embed/abc123", "https://www.youtube-nocookie.com/embed/abc123")]
    [InlineData("https://player.vimeo.com/video/42", "https://player.vimeo.com/video/42?dnt=1")]
    [InlineData("https://player.vimeo.com/video/42?h=x", "https://player.vimeo.com/video/42?h=x&dnt=1")]
    [InlineData("https://player.vimeo.com/video/42?dnt=1", "https://player.vimeo.com/video/42?dnt=1")]
    [InlineData("/uploads/clip.mp4", "/uploads/clip.mp4")]
    [InlineData("https://www.youtube.com/watch?v=abc123", "https://www.youtube.com/watch?v=abc123")]
    public void Embeds_RenderInPrivacyMode(string stored, string rendered) => Assert.Equal(rendered, PublicSite.PrivateEmbed(stored));

    [Theory]
    [InlineData("https://youtu.be/dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://vimeo.com/76979871")]
    public void EverySavedEmbed_RendersOnAHostTheCspAllows(string pasted)
    {
        // Embeds are stored as the normalizer writes them; the page's frame-src allows only these two hosts.
        var stored = Portfolio.Application.Common.EmbedUrlNormalizer.Normalize(pasted)!;
        var host = new Uri(PublicSite.PrivateEmbed(stored)).Host;
        Assert.Contains(host, new[] { "www.youtube-nocookie.com", "player.vimeo.com" });
    }

    [Fact]
    public void Description_AddsWhereTheOwnerIsBased_WhenItFits()
    {
        Assert.Equal(Bio().Tagline + " Based in Lahore, Pakistan.", PublicSite.Describe(Bio()));

        var longBio = Bio();
        longBio.Tagline = new string('x', 150);
        Assert.Equal(longBio.Tagline, PublicSite.Describe(longBio));
    }

    [Fact]
    public void Description_EndsTheTaglineSentence_AndCopesWithNone()
    {
        var bio = Bio();
        bio.Tagline = "I build .NET software";
        Assert.StartsWith("I build .NET software. ", PublicSite.Describe(bio));
        bio.Tagline = "Let's build!";
        Assert.StartsWith("Let's build! Based", PublicSite.Describe(bio));
        bio.Tagline = "Hiring?";
        Assert.StartsWith("Hiring? Based", PublicSite.Describe(bio));

        bio.Tagline = "";
        var none = PublicSite.Describe(bio);
        Assert.False(char.IsWhiteSpace(none[0]));
        Assert.Contains("Lahore, Pakistan", none);
    }

    [Fact]
    public void Description_UsesTheWholeLimit()
    {
        var bio = Bio();
        var suffix = PublicSite.Describe(new Bio { Tagline = "x.", Location = bio.Location })[2..];   // " Based in …"
        bio.Tagline = new string('x', PublicSite.DescriptionMax - suffix.Length - 1) + ".";
        Assert.Equal(PublicSite.DescriptionMax, PublicSite.Describe(bio).Length);
    }
}
