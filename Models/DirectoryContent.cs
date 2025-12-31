namespace NetDisk.Api.Models;

public class DirectoryContent
{
    public string CurrentPath { get; set; } = string.Empty;
    public string? ParentPath { get; set; }
    public List<FileItem> Items { get; set; } = new();
}
