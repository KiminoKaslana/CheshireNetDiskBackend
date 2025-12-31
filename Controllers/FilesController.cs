using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NetDisk.Api.Models;
using NetDisk.Api.Services;

namespace NetDisk.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FilesController : ControllerBase
{
    private readonly IFileService _fileService;
    private readonly ISubtitleService _subtitleService;
    private readonly ILogger<FilesController> _logger;

    public FilesController(IFileService fileService, ISubtitleService subtitleService, ILogger<FilesController> logger)
    {
        _fileService = fileService;
        _subtitleService = subtitleService;
        _logger = logger;
    }

    /// <summary>
    /// 获取目录内容
    /// </summary>
    /// <param name="path">相对路径，例如：folder1/subfolder</param>
    /// <returns>目录内容，包括文件和子文件夹列表</returns>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<DirectoryContent>), 200)]
    public IActionResult GetFiles([FromQuery] string path = "")
    {
        try
        {
            var content = _fileService.GetDirectoryContent(path);
            return Ok(new ApiResponse<DirectoryContent>
            {
                Success = true,
                Message = "Success",
                Data = content
            });
        }
        catch (DirectoryNotFoundException ex)
        {
            _logger.LogWarning(ex, "Directory not found: {Path}", path);
            return NotFound(new ApiResponse<DirectoryContent>
            {
                Success = false,
                Message = ex.Message
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access attempt: {Path}", path);
            return StatusCode(403, new ApiResponse<DirectoryContent>
            {
                Success = false,
                Message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting directory content: {Path}", path);
            return StatusCode(500, new ApiResponse<DirectoryContent>
            {
                Success = false,
                Message = "Internal server error"
            });
        }
    }

    /// <summary>
    /// 获取文件或文件夹的 HTTP URL
    /// </summary>
    /// <param name="path">相对路径</param>
    /// <returns>HTTP URL</returns>
    [HttpGet("url")]
    [ProducesResponseType(typeof(ApiResponse<string>), 200)]
    public IActionResult GetFileUrl([FromQuery] string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return BadRequest(new ApiResponse<string>
                {
                    Success = false,
                    Message = "Path parameter is required"
                });
            }

            var url = _fileService.GetHttpUrl(path);
            return Ok(new ApiResponse<string>
            {
                Success = true,
                Message = "Success",
                Data = url
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating URL for path: {Path}", path);
            return StatusCode(500, new ApiResponse<string>
            {
                Success = false,
                Message = "Internal server error"
            });
        }
    }

    /// <summary>
    /// 提取媒体文件中的字幕
    /// </summary>
    /// <param name="path">媒体文件的相对路径</param>
    /// <returns>字幕提取结果，包含所有字幕轨道的 URL</returns>
    [HttpGet("subtitles")]
    [ProducesResponseType(typeof(ApiResponse<List<SubtitleTrack>>), 200)]
    public async Task<IActionResult> ExtractSubtitles([FromQuery] string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return BadRequest(new ApiResponse<List<SubtitleTrack>>
                {
                    Success = false,
                    Message = "Path parameter is required"
                });
            }

            var result = await _subtitleService.ExtractSubtitlesAsync(path);
            
            if (!result.Success)
            {
                return BadRequest(new ApiResponse<List<SubtitleTrack>>
                {
                    Success = false,
                    Message = result.Message
                });
            }

            return Ok(new ApiResponse<List<SubtitleTrack>>
            {
                Success = true,
                Message = result.Message,
                Data = result.Subtitles
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting subtitles from: {Path}", path);
            return StatusCode(500, new ApiResponse<List<SubtitleTrack>>
            {
                Success = false,
                Message = "Internal server error"
            });
        }
    }
}
