using Transcoder.Common.Entities;

namespace Transcoder.ChunkSaver.Application.Interfaces;

public interface IChunkRepository
{
    Task AddAsync(ProcessedChunk chunk, CancellationToken token);

    Task<int> GetTotalAsync(Guid videoId, int height, CancellationToken token);
}
