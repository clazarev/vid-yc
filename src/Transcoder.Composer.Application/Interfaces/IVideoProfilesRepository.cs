using Transcoder.Composer.Domain;

namespace Transcoder.Composer.Application.Interfaces;
public interface IVideoProfilesRepository
{
    Task AddAsync(VideoProfile profile, CancellationToken token);
    Task<List<VideoProfile>> GetAllAsync(Guid videoId, CancellationToken token);
}
