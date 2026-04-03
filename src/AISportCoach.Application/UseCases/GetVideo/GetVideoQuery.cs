using AISportCoach.Domain.Entities;
using MediatR;
namespace AISportCoach.Application.UseCases.GetVideo;
public record GetVideoQuery(Guid VideoId) : IRequest<VideoUpload>;
