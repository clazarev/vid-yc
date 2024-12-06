using Transcoder.Common.Entities;

namespace Transcoder.Composer.Application.Interfaces;

public interface IChunkRepository
{
    Task AddAsync(ProcessedChunk chunk, CancellationToken token);
    Task<List<ProcessedChunk>> GetAllAsync(Guid videoId, CancellationToken token);
    Task<List<ProcessedChunk>> GetAllAsync(Guid videoId, int height, CancellationToken token);
    Task<int> GetTotalAsync(Guid videoId, CancellationToken token);
    Task<int> GetTotalAsync(Guid videoId, int height, CancellationToken token);
    Task DeleteChunks(ProcessedChunk[] chunks, CancellationToken token);

    Task CleanUp(TimeSpan expiry, CancellationToken token);
}
