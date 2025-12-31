using NetDisk.Api.Models;

namespace NetDisk.Api.Services;

public class FileService : IFileService
{
    private readonly ISettingsService _settingsService;

    public FileService(ISettingsService settingsService)
    {
        _settingsService = settingsService;

        // 确保根目录存在
        var settings = _settingsService.GetFileStorageSettings();
        if (!string.IsNullOrEmpty(settings.RootPath) && !Directory.Exists(settings.RootPath))
        {
            Directory.CreateDirectory(settings.RootPath);
        }
    }

    public DirectoryContent GetDirectoryContent(string relativePath)
    {
        // 获取当前配置
        var settings = _settingsService.GetFileStorageSettings();
        var rootPath = settings.RootPath;

        if (string.IsNullOrEmpty(rootPath))
        {
            throw new InvalidOperationException("FileStorage:RootPath not configured");
        }

        // 清理和验证路径
        relativePath = CleanPath(relativePath);
        var fullPath = Path.Combine(rootPath, relativePath);

        // 安全检查：确保路径在根目录内
        var normalizedFullPath = Path.GetFullPath(fullPath);
        var normalizedRootPath = Path.GetFullPath(rootPath);
        
        if (!normalizedFullPath.StartsWith(normalizedRootPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Access denied: Path is outside root directory");
        }

        if (!Directory.Exists(normalizedFullPath))
        {
            throw new DirectoryNotFoundException($"Directory not found: {relativePath}");
        }

        var result = new DirectoryContent
        {
            CurrentPath = relativePath,
            ParentPath = GetParentPath(relativePath)
        };

        var directoryInfo = new DirectoryInfo(normalizedFullPath);

        // 获取子目录
        foreach (var dir in directoryInfo.GetDirectories())
        {
            var itemPath = string.IsNullOrEmpty(relativePath) 
                ? dir.Name 
                : Path.Combine(relativePath, dir.Name);

            result.Items.Add(new FileItem
            {
                Name = dir.Name,
                Path = itemPath.Replace("\\", "/"),
                HttpUrl = GetHttpUrl(itemPath),
                IsDirectory = true,
                Size = 0,
                LastModified = dir.LastWriteTime
            });
        }

        // 获取文件
        foreach (var file in directoryInfo.GetFiles())
        {
            var itemPath = string.IsNullOrEmpty(relativePath) 
                ? file.Name 
                : Path.Combine(relativePath, file.Name);

            result.Items.Add(new FileItem
            {
                Name = file.Name,
                Path = itemPath.Replace("\\", "/"),
                HttpUrl = GetHttpUrl(itemPath),
                IsDirectory = false,
                Size = file.Length,
                LastModified = file.LastWriteTime
            });
        }

        // 排序：文件夹在前，然后按名称排序
        result.Items = result.Items
            .OrderByDescending(x => x.IsDirectory)
            .ThenBy(x => x.Name)
            .ToList();

        return result;
    }

    public string GetHttpUrl(string relativePath)
    {
        var settings = _settingsService.GetFileStorageSettings();
        var httpBaseUrl = settings.HttpBaseUrl;

        if (string.IsNullOrEmpty(httpBaseUrl))
        {
            throw new InvalidOperationException("FileStorage:HttpBaseUrl not configured");
        }

        relativePath = CleanPath(relativePath);
        var urlPath = relativePath.Replace("\\", "/");
        return $"{httpBaseUrl.TrimEnd('/')}/{urlPath.TrimStart('/')}";
    }

    private string CleanPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        // 移除开头的斜杠
        path = path.TrimStart('/', '\\');

        // 规范化路径分隔符
        path = path.Replace('/', Path.DirectorySeparatorChar);

        return path;
    }

    private string? GetParentPath(string currentPath)
    {
        if (string.IsNullOrEmpty(currentPath))
            return null;

        var parent = Path.GetDirectoryName(currentPath);
        if (string.IsNullOrEmpty(parent))
            return string.Empty;

        return parent.Replace("\\", "/");
    }
}
