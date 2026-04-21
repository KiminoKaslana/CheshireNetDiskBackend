using System.Text.Json.Serialization;

namespace NetDisk.Api.Models;

public class LoginResponse
{
    [JsonIgnore]
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}
