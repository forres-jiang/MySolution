using System;
using System.Security.Cryptography;
using System.Text;

namespace My.XXX.Infrastructure.Security;

public class AESHelper
{
    private static byte[] GetKey(string? encodedKey)
    {
        var encoded = encodedKey ?? Environment.GetEnvironmentVariable("APP_ENCRYPTION_KEY");
        if (string.IsNullOrWhiteSpace(encoded))
            throw new InvalidOperationException("APP_ENCRYPTION_KEY must contain a Base64-encoded 32-byte key.");
        byte[] key;
        try { key = Convert.FromBase64String(encoded); }
        catch (FormatException ex) { throw new InvalidOperationException("APP_ENCRYPTION_KEY must contain a Base64-encoded 32-byte key.", ex); }
        if (key.Length != 32)
        {
            CryptographicOperations.ZeroMemory(key);
            throw new InvalidOperationException("APP_ENCRYPTION_KEY must be 32 bytes.");
        }
        return key;
    }

    public static void ValidateKey(string? encodedKey = null)
    {
        var key = GetKey(encodedKey);
        CryptographicOperations.ZeroMemory(key);
    }

    public static string Encrypt(string text, string? encodedKey = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        var key = GetKey(encodedKey);
        try
        {
            var nonce = RandomNumberGenerator.GetBytes(12);
            var plaintext = Encoding.UTF8.GetBytes(text);
            var ciphertext = new byte[plaintext.Length];
            var tag = new byte[16];
            using var aes = new AesGcm(key, 16);
            aes.Encrypt(nonce, plaintext, ciphertext, tag);
            var payload = new byte[28 + ciphertext.Length];
            nonce.CopyTo(payload, 0);
            tag.CopyTo(payload, 12);
            ciphertext.CopyTo(payload, 28);
            return "enc:v1:" + Convert.ToBase64String(payload);
        }
        finally { CryptographicOperations.ZeroMemory(key); }
    }

    public static string Decrypt(string text, string? encodedKey = null)
    {
        if (text == null || !text.StartsWith("enc:v1:", StringComparison.Ordinal))
            throw new CryptographicException("Unsupported ciphertext format. Migrate legacy encrypted settings before deployment.");
        var payload = Convert.FromBase64String(text.Substring(7));
        if (payload.Length < 28) throw new CryptographicException("Invalid ciphertext.");
        var key = GetKey(encodedKey);
        try
        {
            var plaintext = new byte[payload.Length - 28];
            using var aes = new AesGcm(key, 16);
            aes.Decrypt(payload.AsSpan(0, 12), payload.AsSpan(28), payload.AsSpan(12, 16), plaintext);
            return Encoding.UTF8.GetString(plaintext);
        }
        finally { CryptographicOperations.ZeroMemory(key); }
    }
}
