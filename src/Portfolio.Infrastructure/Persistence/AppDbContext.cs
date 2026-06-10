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
    }
}
