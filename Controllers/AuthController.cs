using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NetDisk.Api.Models;
using NetDisk.Api.Services;

namespace NetDisk.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    /// <summary>
    /// 用户登录
    /// </summary>
    /// <param name="request">登录请求，包含用户名和密码</param>
    /// <returns>JWT token</returns>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), 200)]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new ApiResponse<LoginResponse>
                {
                    Success = false,
                    Message = "Username and password are required"
                });
            }

            var result = _authService.Login(request.Username, request.Password);

            if (result == null)
            {
                return Unauthorized(new ApiResponse<LoginResponse>
                {
                    Success = false,
                    Message = "Invalid username or password"
                });
            }

            return Ok(new ApiResponse<LoginResponse>
            {
                Success = true,
                Message = "Login successful",
                Data = result
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login for user: {Username}", request.Username);
            return StatusCode(500, new ApiResponse<LoginResponse>
            {
                Success = false,
                Message = "Internal server error"
            });
        }
    }

    /// <summary>
    /// 验证 token（供 nginx auth_request 使用）
    /// </summary>
    /// <returns>200 表示验证成功，401 表示验证失败</returns>
    [HttpGet("verify")]
    [AllowAnonymous]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    public IActionResult Verify()
    {
        try
        {
            // 从 Authorization header 获取 token
            var authHeader = Request.Headers["Authorization"].FirstOrDefault();
            
            if (string.IsNullOrWhiteSpace(authHeader))
            {
                return Unauthorized();
            }

            // 移除 "Bearer " 前缀
            var token = authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? authHeader.Substring("Bearer ".Length).Trim()
                : authHeader;

            if (!_authService.ValidateToken(token))
            {
                return Unauthorized();
            }

            // 检查原始请求方法
            var originalMethod = Request.Headers["X-Original-Method"].FirstOrDefault();
            
            // 对于非 GET 请求，需要管理员权限
            if (!string.IsNullOrWhiteSpace(originalMethod) && 
                !originalMethod.Equals("GET", StringComparison.OrdinalIgnoreCase))
            {
                if (!_authService.IsUserAdmin(token))
                {
                    _logger.LogWarning("Non-admin user attempted {Method} request", originalMethod);
                    return Unauthorized();
                }
            }

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during token verification");
            return Unauthorized();
        }
    }

    /// <summary>
    /// 验证当前用户是否已认证（需要 Bearer token）
    /// </summary>
    /// <returns>用户信息</returns>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    public IActionResult GetCurrentUser()
    {
        var username = User.Identity?.Name;
        
        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "Authenticated",
            Data = new { Username = username }
        });
    }
}
