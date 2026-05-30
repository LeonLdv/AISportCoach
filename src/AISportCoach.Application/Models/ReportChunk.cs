using AISportCoach.Domain.Enums;

namespace AISportCoach.Application.Models;

/// <summary>
/// A chunk of a coaching report, representing a discrete semantic unit.
/// </summary>
/// <param name="ChunkType">The type of content in this chunk.</param>
/// <param name="ChunkId">ID of the source entity; equals ReportId for Summary chunks.</param>
/// <param name="ReportId">ID of the coaching report this chunk belongs to.</param>
/// <param name="Text">Chunk content, max 2048 characters (truncated + warning logged if exceeded).</param>
public record ReportChunk(
    ChunkType ChunkType,
    Guid ChunkId,
    Guid ReportId,
    string Text);
