namespace NetDisk.Api.Models;

public class AppSettings
{
    public FileStorageSettings FileStorage { get; set; } = new();
    public JwtSettings JwtSettings { get; set; } = new();
}

public class FileStorageSettings
{
    public string RootPath { get; set; } = string.Empty;
    public string HttpBaseUrl { get; set; } = string.Empty;
}

public class JwtSettings
{
    public string SecretKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int ExpirationMinutes { get; set; }
}
