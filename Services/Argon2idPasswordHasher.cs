using Sodium;

namespace NetDisk.Api.Services;

public class Argon2idPasswordHasher : IPasswordHasher
{
    public string HashPassword(string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            throw new ArgumentException("Password cannot be null or empty", nameof(password));
        }

        return PasswordHash.ArgonHashString(password);
    }

    public bool VerifyPassword(string password, string hashedPassword)
    {
        if (string.IsNullOrEmpty(password))
        {
            return false;
        }

        if (string.IsNullOrEmpty(hashedPassword))
        {
            return false;
        }

        try
        {
            return PasswordHash.ArgonHashStringVerify(hashedPassword, password);
        }
        catch
        {
            return false;
        }
    }
}
