namespace My.XXX.Infrastructure.Security;

/// <summary>Application-scoped encryption key shared by data protection and encrypted settings.</summary>
public sealed class AesEncryptionKey
{
    private readonly string encodedKey;

    public AesEncryptionKey(string encodedKey)
    {
        AESHelper.ValidateKey(encodedKey);
        this.encodedKey = encodedKey;
    }

    public string Encrypt(string text) => AESHelper.Encrypt(text, encodedKey);
    public string Decrypt(string text) => AESHelper.Decrypt(text, encodedKey);
}
