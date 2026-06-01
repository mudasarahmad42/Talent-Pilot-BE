using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TalentPilot.Application.Ai;

internal static class AiTextHasher
{
    public static string HashText(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static string HashObject<T>(T value)
    {
        return HashText(JsonSerializer.Serialize(value, new JsonSerializerOptions
        {
            WriteIndented = false
        }));
    }
}
