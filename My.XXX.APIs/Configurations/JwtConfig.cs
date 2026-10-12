namespace My.XXX.APIs.Configurations;

public class JwtConfig
{
    public required string Secret { get; set; }

    /// <summary>
    /// 轮换期间的旧签名密钥。配置后验证同时接受主密钥和副密钥签发的令牌，新令牌始终使用主密钥签发。
    /// </summary>
    public string? SecondarySecret { get; set; }

    public required string Issuer { get; set; }

    public required string Audience { get; set; }

    /// <summary>
    /// Access token 在多少分钟过期
    /// </summary>
    public int ExpiryInMinutes { get; set; }

    /// <summary>
    /// Refresh token 在多少分钟过期
    /// </summary>
    public int RefreshExpiryInMinutes { get; set; }
}