using My.XXX.Infrastructure.Security;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System;
using System.Xml.Linq;

namespace Microsoft.AspNetCore.DataProtection;

/// <summary>
/// 用于通过 <see cref="IDataProtectionBuilder"/> 配置数据保护的扩展方法。
/// </summary>
public static class DataProtectionBuilderExtensions
{
    /// <summary>
    /// 配置密钥在持久化到存储之前先用 AES 加密。
    /// </summary>
    /// <param name="builder"><see cref="IDataProtectionBuilder"/>。</param>
    /// <returns>此操作完成后对 <see cref="IDataProtectionBuilder" /> 的引用。</returns>
    public static IDataProtectionBuilder ProtectKeysWithAES(this IDataProtectionBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddSingleton<IConfigureOptions<KeyManagementOptions>>(services =>
        {
            return new ConfigureOptions<KeyManagementOptions>(options =>
            {
                options.XmlEncryptor = new AesXmlEncryptor(services.GetRequiredService<AesEncryptionKey>());
            });
        });

        return builder;
    }
}

/// <summary>
/// 一个使用 AES 加密器对 XML 元素进行加密的 <see cref="IXmlEncryptor"/>。
/// </summary>
internal sealed class AesXmlEncryptor(AesEncryptionKey key) : IXmlEncryptor
{
    /// <summary>
    /// 使用 AES 加密器对指定的 <see cref="XElement"/> 进行加密。
    /// </summary>
    /// <param name="plaintextElement">要加密的明文元素。</param>
    /// <returns>
    /// 一个 <see cref="EncryptedXmlInfo"/>，包含 <paramref name="plaintextElement"/>
    /// 的加密值以及解密所需的相关信息。
    /// </returns>
    public EncryptedXmlInfo Encrypt(XElement plaintextElement)
    {
        ArgumentNullException.ThrowIfNull(plaintextElement);

        var jsonXmlStr = JsonConvert.SerializeObject(plaintextElement);
        var encryptedData = key.Encrypt(jsonXmlStr);
        var newElement = new XElement("encryptedKey",
            new XComment(" 此密钥已使用 AES 加密。"),
            new XElement("value", encryptedData));

        return new EncryptedXmlInfo(newElement, typeof(AesXmlDecryptor));
    }
}

/// <summary>
/// 一个使用 AES 解密器对 XML 元素进行解密的 <see cref="IXmlDecryptor"/>。
/// </summary>
internal sealed class AesXmlDecryptor(IServiceProvider services) : IXmlDecryptor
{
    /// <summary>
    /// 解密指定的 XML 元素。
    /// </summary>
    /// <param name="encryptedElement">已加密的 XML 元素。</param>
    /// <returns><paramref name="encryptedElement"/> 的解密形式。</returns>
    public XElement Decrypt(XElement encryptedElement)
    {
        ArgumentNullException.ThrowIfNull(encryptedElement);

        var encryptedData = (string?)encryptedElement.Element("value");
        var jsonXmlStr = services.GetRequiredService<AesEncryptionKey>().Decrypt(encryptedData!);

        return JsonConvert.DeserializeObject<XElement>(jsonXmlStr)!;
    }
}
