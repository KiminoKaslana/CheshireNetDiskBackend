using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetDisk.Api.Models;
using NetDisk.Api.Services;

namespace NetDisk.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class SettingsController : ControllerBase
{
    private readonly ISettingsService _settingsService;
    private readonly ILogger<SettingsController> _logger;

    public SettingsController(ISettingsService settingsService, ILogger<SettingsController> logger)
    {
        _settingsService = settingsService;
        _logger = logger;
    }

    /// <summary>
    /// 获取当前文件存储配置
    /// </summary>
    /// <returns>文件存储配置</returns>
    [HttpGet("storage")]
    [ProducesResponseType(typeof(ApiResponse<FileStorageSettings>), 200)]
    public IActionResult GetStorageSettings()
    {
        try
        {
            var settings = _settingsService.GetFileStorageSettings();
            return Ok(new ApiResponse<FileStorageSettings>
            {
                Success = true,
                Message = "Success",
                Data = settings
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting storage settings");
            return StatusCode(500, new ApiResponse<FileStorageSettings>
            {
                Success = false,
                Message = "Internal server error"
            });
        }
    }

    /// <summary>
    /// 更新文件存储配置
    /// </summary>
    /// <param name="request">更新请求，包含新的根目录和 HTTP URL</param>
    /// <returns>更新结果</returns>
    [HttpPut("storage")]
    [ProducesResponseType(typeof(ApiResponse<FileStorageSettings>), 200)]
    public async Task<IActionResult> UpdateStorageSettings([FromBody] UpdateSettingsRequest request)
    {
        try
        {
            // 验证输入
            if (request.RootPath != null)
            {
                if (string.IsNullOrWhiteSpace(request.RootPath))
                {
                    return BadRequest(new ApiResponse<FileStorageSettings>
                    {
                        Success = false,
                        Message = "RootPath cannot be empty"
                    });
                }

                // 验证目录是否存在或可以创建
                try
                {
                    if (!Directory.Exists(request.RootPath))
                    {
                        Directory.CreateDirectory(request.RootPath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Cannot create directory: {Path}", request.RootPath);
                    return BadRequest(new ApiResponse<FileStorageSettings>
                    {
                        Success = false,
                        Message = $"Cannot create or access directory: {request.RootPath}"
                    });
                }
            }

            if (request.HttpBaseUrl != null && string.IsNullOrWhiteSpace(request.HttpBaseUrl))
            {
                return BadRequest(new ApiResponse<FileStorageSettings>
                {
                    Success = false,
                    Message = "HttpBaseUrl cannot be empty"
                });
            }

            // 更新配置
            var success = await _settingsService.UpdateFileStorageSettingsAsync(
                request.RootPath, 
                request.HttpBaseUrl);

            if (!success)
            {
                return StatusCode(500, new ApiResponse<FileStorageSettings>
                {
                    Success = false,
                    Message = "Failed to update settings"
                });
            }

            // 返回更新后的配置
            var updatedSettings = _settingsService.GetFileStorageSettings();
            
            _logger.LogInformation(
                "Storage settings updated by {User}. RootPath: {RootPath}, HttpBaseUrl: {HttpBaseUrl}",
                User.Identity?.Name,
                updatedSettings.RootPath,
                updatedSettings.HttpBaseUrl);

            return Ok(new ApiResponse<FileStorageSettings>
            {
                Success = true,
                Message = "Settings updated successfully",
                Data = updatedSettings
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating storage settings");
            return StatusCode(500, new ApiResponse<FileStorageSettings>
            {
                Success = false,
                Message = "Internal server error"
            });
        }
    }
}
