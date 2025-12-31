using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetDisk.Api.Models;
using NetDisk.Api.Services;
using NetDisk.Api.Filters;

namespace NetDisk.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UserManagementController : ControllerBase
{
    private readonly MongoUserStore _userStore;
    private readonly ILogger<UserManagementController> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IWebHostEnvironment _environment;

    public UserManagementController(
        IUserStore userStore,
        ILogger<UserManagementController> logger,
        ILoggerFactory loggerFactory,
        IWebHostEnvironment environment)
    {
        _userStore = (MongoUserStore)userStore;
        _logger = logger;
        _loggerFactory = loggerFactory;
        _environment = environment;
    }

    /// <summary>
    /// 创建新用户
    /// </summary>
    [HttpPost("create")]
    [AllowAnonymousIfNoUsers]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { message = "用户名和密码不能为空" });
            }

            var success = await _userStore.CreateUserAsync(
                request.Username,
                request.Password,
                request.Role ?? "User"
            );

            if (!success)
            {
                return BadRequest(new { message = "用户已存在或创建失败" });
            }

            return Ok(new { message = $"用户 {request.Username} 创建成功" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建用户时发生错误");
            return StatusCode(500, new { message = "创建用户失败", error = ex.Message });
        }
    }

    /// <summary>
    /// 更新用户密码
    /// </summary>
    [HttpPut("update-password")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdatePassword([FromBody] UpdatePasswordRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.NewPassword))
            {
                return BadRequest(new { message = "用户名和新密码不能为空" });
            }

            var success = await _userStore.UpdatePasswordAsync(request.Username, request.NewPassword);

            if (!success)
            {
                return NotFound(new { message = $"用户 {request.Username} 不存在" });
            }

            return Ok(new { message = $"用户 {request.Username} 的密码已更新" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新密码时发生错误");
            return StatusCode(500, new { message = "更新密码失败", error = ex.Message });
        }
    }

    /// <summary>
    /// 删除用户
    /// </summary>
    [HttpDelete("delete/{username}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteUser(string username)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return BadRequest(new { message = "用户名不能为空" });
            }

            var success = await _userStore.DeleteUserAsync(username);

            if (!success)
            {
                return NotFound(new { message = $"用户 {username} 不存在" });
            }

            return Ok(new { message = $"用户 {username} 已删除" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除用户时发生错误");
            return StatusCode(500, new { message = "删除用户失败", error = ex.Message });
        }
    }

    /// <summary>
    /// 获取所有用户列表
    /// </summary>
    [HttpGet("list")]
    [Authorize(Roles = "Admin")]
    public IActionResult GetUsers()
    {
        try
        {
            var users = _userStore.GetUsers();
            var userList = users.Select(u => new
            {
                username = u.Username,
                role = u.Role
            }).ToArray();

            return Ok(userList);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取用户列表时发生错误");
            return StatusCode(500, new { message = "获取用户列表失败", error = ex.Message });
        }
    }
}

public class CreateUserRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? Role { get; set; }
}

public class UpdatePasswordRequest
{
    public string Username { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}
