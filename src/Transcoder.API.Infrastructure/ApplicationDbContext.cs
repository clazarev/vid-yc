using Transcoder.API.Application.Interfaces;
using Transcoder.API.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using MongoDB.EntityFrameworkCore.Extensions;

namespace Transcoder.API.Infrastructure;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options), IApplicationDbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Video>()
            .ToCollection("videos");
    }

    public DbSet<Video> Videos { get; init; }

    public Task<bool> CanConnectAsync(CancellationToken cancellationToken)
    {
        return Database.CanConnectAsync(cancellationToken);
    }
}
