using Microsoft.AspNetCore.Mvc;
using Portfolio.Api;
using Portfolio.Application.Facades;
using Portfolio.Domain.Entities;
using Portfolio.Tests.Fakes;
using Xunit;

namespace Portfolio.Tests;

public class PublicApiTests
{
    private readonly FakeRepository<Bio> _bios = new();
    private readonly FakeRepository<Service> _services = new();
    private readonly FakeRepository<Experience> _experiences = new();
    private readonly FakeRepository<Project> _projects = new();
    private readonly FakeRepository<Skill> _skills = new();
    private readonly FakeRepository<Testimonial> _testimonials = new();
    private readonly FakeRepository<Education> _educations = new();
    private readonly FakeRepository<Certification> _certifications = new();
    private readonly FakeRepository<ProjectMedia> _media = new();

    private PublicApiController CreateController()
    {
        var facade = new PublicContentFacade(
            _bios, _services, _experiences, _projects, _skills,
            _testimonials, _educations, _certifications, _media);
        return new PublicApiController(facade);
    }

    [Fact]
    public async Task Site_ReturnsAllAggregatedPublishedData()
    {
        _bios.Items.Add(new Bio { FullName = "Shoaib Riaz", Headline = "Team Lead" });
        _experiences.Items.Add(new Experience { Role = "Lead Engineer", Company = "Lucen Software", IsPublished = true, SortOrder = 1 });
        _projects.Items.Add(new Project { Title = "Rage War", Slug = "rage-war", IsPublished = true, SortOrder = 1, IsFeatured = true });

        var controller = CreateController();
        var actionResult = await controller.Site(CancellationToken.None);

        var dto = actionResult.Value;
        Assert.NotNull(dto);
        Assert.Equal("Shoaib Riaz", dto.Profile.FullName);
        Assert.Single(dto.Experience);
        Assert.Equal("Lucen Software", dto.Experience[0].Company);
        Assert.Single(dto.Projects);
        Assert.Equal("Rage War", dto.Projects[0].Title);
    }

    [Fact]
    public async Task Experience_ReturnsPublishedCompaniesInChronologicalOrder()
    {
        _experiences.Items.Add(new Experience { Role = "Software Engineer", Company = "InvoZone", IsPublished = true, SortOrder = 1 });
        _experiences.Items.Add(new Experience { Role = "Lead Engineer", Company = "Lucen Software", IsPublished = true, SortOrder = 2 });
        _experiences.Items.Add(new Experience { Role = "Draft Role", Company = "Secret Corp", IsPublished = false, SortOrder = 3 });

        var controller = CreateController();
        var actionResult = await controller.Experience(CancellationToken.None);

        var list = actionResult.Value;
        Assert.NotNull(list);
        Assert.Equal(2, list.Count);
        Assert.Contains(list, c => c.Company == "InvoZone");
        Assert.Contains(list, c => c.Company == "Lucen Software");
        Assert.DoesNotContain(list, c => c.Company == "Secret Corp");
    }

    [Fact]
    public async Task Projects_ReturnsPublishedProjectsOnly()
    {
        _projects.Items.Add(new Project { Title = "Published 1", Slug = "pub-1", IsPublished = true, SortOrder = 1 });
        _projects.Items.Add(new Project { Title = "Draft 2", Slug = "draft-2", IsPublished = false, SortOrder = 2 });

        var controller = CreateController();
        var actionResult = await controller.Projects(CancellationToken.None);

        var list = actionResult.Value;
        Assert.NotNull(list);
        Assert.Single(list);
        Assert.Equal("pub-1", list[0].Slug);
    }

    [Fact]
    public async Task Project_WithValidSlug_ReturnsProjectDetailDto()
    {
        _projects.Items.Add(new Project
        {
            Title = "Atomic Golf",
            Slug = "atomic-golf",
            IsPublished = true,
            Description = "Golf description\nParagraph 2",
            Challenge = "Zero downtime required",
            Solution = "Unity and Node.js microservices",
            Outcome = "Shipped on schedule",
            MyRole = "Lead Gameplay Engineer"
        });

        var controller = CreateController();
        var actionResult = await controller.Project("atomic-golf", CancellationToken.None);

        var detail = actionResult.Value;
        Assert.NotNull(detail);
        Assert.Equal("Atomic Golf", detail.Project.Title);
        Assert.Equal(2, detail.Description.Length);
        Assert.Contains("Zero downtime required", detail.Challenge);
        Assert.Equal("Lead Gameplay Engineer", detail.MyRole);
    }

    [Fact]
    public async Task Project_WithUnknownSlug_ReturnsNotFound()
    {
        var controller = CreateController();
        var actionResult = await controller.Project("non-existent-slug", CancellationToken.None);

        Assert.IsType<NotFoundResult>(actionResult.Result);
    }

    [Fact]
    public async Task Profile_ReturnsBioDetails()
    {
        _bios.Items.Add(new Bio
        {
            FullName = "Shoaib Riaz",
            Headline = "Lead .NET Engineer",
            Email = "riazshoaib17@gmail.com",
            LinkedIn = "https://linkedin.com/in/mshoaibriaz"
        });

        var controller = CreateController();
        var actionResult = await controller.Profile(CancellationToken.None);

        var profile = actionResult.Value;
        Assert.NotNull(profile);
        Assert.Equal("Shoaib Riaz", profile.FullName);
        Assert.Equal("riazshoaib17@gmail.com", profile.Email);
    }
}
