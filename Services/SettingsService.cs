using NetDisk.Api.Models;
using System.Text.Json;

namespace NetDisk.Api.Services;

public interface ISettingsService
{
    FileStorageSettings GetFileStorageSettings();
    Task<bool> UpdateFileStorageSettingsAsync(string? rootPath, string? httpBaseUrl);
}

public class SettingsService : ISettingsService
{
    private readonly IConfiguration _configuration;
    private readonly string _settingsFilePath;
    private FileStorageSettings _cachedSettings;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public SettingsService(IConfiguration configuration, IWebHostEnvironment environment)
    {
        _configuration = configuration;
        _settingsFilePath = Path.Combine(environment.ContentRootPath, "appsettings.json");
        _cachedSettings = new FileStorageSettings
        {
            RootPath = configuration["FileStorage:RootPath"] ?? "",
            HttpBaseUrl = configuration["FileStorage:HttpBaseUrl"] ?? ""
        };
    }

    public FileStorageSettings GetFileStorageSettings()
    {
        return new FileStorageSettings
        {
            RootPath = _cachedSettings.RootPath,
            HttpBaseUrl = _cachedSettings.HttpBaseUrl
        };
    }

    public async Task<bool> UpdateFileStorageSettingsAsync(string? rootPath, string? httpBaseUrl)
    {
        await _lock.WaitAsync();
        try
        {
            // 读取现有配置文件
            var jsonString = await File.ReadAllTextAsync(_settingsFilePath);
            var jsonDoc = JsonDocument.Parse(jsonString);
            
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartObject();

                foreach (var property in jsonDoc.RootElement.EnumerateObject())
                {
                    if (property.Name == "FileStorage")
                    {
                        writer.WritePropertyName("FileStorage");
                        writer.WriteStartObject();
                        
                        // 更新或保留现有值
                        writer.WriteString("RootPath", rootPath ?? _cachedSettings.RootPath);
                        writer.WriteString("HttpBaseUrl", httpBaseUrl ?? _cachedSettings.HttpBaseUrl);
                        
                        writer.WriteEndObject();
                    }
                    else
                    {
                        property.WriteTo(writer);
                    }
                }

                writer.WriteEndObject();
            }

            // 保存到文件
            var updatedJson = System.Text.Encoding.UTF8.GetString(stream.ToArray());
            await File.WriteAllTextAsync(_settingsFilePath, updatedJson);

            // 更新缓存
            if (rootPath != null) _cachedSettings.RootPath = rootPath;
            if (httpBaseUrl != null) _cachedSettings.HttpBaseUrl = httpBaseUrl;

            return true;
        }
        catch (Exception)
        {
            return false;
        }
        finally
        {
            _lock.Release();
        }
    }
}
