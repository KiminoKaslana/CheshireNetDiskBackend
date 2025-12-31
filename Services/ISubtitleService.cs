using NetDisk.Api.Models;

namespace NetDisk.Api.Services;

public interface ISubtitleService
{
    Task<SubtitleExtractionResult> ExtractSubtitlesAsync(string filePath);
}
