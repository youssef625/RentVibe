using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RentVibe.Services.Caching;

public class CacheKeyBuilder
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public string Build(string prefix, object? args, string? userId)
    {
        var argsJson = args is null ? "null" : JsonSerializer.Serialize(args, SerializerOptions);
        var hash = ComputeHash(argsJson);
        var userPart = string.IsNullOrWhiteSpace(userId) ? "anon" : userId;
        return $"gql:{prefix}:u:{userPart}:a:{hash}";
    }

    private static string ComputeHash(string input)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}