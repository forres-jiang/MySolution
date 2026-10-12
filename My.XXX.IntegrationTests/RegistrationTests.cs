using My.XXX.APIs;
using My.XXX.APIs.Common.Authorization;
using My.XXX.Application.Authentication.Ports;
using My.XXX.Application.Authorization;
using My.XXX.Application.Authorization.Ports;
using My.XXX.Application.Menus;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using IAuthenticationService = My.XXX.Application.Authentication.Ports.IAuthenticationService;

namespace My.XXX.IntegrationTests;

[TestClass]
public sealed class RegistrationTests
{
    [TestMethod]
    public async Task CompositionRootResolvesUseCasesControllersAndHttpPolicies()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "MySolution.sln"))) root = root.Parent;
        Assert.IsNotNull(root);
        var encryptionKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        await using var app = Program.CreateApplication(new[] { "--contentRoot", Path.Combine(root.FullName, "My.XXX.APIs"), "--environment", "Development" }, builder =>
        {
            builder.Configuration.Sources.Clear();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["APP_ENCRYPTION_KEY"] = encryptionKey,
                ["JwtConfig:Secret"] = new string('x', 32),
                ["JwtConfig:Issuer"] = "test",
                ["JwtConfig:Audience"] = "test",
                ["JwtConfig:ExpiryInMinutes"] = "5",
                ["JwtConfig:RefreshExpiryInMinutes"] = "60",
                ["PermissionCache:KeyPrefix"] = "test",
                ["PermissionCache:ExpiryInMinutes"] = "5",
                ["DatabaseProviders:Default"] = "PostgreSQL",
                ["ConnectionStrings:Default"] = "Host=127.0.0.1;Port=1;Database=test;Username=test;Timeout=1",
                ["DataProtection:KeyPath"] = Path.Combine(root.FullName, ".test-artifacts", "registration-keys")
            });
            builder.Host.UseDefaultServiceProvider(options => { options.ValidateOnBuild = true; options.ValidateScopes = true; });
        });
        await using var scope = app.Services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        foreach (var service in new[] { typeof(MenuCommandService), typeof(MenuQueryService), typeof(RoleAccessAdministration),
            typeof(RoleMenuAssignmentService), typeof(IPermissionAdministration), typeof(IPermissionQuery),
            typeof(IAuthenticationService), typeof(ISessionService) })
            Assert.IsNotNull(provider.GetRequiredService(service));
        foreach (var controller in typeof(Program).Assembly.GetExportedTypes().Where(type => !type.IsAbstract && typeof(ControllerBase).IsAssignableFrom(type)))
            Assert.IsNotNull(ActivatorUtilities.CreateInstance(provider, controller));

        var schemes = await provider.GetRequiredService<IAuthenticationSchemeProvider>().GetAllSchemesAsync();
        CollectionAssert.AreEquivalent(new[] { "Bearer", "Refresh" }, schemes.Select(scheme => scheme.Name).ToArray());
        var policies = provider.GetRequiredService<IAuthorizationPolicyProvider>();
        Assert.IsNotNull(await policies.GetFallbackPolicyAsync());
        Assert.IsTrue((await policies.GetPolicyAsync("Permissions"))!.Requirements.Any(requirement => requirement is PermissionsRequirement));
    }
}


