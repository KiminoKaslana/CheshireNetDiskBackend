using NetDisk.Api.Models;
using System.Text.RegularExpressions;
using FFMpegCore;
using FFMpegCore.Arguments;
using FFMpegCore.Enums;

namespace NetDisk.Api.Services;

public class SubtitleService : ISubtitleService
{
    private readonly ISettingsService _settingsService;
    private readonly ILogger<SubtitleService> _logger;

    public SubtitleService(ISettingsService settingsService, ILogger<SubtitleService> logger, IConfiguration configuration)
    {
        _settingsService = settingsService;
        _logger = logger;

        // 配置 FFMpegCore 的可执行文件路径
        var ffmpegPath = configuration["FFmpeg:FFmpegPath"] ?? "ffmpeg";
        var ffprobePath = configuration["FFmpeg:FFprobePath"] ?? "ffprobe";

        GlobalFFOptions.Configure(options =>
        {
            options.BinaryFolder = string.IsNullOrEmpty(Path.GetDirectoryName(ffmpegPath))
                ? Environment.CurrentDirectory
                : Path.GetDirectoryName(ffmpegPath)!;
            options.TemporaryFilesFolder = Path.GetTempPath();
        });
    }

    public async Task<SubtitleExtractionResult> ExtractSubtitlesAsync(string relativePath)
    {
        var result = new SubtitleExtractionResult();

        try
        {
            var settings = _settingsService.GetFileStorageSettings();
            var fullPath = GetFullPath(relativePath, settings.RootPath);

            if (!File.Exists(fullPath))
            {
                result.Success = false;
                result.Message = $"未找到文件: {relativePath}";
                return result;
            }

            // 获取媒体文件所在目录和文件名（不含扩展名）
            var mediaDirectory = Path.GetDirectoryName(fullPath)!;
            var mediaFileNameWithoutExtension = Path.GetFileNameWithoutExtension(fullPath);
            var mediaRelativeDirectory = Path.GetDirectoryName(relativePath);

            // 首先检查是否已有字幕文件
            var existingSubtitles = FindExistingSubtitleFiles(mediaDirectory, mediaFileNameWithoutExtension, mediaRelativeDirectory, settings);

            if (existingSubtitles.Count > 0)
            {
                _logger.LogInformation($"在文件 {relativePath} 中发现 {existingSubtitles.Count} 个已存在的字幕文件");
                result.Subtitles = existingSubtitles;
                result.Success = true;
                result.Message = $"找到 {existingSubtitles.Count} 个已存在的字幕文件";
                return result;
            }

            // 如果没有找到已存在的字幕，则提取
            var subtitleTracks = await GetSubtitleTracksAsync(fullPath);

            if (subtitleTracks.Count == 0)
            {
                result.Success = true;
                result.Message = "媒体文件中未发现字幕";
                return result;
            }

            foreach (var track in subtitleTracks)
            {
                var saved = await ExtractAndSaveSubtitleTrackAsync(fullPath, track, mediaDirectory, mediaFileNameWithoutExtension, relativePath, settings);
                if (saved != null)
                {
                    result.Subtitles.Add(saved);
                }
            }

            result.Success = true;
            result.Message = $"已提取 {result.Subtitles.Count} 个字幕流";
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"从文件 {relativePath} 提取字幕时出错");
            result.Success = false;
            result.Message = $"提取字幕时出错: {ex.Message}";
            return result;
        }
    }

    private List<SubtitleTrack> FindExistingSubtitleFiles(
        string mediaDirectory,
        string mediaFileNameWithoutExtension,
        string? mediaRelativeDirectory,
        FileStorageSettings settings)
    {
        var existingSubtitles = new List<SubtitleTrack>();

        try
        {
            // 支持多种字幕格式扩展名
            var supportedExtensions = new[] { ".srt", ".ass", ".ssa", ".vtt", ".sup", ".sub", ".txt" };

            // 获取目录下所有文件
            var allFiles = Directory.GetFiles(mediaDirectory);

            // 查找所有前缀匹配且扩展名为字幕格式的文件
            var candidateFiles = new List<(string FilePath, string Extension, int MatchScore)>();

            foreach (var file in allFiles)
            {
                var fileName = Path.GetFileName(file);
                var fileNameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                var extension = Path.GetExtension(fileName).ToLowerInvariant();

                // 检查扩展名是否为支持的字幕格式
                if (!supportedExtensions.Contains(extension))
                {
                    continue;
                }

                // 检查文件名是否以媒体文件名开头
                if (!fileNameWithoutExt.StartsWith(mediaFileNameWithoutExtension, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // 计算匹配分数：文件名越接近媒体文件名，分数越高
                // 完全匹配（仅扩展名不同）得分最高
                var matchScore = CalculateMatchScore(mediaFileNameWithoutExtension, fileNameWithoutExt);
                candidateFiles.Add((file, extension, matchScore));
            }

            // 如果没有找到匹配的文件，直接返回
            if (candidateFiles.Count == 0)
            {
                return existingSubtitles;
            }

            // 选择匹配程度最高的文件
            // 按匹配分数降序排序，然后按文件名长度升序（同分数时选择更短的）
            var bestMatch = candidateFiles
                .OrderByDescending(f => f.MatchScore)
                .ThenBy(f => Path.GetFileNameWithoutExtension(f.FilePath).Length)
                .First();

            var subtitleFileName = Path.GetFileName(bestMatch.FilePath);
            var codec = GetCodecFromExtension(bestMatch.Extension);

            // 计算相对路径
            var subtitleRelativePath = string.IsNullOrEmpty(mediaRelativeDirectory)
                ? subtitleFileName
                : Path.Combine(mediaRelativeDirectory, subtitleFileName);

            var track = new SubtitleTrack
            {
                Index = 0,
                Language = ExtractLanguageFromFileName(subtitleFileName, mediaFileNameWithoutExtension),
                Codec = codec,
                FilePath = subtitleRelativePath.Replace("\\", "/"),
                HttpUrl = GetHttpUrl(subtitleRelativePath, settings)
            };

            existingSubtitles.Add(track);

            _logger.LogInformation($"找到最佳匹配字幕文件: {subtitleFileName}，匹配分数: {bestMatch.MatchScore}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, $"在目录 {mediaDirectory} 中搜索现有字幕文件时出错");
        }

        return existingSubtitles;
    }

    private int CalculateMatchScore(string mediaFileName, string subtitleFileName)
    {
        // 完全匹配（仅扩展名不同）得分最高
        if (string.Equals(mediaFileName, subtitleFileName, StringComparison.OrdinalIgnoreCase))
        {
            return 1000;
        }

        // 字幕文件名包含媒体文件名作为前缀
        // 分数根据额外字符的数量递减
        var extraChars = subtitleFileName.Length - mediaFileName.Length;
        return 1000 - extraChars;
    }

    private string? ExtractLanguageFromFileName(string subtitleFileName, string mediaFileNameWithoutExtension)
    {
        // 尝试从字幕文件名中提取语言信息
        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(subtitleFileName);
        
        // 移除媒体文件名前缀
        if (fileNameWithoutExt.StartsWith(mediaFileNameWithoutExtension, StringComparison.OrdinalIgnoreCase))
        {
            var suffix = fileNameWithoutExt.Substring(mediaFileNameWithoutExtension.Length);
            
            // 常见的语言标识模式
            var languagePatterns = new[]
            {
                @"\[([^\]]+)\]",  // [chi], [eng], [jpn] 等
                @"\.([a-z]{2,3})$",  // .zh, .en, .ja 等
                @"_([a-z]{2,3})$",  // _zh, _en, _ja 等
                @"-([a-z]{2,3})$"   // -zh, -en, -ja 等
            };

            foreach (var pattern in languagePatterns)
            {
                var match = Regex.Match(suffix, pattern, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }
            }
        }

        return null;
    }

    private async Task<List<SubtitleTrack>> GetSubtitleTracksAsync(string filePath)
    {
        var tracks = new List<SubtitleTrack>();

        try
        {
            var mediaInfo = await FFProbe.AnalyseAsync(filePath);

            foreach (var stream in mediaInfo.SubtitleStreams)
            {
                var track = new SubtitleTrack
                {
                    Index = stream.Index,
                    Codec = stream.CodecName,
                    Language = stream.Language,
                };

                tracks.Add(track);
            }

            _logger.LogInformation($"在文件 {filePath} 中发现 {tracks.Count} 个字幕流");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"获取文件 {filePath} 的字幕流时出错");
        }

        return tracks;
    }

    private async Task<SubtitleTrack?> ExtractAndSaveSubtitleTrackAsync(
        string mediaFilePath,
        SubtitleTrack track,
        string outputDirectory,
        string mediaFileNameWithoutExtension,
        string mediaRelativePath,
        FileStorageSettings settings)
    {
        try
        {
            // 根据字幕格式确定文件扩展名
            var extension = GetSubtitleExtension(track.Codec);

            // 构建字幕文件名：媒体名[Subtitle][语言][轨道索引].{扩展名}
            var languagePart = !string.IsNullOrEmpty(track.Language) ? $"[{track.Language}]" : "";
            var subtitleFileName = $"{mediaFileNameWithoutExtension}[Subtitle]{languagePart}[T{track.Index}]{extension}";
            var subtitleFilePath = Path.Combine(outputDirectory, subtitleFileName);

            // 使用 FFMpegCore 提取字幕
            var success = await FFMpegArguments
                .FromFileInput(mediaFilePath)
                .OutputToFile(subtitleFilePath, true, options => options
                    .SelectStream(track.Index, 0)
                    .CopyChannel(Channel.Subtitle))
                .ProcessAsynchronously();

            if (!success)
            {
                _logger.LogWarning($"FFmpeg 提取字幕流 {track.Index} 失败");
                return null;
            }

            if (!File.Exists(subtitleFilePath))
            {
                _logger.LogWarning($"未创建字幕文件: {subtitleFilePath}");
                return null;
            }

            var mediaDirectory = Path.GetDirectoryName(mediaRelativePath);
            var subtitleRelativePath = string.IsNullOrEmpty(mediaDirectory)
                ? subtitleFileName
                : Path.Combine(mediaDirectory, subtitleFileName);

            var httpUrl = GetHttpUrl(subtitleRelativePath, settings);

            track.FilePath = subtitleRelativePath.Replace("\\", "/");
            track.HttpUrl = httpUrl;

            _logger.LogInformation($"已将字幕提取到 {subtitleFilePath}");

            return track;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"从文件 {mediaFilePath} 提取字幕流 {track.Index} 时出错");
            return null;
        }
    }

    private string GetSubtitleExtension(string? codec)
    {
        if (string.IsNullOrEmpty(codec))
        {
            return ".srt";
        }

        return codec.ToLowerInvariant() switch
        {
            "subrip" or "srt" => ".srt",
            "ass" => ".ass",
            "ssa" => ".ssa",
            "webvtt" or "vtt" => ".vtt",
            "mov_text" => ".srt",
            "hdmv_pgs_subtitle" or "pgs" => ".sup",
            "dvd_subtitle" or "dvdsub" => ".sub",
            "dvb_subtitle" => ".sub",
            "microdvd" => ".sub",
            "subviewer" => ".sub",
            "text" => ".txt",
            _ => ".srt"
        };
    }

    private string GetFullPath(string relativePath, string rootPath)
    {
        var normalizedRelativePath = relativePath.Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(rootPath, normalizedRelativePath));

        var normalizedRootPath = Path.GetFullPath(rootPath);
        if (!fullPath.StartsWith(normalizedRootPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Access denied: Path is outside root directory");
        }

        return fullPath;
    }

    private string GetHttpUrl(string relativePath, FileStorageSettings settings)
    {
        var httpBaseUrl = settings.HttpBaseUrl;

        if (string.IsNullOrEmpty(httpBaseUrl))
        {
            throw new InvalidOperationException("FileStorage:HttpBaseUrl not configured");
        }

        var urlPath = relativePath.Replace("\\", "/");
        return $"{httpBaseUrl.TrimEnd('/')}/{urlPath.TrimStart('/')}";
    }

    private string GetCodecFromExtension(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".srt" => "srt",
            ".ass" => "ass",
            ".ssa" => "ssa",
            ".vtt" => "webvtt",
            ".sup" => "pgs",
            ".sub" => "dvd_subtitle",
            ".txt" => "text",
            _ => "unknown"
        };
    }
}
