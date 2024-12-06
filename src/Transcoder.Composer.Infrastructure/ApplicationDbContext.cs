using Transcoder.Common.Entities;
using Transcoder.Composer.Application.Interfaces;
using Transcoder.Composer.Domain;

using Microsoft.EntityFrameworkCore;

using MongoDB.EntityFrameworkCore.Extensions;

namespace Transcoder.Composer.Infrastructure;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options), IApplicationDbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<ProcessedChunk>().ToCollection("processedChunks");
        modelBuilder.Entity<VideoProfile>().ToCollection("videoProfiles");
    }

    public DbSet<ProcessedChunk> ProcessedChunks { get; init; }
    public DbSet<VideoProfile> VideoProfiles { get; init; }

    public Task<bool> CanConnectAsync(CancellationToken cancellationToken)
    {
        return Database.CanConnectAsync(cancellationToken);
    }
}
