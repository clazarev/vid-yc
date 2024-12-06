using Transcoder.ChunkSaver.Application.Interfaces;
using Transcoder.Common.Entities;

using Microsoft.EntityFrameworkCore;

namespace Transcoder.ChunkSaver.Infrastructure;

public class ChunkRepository(IApplicationDbContext context) : IChunkRepository
{
    public async Task AddAsync(ProcessedChunk chunk, CancellationToken token)
    {
        await context.ProcessedChunks.AddAsync(chunk, token);
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

    public Task<int> GetTotalAsync(Guid videoId, int height, CancellationToken token)
    {
        return context.ProcessedChunks.CountAsync(x => x.VideoId == videoId && x.Height == height, token);
    }
}
