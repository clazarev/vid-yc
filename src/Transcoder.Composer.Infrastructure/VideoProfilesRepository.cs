using Transcoder.Composer.Application.Interfaces;
using Transcoder.Composer.Domain;

using Microsoft.EntityFrameworkCore;

namespace Transcoder.Composer.Infrastructure;
public class VideoProfilesRepository(IApplicationDbContext context) : IVideoProfilesRepository
{
    public async Task AddAsync(VideoProfile profile, CancellationToken token)
    {
        await context.VideoProfiles.AddAsync(profile, token);
        await context.SaveChangesAsync(token);
    }

    public Task<List<VideoProfile>> GetAllAsync(Guid videoId, CancellationToken token)
    {
        return context.VideoProfiles.Where(x => x.VideoId == videoId).ToListAsync(token);
    }
}
