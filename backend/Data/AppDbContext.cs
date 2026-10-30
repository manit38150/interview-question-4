using Example.InterviewQuestion4.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Example.InterviewQuestion4.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Person> Persons => Set<Person>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var person = modelBuilder.Entity<Person>();
        person.HasKey(p => p.Id);
        person.Property(p => p.Id).ValueGeneratedOnAdd();
        person.Property(p => p.FirstName).HasMaxLength(100).IsRequired();
        person.Property(p => p.LastName).HasMaxLength(100).IsRequired();
        person.Property(p => p.Email).HasMaxLength(254).IsRequired();
        person.Property(p => p.Phone).HasMaxLength(10).IsRequired();
        person.Property(p => p.ProfileBase64).IsRequired();
        person.Property(p => p.ProfileFileName).HasMaxLength(255).IsRequired();
        person.Property(p => p.ProfileContentType).HasMaxLength(10).IsRequired();
        person.Property(p => p.Occupation).HasMaxLength(100).IsRequired();
        person.Property(p => p.Sex).HasMaxLength(50).IsRequired();
    }
}
