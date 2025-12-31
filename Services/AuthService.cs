using Microsoft.IdentityModel.Tokens;
using NetDisk.Api.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace NetDisk.Api.Services;

public class AuthService : IAuthService
{
    private readonly IConfiguration _configuration;
    private readonly MongoUserStore _userStore;
    private readonly string _secretKey;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _expirationMinutes;

    public AuthService(IConfiguration configuration, IUserStore userStore)
    {
        _configuration = configuration;
        _userStore = (MongoUserStore)userStore;
        _secretKey = configuration["JwtSettings:SecretKey"] 
            ?? throw new InvalidOperationException("JWT SecretKey not configured");
        _issuer = configuration["JwtSettings:Issuer"] 
            ?? throw new InvalidOperationException("JWT Issuer not configured");
        _audience = configuration["JwtSettings:Audience"] 
            ?? throw new InvalidOperationException("JWT Audience not configured");
        _expirationMinutes = int.Parse(configuration["JwtSettings:ExpirationMinutes"] ?? "1440");
    }

    public LoginResponse? Login(string username, string password)
    {
        // 验证用户凭据
        if (!_userStore.ValidateCredentials(username, password))
        {
            return null;
        }

        // 生成 JWT token
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(_secretKey);
        var expiresAt = DateTime.UtcNow.AddMinutes(_expirationMinutes);

        // 获取用户角色
        var role = GetUserRole(username);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.Name, username),
                new Claim(ClaimTypes.Role, role),
                new Claim(JwtRegisteredClaimNames.Sub, username),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            }),
            Expires = expiresAt,
            Issuer = _issuer,
            Audience = _audience,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        var tokenString = tokenHandler.WriteToken(token);

        return new LoginResponse
        {
            Token = tokenString,
            ExpiresAt = expiresAt
        };
    }

    public bool ValidateToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return false;

        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(_secretKey);

        try
        {
            tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = _issuer,
                ValidateAudience = true,
                ValidAudience = _audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            }, out SecurityToken validatedToken);

            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool IsUserAdmin(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return false;

        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(_secretKey);

        try
        {
            var claimsPrincipal = tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = _issuer,
                ValidateAudience = true,
                ValidAudience = _audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            }, out SecurityToken validatedToken);

            var roleClaim = claimsPrincipal.FindFirst(ClaimTypes.Role);
            return roleClaim?.Value.Equals("Admin", StringComparison.OrdinalIgnoreCase) ?? false;
        }
        catch
        {
            return false;
        }
    }

    private string GetUserRole(string username)
    {
        var user = _userStore.GetUser(username);
        return user?.Role ?? "User";
    }
}
