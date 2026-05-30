using AISportCoach.Domain.Enums;

namespace AISportCoach.Application.Models;

public record ReportChunk(
    ChunkType ChunkType,
    Guid ChunkId,
    Guid ReportId,
    string Text);
