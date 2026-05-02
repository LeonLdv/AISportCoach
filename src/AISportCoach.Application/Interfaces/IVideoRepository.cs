using AISportCoach.Domain.Entities;
namespace AISportCoach.Application.Interfaces;
public interface IVideoRepository
{
    Task<VideoUpload?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<VideoUpload?> GetByIdAndUserAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task<IEnumerable<VideoUpload>> GetByUserAsync(Guid userId, CancellationToken ct = default);
    Task AddAsync(VideoUpload video, CancellationToken ct = default);
    Task UpdateAsync(VideoUpload video, CancellationToken ct = default);
}
