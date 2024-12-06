using Transcoder.API.Domain.Entities;

namespace Transcoder.API.Application.Interfaces;

public interface IVideoService
{
    Task<Video> AddForProcessingAsync(Guid videoId, string sourcePath, string playlist, CancellationToken cancellationToken);
    Task<Video?> GetAsync(Guid id, CancellationToken cancellationToken);
}
