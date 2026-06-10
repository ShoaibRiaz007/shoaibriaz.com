using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Portfolio.Application.Abstractions;
using Portfolio.Models;

namespace Portfolio.Controllers;

public class HomeController : Controller
{
    private readonly IPublicContentFacade _content;

    public HomeController(IPublicContentFacade content) => _content = content;

    [OutputCache(PolicyName = "public-content")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var data = await _content.GetHomeAsync(6, ct);
        return View(new HomeViewModel
        {
            Bio = data.Bio,
            Services = data.Services.ToList(),
            Experiences = data.Experiences.ToList(),
            Projects = data.ProminentProjects.ToList(),
            Skills = data.Skills.ToList(),
            Testimonials = data.Testimonials.ToList(),
            TotalProjects = data.TotalProjects,
        });
    }

    [HttpGet("/projects")]
    [OutputCache(PolicyName = "public-content")]
    public async Task<IActionResult> Projects(CancellationToken ct)
    {
        var data = await _content.GetProjectsAsync(ct);
        return View(new ProjectsViewModel
        {
            Projects = data.Projects.ToList(),
            Categories = data.Categories.ToList(),
            Bio = data.Bio,
        });
    }

    [HttpGet("/project/{slug}")]
    [OutputCache(PolicyName = "public-content")]
    public async Task<IActionResult> Project(string slug, CancellationToken ct)
    {
        var detail = await _content.GetProjectDetailAsync(slug, ct);
        if (detail is null) return NotFound();

        return View(new ProjectDetailViewModel
        {
            Project = detail.Project,
            Media = detail.Media.ToList(),
            More = detail.More.ToList(),
            Bio = detail.Bio,
        });
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
        => View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
