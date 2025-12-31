namespace NetDisk.Api.Models;

public class UpdateSettingsRequest
{
    public string? RootPath { get; set; }
    public string? HttpBaseUrl { get; set; }
}
