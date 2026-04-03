using AISportCoach.Application.Interfaces;
using AISportCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AISportCoach.Infrastructure.Persistence.Repositories;

public class VideoRepository(AppDbContext db) : IVideoRepository
{
    public Task<VideoUpload?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => db.VideoUploads.FirstOrDefaultAsync(v => v.Id == id, ct);

    public async Task AddAsync(VideoUpload video, CancellationToken ct = default)
    {
        db.VideoUploads.Add(video);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(VideoUpload video, CancellationToken ct = default)
    {
        db.VideoUploads.Update(video);
        await db.SaveChangesAsync(ct);
    }
}
