namespace NetDisk.Api.Models;

public class SubtitleExtractionResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public List<SubtitleTrack> Subtitles { get; set; } = new();
}

public class SubtitleTrack
{
    public int Index { get; set; }
    public string? Language { get; set; }
    public string? Codec { get; set; }
    public string? HttpUrl { get; set; }
    public string? FilePath { get; set; }
}
