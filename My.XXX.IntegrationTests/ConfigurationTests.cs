using My.XXX.APIs;
using My.XXX.Infrastructure.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;

namespace My.XXX.IntegrationTests;

[TestClass]
public sealed class ConfigurationTests
{
    [TestMethod]
    public void EncryptionKeysAreValidatedAndIsolatedPerApplication()
    {
        foreach (var invalid in new[] { "", "not-base64", Convert.ToBase64String(new byte[16]) })
            Assert.ThrowsExactly<InvalidOperationException>(() => new AesEncryptionKey(invalid));

        var first = new AesEncryptionKey(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        var second = new AesEncryptionKey(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        var ciphertext = first.Encrypt("connection");
        Assert.AreEqual("connection", first.Decrypt(ciphertext));
        Assert.ThrowsExactly<AuthenticationTagMismatchException>(() => second.Decrypt(ciphertext));
    }

    [TestMethod]
    public async Task RootConfigurationSupportsCustomEnvironmentOverridesAndPersistedEncryption()
    {
        var root = Path.Combine(Path.GetTempPath(), "my-config-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            var connection = "Host=127.0.0.1;Port=1;Database=test;Username=test;Timeout=1";
            File.WriteAllText(Path.Combine(root, "appsettings.json"), JsonSerializer.Serialize(new
            {
                APP_ENCRYPTION_KEY = key,
                JwtConfig = new { Secret = new string('x', 32), Issuer = "file-issuer", Audience = "test", ExpiryInMinutes = 5, RefreshExpiryInMinutes = 60 },
                DatabaseProviders = new { Default = "PostgreSQL" },
                ConnectionStrings = new { Default = AESHelper.Encrypt(connection, key) },
                PermissionCache = new { KeyPrefix = "test", ExpiryInMinutes = 5 },
                DataProtection = new { KeyPath = Path.Combine(root, "keys") }
            }));
            var args = new[] { "--contentRoot", root, "--environment", "Testing", "--JwtConfig:Issuer", "command-line-issuer" };
            string protectedValue;
            await using (var app = Program.CreateApplication(args))
            {
                var configuration = app.Services.GetRequiredService<IConfiguration>();
                Assert.AreEqual("command-line-issuer", configuration["JwtConfig:Issuer"]);
                Assert.AreEqual("test", configuration["JwtConfig:Audience"]);
                Assert.AreEqual(connection, app.Services.GetRequiredService<AesEncryptionKey>()
                    .Decrypt(configuration.GetConnectionString("Default")!));
                protectedValue = app.Services.GetRequiredService<IDataProtectionProvider>()
                    .CreateProtector("configuration-test").Protect("persisted-value");
            }
            await using (var app = Program.CreateApplication(args))
            {
                Assert.AreEqual("persisted-value", app.Services.GetRequiredService<IDataProtectionProvider>()
                    .CreateProtector("configuration-test").Unprotect(protectedValue));
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
