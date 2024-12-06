using Transcoder.Common.Entities;
using Transcoder.Composer.Domain;

using Microsoft.EntityFrameworkCore;

namespace Transcoder.Composer.Application.Interfaces;

public interface IApplicationDbContext
{
    public DbSet<ProcessedChunk> ProcessedChunks { get; }
    public DbSet<VideoProfile> VideoProfiles { get; }

    public Task<bool> CanConnectAsync(CancellationToken cancellationToken);
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
