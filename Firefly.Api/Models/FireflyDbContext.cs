using Firefly.Api.Models.DatabaseModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Firefly.Api.Models;

public class FireflyDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public FireflyDbContext(DbContextOptions<FireflyDbContext> options) : base(options)
    {
    }

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasPostgresExtension("pg_trgm");

        modelBuilder.Entity<RefreshToken>(b =>
        {
            b.HasKey(r => r.Id);
            b.HasIndex(r => r.Token).IsUnique();
            b.HasOne(r => r.User)
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}