using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using TalentPilot.Application.Calendar;

namespace TalentPilot.Infrastructure.Calendar;

public sealed class GoogleCalendarTokenProtector : IGoogleCalendarTokenProtector
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private readonly byte[] _key;

    public GoogleCalendarTokenProtector(IOptions<GoogleCalendarOptions> options)
    {
        if (string.IsNullOrWhiteSpace(options.Value.TokenProtectionKey))
        {
            throw new InvalidOperationException("Google Calendar token protection key is not configured.");
        }

        var keyMaterial = options.Value.TokenProtectionKey.Trim();
        _key = SHA256.HashData(Encoding.UTF8.GetBytes(keyMaterial));
    }

    public string Protect(string token)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var plaintext = Encoding.UTF8.GetBytes(token);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        return string.Join(':', "v1", Convert.ToBase64String(nonce), Convert.ToBase64String(tag), Convert.ToBase64String(ciphertext));
    }

    public string Unprotect(string protectedToken)
    {
        var parts = protectedToken.Split(':');
        if (parts.Length != 4 || !string.Equals(parts[0], "v1", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Google Calendar token payload is not in a supported format.");
        }

        var nonce = Convert.FromBase64String(parts[1]);
        var tag = Convert.FromBase64String(parts[2]);
        var ciphertext = Convert.FromBase64String(parts[3]);
        var plaintext = new byte[ciphertext.Length];

        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);

        return Encoding.UTF8.GetString(plaintext);
    }
}
