using Transcoder.Common.Entities;
using Transcoder.Composer.Application.Interfaces;

using Microsoft.EntityFrameworkCore;

namespace Transcoder.Composer.Infrastructure;

public class ChunkRepository(IApplicationDbContext context) : IChunkRepository
{
    public async Task AddAsync(ProcessedChunk chunk, CancellationToken token)
    {
        await context.ProcessedChunks.AddAsync(chunk, token);
        await  context.SaveChangesAsync(token);
    }

    public async Task DeleteChunks(ProcessedChunk[] chunks, CancellationToken token)
    {
        context.ProcessedChunks.RemoveRange(chunks);
        await context.SaveChangesAsync(token);
    }

    public async Task CleanUp(TimeSpan expiry, CancellationToken token)
    {
        //context.ProcessedChunks.Where(row => row.CreatedAt.HasValue && row.CreatedAt < DateTime.Now - expiry);
        await context.SaveChangesAsync(token);
    }

    public Task<List<ProcessedChunk>> GetAllAsync(Guid videoId, CancellationToken token)
    {
        return context.ProcessedChunks.Where(x => x.VideoId == videoId).ToListAsync(token);
    }
    public Task<List<ProcessedChunk>> GetAllAsync(Guid videoId, int height, CancellationToken token)
    {
        return context.ProcessedChunks.Where(x => x.VideoId == videoId && x.Height == height).ToListAsync(token);
    }

    public Task<int> GetTotalAsync(Guid videoId, CancellationToken token)
    {
        return context.ProcessedChunks.CountAsync(x => x.VideoId == videoId, token);
    }

    public Task<int> GetTotalAsync(Guid videoId, int height, CancellationToken token)
    {
        return context.ProcessedChunks.CountAsync(x => x.VideoId == videoId && x.Height == height, token);
    }
}
