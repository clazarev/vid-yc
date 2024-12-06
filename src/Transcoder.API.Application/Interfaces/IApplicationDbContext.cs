using Transcoder.API.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Transcoder.API.Application.Interfaces;

public interface IApplicationDbContext
{
    public DbSet<Video> Videos { get; }

    public Task<bool> CanConnectAsync(CancellationToken cancellationToken);
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
