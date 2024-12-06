using Transcoder.ChunkSaver.Application.Interfaces;
using Transcoder.Common.Entities;

using Microsoft.EntityFrameworkCore;

using MongoDB.EntityFrameworkCore.Extensions;

namespace Transcoder.ChunkSaver.Infrastructure;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options), IApplicationDbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<ProcessedChunk>().ToCollection("processedChunks");
    }

    public required DbSet<ProcessedChunk> ProcessedChunks { get; init; }

    public Task<bool> CanConnectAsync(CancellationToken cancellationToken)
    {
        return Database.CanConnectAsync(cancellationToken);
    }
}
