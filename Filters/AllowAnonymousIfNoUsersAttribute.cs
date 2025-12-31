using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NetDisk.Api.Services;

namespace NetDisk.Api.Filters;

/// <summary>
/// 当数据库中没有用户时，允许匿名访问；否则需要管理员权限
/// </summary>
public class AllowAnonymousIfNoUsersAttribute : Attribute, IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var userStore = context.HttpContext.RequestServices.GetService<IUserStore>();
        
        if (userStore == null)
        {
            context.Result = new StatusCodeResult(StatusCodes.Status500InternalServerError);
            return;
        }

        // 检查是否有用户存在
        var users = userStore.GetUsers();
        
        // 如果没有用户，允许匿名访问（绕过认证）
        if (users == null || users.Count == 0)
        {
            // 明确标记为允许匿名访问
            context.HttpContext.Items["AllowAnonymous"] = true;
            return;
        }

        // 如果有用户，检查是否已认证且是管理员
        var user = context.HttpContext.User;
        
        if (!user.Identity?.IsAuthenticated ?? true)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        // 检查是否是管理员角色
        if (!user.IsInRole("Admin"))
        {
            context.Result = new ForbidResult();
            return;
        }
    }
}
