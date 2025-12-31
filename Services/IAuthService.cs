using NetDisk.Api.Models;

namespace NetDisk.Api.Services;

public interface IAuthService
{
    LoginResponse? Login(string username, string password);
    bool ValidateToken(string token);
    bool IsUserAdmin(string token);
}
