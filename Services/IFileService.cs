using NetDisk.Api.Models;

namespace NetDisk.Api.Services;

public interface IFileService
{
    DirectoryContent GetDirectoryContent(string relativePath);
    string GetHttpUrl(string relativePath);
}
