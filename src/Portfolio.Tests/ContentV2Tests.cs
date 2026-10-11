using System.Text;
using Portfolio.Application.Common;
using Portfolio.Application.Facades;
using Portfolio.Application.Models;
using Portfolio.Domain.Entities;
using Portfolio.Domain.Services;
using Portfolio.Infrastructure.Security;
using Portfolio.Tests.Fakes;
using Xunit;

namespace Portfolio.Tests;

public class PeriodFormatterTests
{
    [Fact]
    public void Format_OpenEnded_ShowsPresent()
        => Assert.Equal("Jul 2026 - Present", PeriodFormatter.Format(new DateOnly(2026, 7, 1), null));

    [Fact]
    public void Format_Closed_ShowsBothMonths()
        => Assert.Equal("Aug 2023 - Jun 2025", PeriodFormatter.Format(new DateOnly(2023, 8, 1), new DateOnly(2025, 6, 30)));

    [Fact]
    public void Format_NoStart_IsEmpty() => Assert.Equal("", PeriodFormatter.Format(null, null));

    [Fact]
    public void ToMonth_NormalisesToFirstOfMonth()
        => Assert.Equal(new DateOnly(2025, 6, 1), PeriodFormatter.ToMonth(new DateOnly(2025, 6, 14)));
}

public class ExperienceGroupingTests
{
    private static Experience Role(string company, int start, int? end) => new()
    {
        Company = company, Role = $"{company} {start}",
        StartDate = new DateOnly(start, 1, 1), EndDate = end is null ? null : new DateOnly(end.Value, 1, 1),
    };

    [Fact]
    public void GroupConsecutive_MergesPromotionsAtSameCompany()
    {
        var groups = ExperienceGrouping.GroupConsecutive(new[]
        {
            Role("Lucen Software", 2026, null), Role("lucen software ", 2025, 2026), Role("InvoZone", 2022, 2025),
        });

        Assert.Equal(2, groups.Count);
        Assert.Equal(2, groups[0].Roles.Count);
        Assert.Null(groups[0].EndDate); // a current role makes the whole company "Present"
        Assert.Equal(new DateOnly(2025, 1, 1), groups[0].StartDate);
    }

    private static Experience Legacy(string company, string period) => new() { Company = company, Role = period, Period = period };

    [Fact]
    public void UndatedRoleEndingInPresent_MakesTheGroupCurrent()
    {
        var group = ExperienceGrouping.GroupConsecutive(new[] { Legacy("Acme", "2024 - Present"), Role("Acme", 2021, 2024) }).Single();
        Assert.Null(group.EndDate);
        Assert.Equal(PeriodFormatter.Format(new DateOnly(2021, 1, 1), null), group.Period);
    }

    [Fact]
    public void AllLegacyGroup_SpansOldestStartToNewestEnd()
    {
        var group = ExperienceGrouping.GroupConsecutive(new[] { Legacy("Acme", "2019 – 2021"), Legacy("Acme", "2017 – 2019") }).Single();
        Assert.Equal("2017 - 2021", group.Period);
    }

    [Fact]
    public void AllLegacyGroup_UnsplittablePeriod_ShowsTheNewestRole()
    {
        var group = ExperienceGrouping.GroupConsecutive(new[] { Legacy("Acme", "Since 2020"), Legacy("Acme", "2017 - 2019") }).Single();
        Assert.Equal("Since 2020", group.Period);
    }

    [Fact]
    public void GroupConsecutive_RejoiningACompanyStaysSeparate()
    {
        var groups = ExperienceGrouping.GroupConsecutive(new[]
        {
            Role("InvoZone", 2025, 2026), Role("UserWise", 2024, 2025), Role("InvoZone", 2022, 2024),
        });
        Assert.Equal(3, groups.Count);
    }
}

public class PdfValidationTests
{
    private static readonly byte[] PdfHeader = Encoding.ASCII.GetBytes("%PDF-1.7\n%âãÏÓ");

    [Fact]
    public void ValidatePdf_RealPdf_Succeeds() => Assert.True(FileSignatureValidator.ValidatePdf(PdfHeader, "cv.pdf").Ok);

    [Fact]
    public void ValidatePdf_WrongExtension_Fails() => Assert.False(FileSignatureValidator.ValidatePdf(PdfHeader, "cv.exe").Ok);

    [Fact]
    public void ValidatePdf_SpoofedContent_Fails()
        => Assert.False(FileSignatureValidator.ValidatePdf(new byte[] { 0x4D, 0x5A, 0x90, 0, 3, 0 }, "cv.pdf").Ok); // MZ = executable

    [Fact]
    public void Validate_ProjectMediaStillRejectsPdf()
        => Assert.False(FileSignatureValidator.Validate(PdfHeader, "cv.pdf").Ok);
}

public class AdminContentFacadeV2Tests
{
    private readonly FakeRepository<Bio> _bios = new();
    private readonly FakeRepository<Project> _projects = new();
    private readonly FakeRepository<Experience> _experiences = new();
    private readonly FakeFileStorage _files = new();

    private AdminContentFacade Sut() => new(
        _bios, new FakeRepository<Service>(), _experiences, _projects, new FakeRepository<Skill>(),
        new FakeRepository<Testimonial>(), new FakeRepository<Education>(), new FakeRepository<Certification>(),
        new FakeRepository<ProjectMedia>(), new FakeRepository<AdminUser>(), new PasswordHasher(), _files, new FakeUnitOfWork());

    private void AddProjects(params int[] ids)
    {
        for (var i = 0; i < ids.Length; i++)
            _projects.Items.Add(new Project { Id = ids[i], Title = $"P{ids[i]}", SortOrder = i + 1 });
    }

    private int[] OrderOfIds() => _projects.Items.OrderBy(p => p.SortOrder).Select(p => p.Id).ToArray();

    [Fact]
    public async Task Reorder_AppliesRequestedOrder()
    {
        AddProjects(1, 2, 3);
        await Sut().ReorderAsync(ContentKind.Projects, new[] { 3, 1, 2 });
        Assert.Equal(new[] { 3, 1, 2 }, OrderOfIds());
    }

    [Fact]
    public async Task Reorder_StaleListMissingARow_KeepsItAtTheEnd()
    {
        // A second admin tab added row 4 after this tab loaded; its reorder must not drop or collide with it.
        AddProjects(1, 2, 3, 4);
        await Sut().ReorderAsync(ContentKind.Projects, new[] { 2, 1, 3 });
        Assert.Equal(new[] { 2, 1, 3, 4 }, OrderOfIds());
        Assert.Equal(4, _projects.Items.Select(p => p.SortOrder).Distinct().Count());
    }

    [Fact]
    public async Task SetPublished_HidesRow_AndReportsMissing()
    {
        AddProjects(1);
        Assert.True(await Sut().SetPublishedAsync(ContentKind.Projects, 1, false));
        Assert.False(_projects.Items[0].IsPublished);
        Assert.False(await Sut().SetPublishedAsync(ContentKind.Projects, 99, false));
    }

    [Fact]
    public async Task SaveExperience_DerivesPeriodFromMonths()
    {
        var role = new Experience { Role = "Team Lead", Company = "Lucen Software", StartDate = new DateOnly(2026, 7, 9) };
        await Sut().SaveExperienceAsync(role);
        Assert.Equal(new DateOnly(2026, 7, 1), role.StartDate);
        Assert.Equal("Jul 2026 - Present", role.Period);
    }

    [Fact]
    public async Task SaveExperience_LegacyRowWithoutDates_KeepsItsPeriod()
    {
        var role = new Experience { Role = "Dev", Company = "X", Period = "2019 – 2020" };
        await Sut().SaveExperienceAsync(role);
        Assert.Equal("2019 – 2020", role.Period);
    }

    [Fact]
    public async Task NewRows_AppendToEndOfList()
    {
        AddProjects(1, 2);
        var p = new Project { Title = "New" };
        await Sut().SaveProjectAsync(p);
        Assert.Equal(3, p.SortOrder);
    }

    [Fact]
    public async Task UploadResume_ReplacesPreviousFile_AfterBioPointsAtNewOne()
    {
        _bios.Items.Add(new Bio { Id = 1, ResumeUrl = "/uploads/old.pdf" });
        using var pdf = new MemoryStream(Encoding.ASCII.GetBytes("%PDF-1.7 rest of file"));

        var result = await Sut().UploadResumeAsync(pdf, "Shoaib_Riaz_CV.pdf");

        Assert.True(result.Success, result.Message);
        Assert.Equal(_files.Saved.Single(), _bios.Items[0].ResumeUrl);
        Assert.Equal(new[] { "/uploads/old.pdf" }, _files.Deleted);
    }

    [Fact]
    public async Task UploadResume_NonPdf_IsRejectedWithoutStoringAnything()
    {
        _bios.Items.Add(new Bio { Id = 1 });
        using var png = new MemoryStream(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0 });

        var result = await Sut().UploadResumeAsync(png, "cv.pdf");

        Assert.False(result.Success);
        Assert.Empty(_files.Saved);
    }

    [Fact]
    public async Task UploadProfileImage_VideoFile_IsRejected()
    {
        _bios.Items.Add(new Bio { Id = 1 });
        // ....ftyp = mp4 container; a valid gallery upload, but not a photo.
        using var mp4 = new MemoryStream(new byte[] { 0, 0, 0, 0x18, 0x66, 0x74, 0x79, 0x70, 0, 0, 0, 0 });

        var result = await Sut().UploadProfileImageAsync(mp4, "me.mp4");

        Assert.False(result.Success);
        Assert.Empty(_files.Saved);
    }
}
