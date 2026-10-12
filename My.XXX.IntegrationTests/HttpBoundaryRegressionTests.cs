using My.XXX.APIs.Common.Mapping;
using My.XXX.APIs.Contracts.Menus;
using My.XXX.APIs.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace My.XXX.IntegrationTests;

[TestClass]
public sealed class HttpBoundaryRegressionTests
{
    [TestMethod]
    public async Task ForwardedHeadersOnlyAcceptExplicitlyTrustedSources()
    {
        foreach (var (key, value, remoteIp, accepted) in new[]
        {
            ("", "", "127.0.0.1", false),
            ("ForwardedHeaders:KnownProxies:0", "192.0.2.10", "192.0.2.10", true),
            ("ForwardedHeaders:KnownProxies:0", "192.0.2.10", "192.0.2.11", false),
            ("ForwardedHeaders:KnownNetworks:0", "192.0.2.0/24", "192.0.2.11", true),
            ("ForwardedHeaders:KnownNetworks:0", "192.0.2.0/24", "198.51.100.11", false)
        })
        {
            var settings = new Dictionary<string, string?>();
            if (key.Length > 0)
            {
                settings[key] = value;
            }
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddHttpPolicies(configuration);
            using var provider = services.BuildServiceProvider();
            var context = new DefaultHttpContext();
            context.Connection.RemoteIpAddress = IPAddress.Parse(remoteIp);
            context.Request.Scheme = "http";
            context.Request.Headers["X-Forwarded-For"] = "203.0.113.77";
            context.Request.Headers["X-Forwarded-Proto"] = "https";
            var middleware = new ForwardedHeadersMiddleware(_ => Task.CompletedTask,
                provider.GetRequiredService<ILoggerFactory>(),
                provider.GetRequiredService<IOptions<ForwardedHeadersOptions>>());

            await middleware.Invoke(context);

            Assert.AreEqual(accepted ? "203.0.113.77" : remoteIp, context.Connection.RemoteIpAddress.ToString());
            Assert.AreEqual(accepted ? "https" : "http", context.Request.Scheme);
        }
    }

    [TestMethod]
    public Task PartialMenuUpdatesAllowOmittedFieldsButCreationRequiresName() => WithClientAsync(async client =>
    {
        foreach (var json in new[] { "{\"id\":1,\"number\":2}", "{\"id\":1,\"number\":2,\"displayName\":null,\"clearFields\":null}" })
        {
            using var response = await client.PatchAsync("/boundary-regression/menu",
                new StringContent(json, Encoding.UTF8, "application/json"));
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.AreEqual(2, body.RootElement.GetProperty("number").GetInt32());
            Assert.AreEqual(JsonValueKind.Null, body.RootElement.GetProperty("displayName").ValueKind);
            Assert.AreEqual(JsonValueKind.Null, body.RootElement.GetProperty("clearFields").ValueKind);
        }
        using var invalid = await client.PostAsync("/boundary-regression/menu",
            new StringContent("{\"isAction\":false}", Encoding.UTF8, "application/json"));
        Assert.AreEqual(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var valid = await client.PostAsync("/boundary-regression/menu",
            new StringContent("{\"displayName\":\"菜单\",\"isAction\":false}", Encoding.UTF8, "application/json"));
        Assert.AreEqual(HttpStatusCode.OK, valid.StatusCode);
    });

    private static async Task WithClientAsync(Func<HttpClient, Task> operation)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddHttpContracts(builder.Configuration);
        builder.Services.AddControllers().AddApplicationPart(typeof(HttpBoundaryProbeController).Assembly);
        await using var app = builder.Build();
        app.MapControllers();
        await app.StartAsync();
        try
        {
            var address = app.Services.GetRequiredService<IServer>().Features
                .Get<IServerAddressesFeature>()!.Addresses.Single();
            using var client = new HttpClient { BaseAddress = new Uri(address) };
            await operation(client);
        }
        finally
        {
            await app.StopAsync();
        }
    }
}

// 使用真实 MVC 校验和映射，避免只测 DTO 而遗漏 HTTP 边界行为。
[ApiController, Route("boundary-regression")]
public sealed class HttpBoundaryProbeController : ControllerBase
{
    [HttpPatch("menu")]
    public object Update(UpdateMenuRequest input) => WireMapper.Instance.ToMenuModification(input)!;

    [HttpPost("menu")]
    public object Create(CreateMenuRequest input) => WireMapper.Instance.ToMenuCreation(input)!;

}
