using NetDisk.Api.Models;

namespace NetDisk.Api.Services;

public interface IUserStore
{
    IReadOnlyList<UserCredential> GetUsers();
    
    UserCredential? GetUser(string username);
}
