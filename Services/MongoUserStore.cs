using MongoDB.Driver;
using NetDisk.Api.Models;

namespace NetDisk.Api.Services;

public class MongoUserStore : IUserStore
{
    private readonly IMongoCollection<User> _usersCollection;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<MongoUserStore> _logger;

    public MongoUserStore(IMongoDatabase database, IPasswordHasher passwordHasher, ILogger<MongoUserStore> logger)
    {
        _usersCollection = database.GetCollection<User>("users");
        _passwordHasher = passwordHasher;
        _logger = logger;

        EnsureIndexes();
    }

    public IReadOnlyList<UserCredential> GetUsers()
    {
        try
        {
            var users = _usersCollection.Find(_ => true).ToList();
            return users.Select(u => new UserCredential
            {
                Username = u.Username,
                Password = u.PasswordHash,
                Role = u.Role
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get users from MongoDB");
            return Array.Empty<UserCredential>();
        }
    }

    public UserCredential? GetUser(string username)
    {
        try
        {
            var filter = Builders<User>.Filter.Eq(u => u.Username, username);
            var user = _usersCollection.Find(filter).FirstOrDefault();
            
            if (user == null)
            {
                return null;
            }

            return new UserCredential
            {
                Username = user.Username,
                Password = user.PasswordHash,
                Role = user.Role
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get user {Username} from MongoDB", username);
            return null;
        }
    }

    public async Task<User?> GetUserByUsernameAsync(string username)
    {
        try
        {
            var filter = Builders<User>.Filter.Eq(u => u.Username, username);
            return await _usersCollection.Find(filter).FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get user {Username} from MongoDB", username);
            return null;
        }
    }

    public async Task<bool> CreateUserAsync(string username, string password, string role = "User")
    {
        try
        {
            var existingUser = await GetUserByUsernameAsync(username);
            if (existingUser != null)
            {
                _logger.LogWarning("User {Username} already exists", username);
                return false;
            }

            var user = new User
            {
                Username = username,
                PasswordHash = _passwordHasher.HashPassword(password),
                Role = role,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _usersCollection.InsertOneAsync(user);
            _logger.LogInformation("User {Username} created successfully", username);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create user {Username}", username);
            return false;
        }
    }

    public async Task<bool> UpdatePasswordAsync(string username, string newPassword)
    {
        try
        {
            var filter = Builders<User>.Filter.Eq(u => u.Username, username);
            var update = Builders<User>.Update
                .Set(u => u.PasswordHash, _passwordHasher.HashPassword(newPassword))
                .Set(u => u.UpdatedAt, DateTime.UtcNow);

            var result = await _usersCollection.UpdateOneAsync(filter, update);
            
            if (result.ModifiedCount > 0)
            {
                _logger.LogInformation("Password updated for user {Username}", username);
                return true;
            }

            _logger.LogWarning("User {Username} not found for password update", username);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update password for user {Username}", username);
            return false;
        }
    }

    public async Task<bool> DeleteUserAsync(string username)
    {
        try
        {
            var filter = Builders<User>.Filter.Eq(u => u.Username, username);
            var result = await _usersCollection.DeleteOneAsync(filter);

            if (result.DeletedCount > 0)
            {
                _logger.LogInformation("User {Username} deleted successfully", username);
                return true;
            }

            _logger.LogWarning("User {Username} not found for deletion", username);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete user {Username}", username);
            return false;
        }
    }

    public bool ValidateCredentials(string username, string password)
    {
        try
        {
            var user = GetUserByUsernameAsync(username).GetAwaiter().GetResult();
            if (user == null)
            {
                return false;
            }

            return _passwordHasher.VerifyPassword(password, user.PasswordHash);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate credentials for user {Username}", username);
            return false;
        }
    }

    private void EnsureIndexes()
    {
        try
        {
            var indexKeys = Builders<User>.IndexKeys.Ascending(u => u.Username);
            var indexOptions = new CreateIndexOptions { Unique = true };
            var indexModel = new CreateIndexModel<User>(indexKeys, indexOptions);
            _usersCollection.Indexes.CreateOne(indexModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create indexes for users collection");
        }
    }
}
