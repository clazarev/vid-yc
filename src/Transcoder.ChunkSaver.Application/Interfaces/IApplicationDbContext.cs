using Transcoder.Common.Entities;

using Microsoft.EntityFrameworkCore;

namespace Transcoder.ChunkSaver.Application.Interfaces;

public interface IApplicationDbContext
{
    public DbSet<ProcessedChunk> ProcessedChunks { get; }

    public Task<bool> CanConnectAsync(CancellationToken cancellationToken);
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
