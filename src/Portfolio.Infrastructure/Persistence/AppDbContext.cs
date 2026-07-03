using Microsoft.EntityFrameworkCore;
using Portfolio.Domain.Entities;

namespace Portfolio.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Bio> Bios => Set<Bio>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectMedia> ProjectMedia => Set<ProjectMedia>();
    public DbSet<Experience> Experiences => Set<Experience>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<Testimonial> Testimonials => Set<Testimonial>();
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<ProjectMedia>()
            .HasOne(m => m.Project)
            .WithMany()
            .HasForeignKey(m => m.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        // Hot lookup paths get indexes so they resolve in SQL instead of table scans.
        modelBuilder.Entity<Project>().HasIndex(p => p.Slug).IsUnique();
        modelBuilder.Entity<AdminUser>().HasIndex(u => u.Username).IsUnique();
        modelBuilder.Entity<ProjectMedia>().HasIndex(m => new { m.ProjectId, m.SortOrder });
        modelBuilder.Entity<ProjectMedia>().HasIndex(m => m.ContentHash);

        // Bound every string column: without a limit, all of these map to unbounded Postgres `text`,
        // letting the admin form (or any future public form) persist multi-MB payloads per field that
        // then get rendered into cached public pages.
        modelBuilder.Entity<Bio>(b =>
        {
            b.Property(x => x.FullName).HasMaxLength(200);
            b.Property(x => x.Headline).HasMaxLength(300);
            b.Property(x => x.Tagline).HasMaxLength(500);
            b.Property(x => x.About).HasMaxLength(10_000);
            b.Property(x => x.Location).HasMaxLength(200);
            b.Property(x => x.YearsExperience).HasMaxLength(20);
            b.Property(x => x.Email).HasMaxLength(320);
            b.Property(x => x.Phone).HasMaxLength(30);
            b.Property(x => x.LinkedIn).HasMaxLength(500);
            b.Property(x => x.GitHub).HasMaxLength(500);
            b.Property(x => x.Website).HasMaxLength(500);
            b.Property(x => x.ResumeUrl).HasMaxLength(500);
            b.Property(x => x.ProfileImageUrl).HasMaxLength(500);
        });

        modelBuilder.Entity<Project>(p =>
        {
            p.Property(x => x.Title).HasMaxLength(200);
            p.Property(x => x.Slug).HasMaxLength(200);
            p.Property(x => x.Summary).HasMaxLength(500);
            p.Property(x => x.Description).HasMaxLength(10_000);
            p.Property(x => x.Category).HasMaxLength(100);
            p.Property(x => x.TechStack).HasMaxLength(500);
            p.Property(x => x.Timeline).HasMaxLength(100);
            p.Property(x => x.ImageUrl).HasMaxLength(500);
            p.Property(x => x.ProjectUrl).HasMaxLength(500);
            p.Property(x => x.GitHubUrl).HasMaxLength(500);
            p.Property(x => x.Challenge).HasMaxLength(10_000);
            p.Property(x => x.Solution).HasMaxLength(10_000);
            p.Property(x => x.Outcome).HasMaxLength(10_000);
            p.Property(x => x.MyRole).HasMaxLength(200);
        });

        modelBuilder.Entity<ProjectMedia>(m =>
        {
            m.Property(x => x.MediaType).HasMaxLength(20);
            m.Property(x => x.Url).HasMaxLength(1000);
            m.Property(x => x.Caption).HasMaxLength(500);
            m.Property(x => x.ContentHash).HasMaxLength(64);
        });

        modelBuilder.Entity<Experience>(e =>
        {
            e.Property(x => x.Role).HasMaxLength(200);
            e.Property(x => x.Company).HasMaxLength(200);
            e.Property(x => x.EmploymentType).HasMaxLength(100);
            e.Property(x => x.Location).HasMaxLength(200);
            e.Property(x => x.Period).HasMaxLength(100);
            e.Property(x => x.Description).HasMaxLength(5_000);
        });

        modelBuilder.Entity<Service>(s =>
        {
            s.Property(x => x.Title).HasMaxLength(200);
            s.Property(x => x.Description).HasMaxLength(2_000);
            s.Property(x => x.Icon).HasMaxLength(20);
        });

        modelBuilder.Entity<Skill>(s =>
        {
            s.Property(x => x.Name).HasMaxLength(100);
            s.Property(x => x.Category).HasMaxLength(100);
        });

        modelBuilder.Entity<Testimonial>(t =>
        {
            t.Property(x => x.Author).HasMaxLength(200);
            t.Property(x => x.AuthorTitle).HasMaxLength(300);
            t.Property(x => x.Quote).HasMaxLength(5_000);
        });

        modelBuilder.Entity<AdminUser>(u =>
        {
            u.Property(x => x.Username).HasMaxLength(100);
            u.Property(x => x.PasswordHash).HasMaxLength(300);
            u.Property(x => x.SecurityStamp).HasMaxLength(100);
        });
    }
}
