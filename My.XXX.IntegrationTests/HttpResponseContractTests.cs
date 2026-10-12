using My.XXX.APIs.Common.Middleware;
using My.XXX.APIs.Common.Responses;
using My.XXX.APIs.Contracts.Shared;
using My.XXX.APIs.DependencyInjection;
using My.XXX.Application.Abstractions.Results;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace My.XXX.IntegrationTests;

[TestClass]
public sealed class HttpResponseContractTests
{
    [TestMethod]
    public async Task MvcPreservesExplicitBodiesAndUsesUniformValidationErrors()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddHttpContracts(builder.Configuration);
        builder.Services.AddControllers().AddApplicationPart(typeof(ErrorContractProbeController).Assembly);
        builder.Services.AddTransient<ExceptionHandlingMiddleware>();
        await using var app = builder.Build();
        app.UseMiddleware<ExceptionHandlingMiddleware>();
        app.MapControllers();
        app.MapGet("/contract-probe/exception", (HttpContext _) =>
            Task.FromException(new System.InvalidOperationException("Private exception details.")));
        await app.StartAsync();
        try
        {
            var address = app.Services.GetRequiredService<IServer>().Features
                .Get<IServerAddressesFeature>()!.Addresses.Single();
            using var client = new HttpClient { BaseAddress = new System.Uri(address) };
            using var exception = await client.GetAsync("/contract-probe/exception");
            Assert.AreEqual(HttpStatusCode.InternalServerError, exception.StatusCode);
            using var exceptionBody = JsonDocument.Parse(await exception.Content.ReadAsStringAsync());
            Assert.AreEqual("Server.Unexpected", exceptionBody.RootElement.GetProperty("code").GetString());
            Assert.IsFalse(exceptionBody.RootElement.GetProperty("message").GetString()!.Contains("Private"));
            Assert.AreEqual(exception.Headers.GetValues("X-Request-Id").Single(),
                exceptionBody.RootElement.GetProperty("traceId").GetString());
            using var failure = await client.GetAsync("/contract-probe/errors");
            Assert.AreEqual(HttpStatusCode.NotFound, failure.StatusCode);
            using var failureBody = JsonDocument.Parse(await failure.Content.ReadAsStringAsync());
            Assert.AreEqual("Menu.NotFound", failureBody.RootElement.GetProperty("code").GetString());
            Assert.AreEqual("probe", failureBody.RootElement.GetProperty("traceId").GetString());
            Assert.AreEqual(JsonValueKind.Null, failureBody.RootElement.GetProperty("data").ValueKind);
            using var modern = JsonDocument.Parse(await client.GetStringAsync("/contract-probe/modern"));
            Assert.AreEqual("value", modern.RootElement.GetProperty("data").GetString());
            Assert.AreEqual("probe", modern.RootElement.GetProperty("traceId").GetString());

            // An unmarked new endpoint also owns its response; no global wrapping remains.
            using var plain = JsonDocument.Parse(await client.GetStringAsync("/contract-probe/plain"));
            Assert.AreEqual("value", plain.RootElement.GetProperty("value").GetString());
            Assert.IsFalse(plain.RootElement.TryGetProperty("data", out _));

            // 验证真实 MVC 响应按服务器本地时间输出，避免内部 UTC 时间直接暴露为无时区值。
            using var time = JsonDocument.Parse(await client.GetStringAsync("/contract-probe/plain/time"));
            var utc = new System.DateTime(2026, 10, 4, 2, 0, 0, System.DateTimeKind.Utc);
            var expected = utc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff",
                System.Globalization.CultureInfo.InvariantCulture);
            Assert.AreEqual(expected, time.RootElement.GetProperty("createdAt").GetString());
            Assert.AreEqual(JsonValueKind.Null, time.RootElement.GetProperty("updatedAt").ValueKind);

            foreach (var route in new[] { "errors", "modern", "plain" })
            {
                using var response = await client.PostAsync("/contract-probe/" + route,
                    new StringContent("{}", Encoding.UTF8, "application/json"));
                Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
                using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                Assert.AreEqual(0, body.RootElement.GetProperty("statusCode").GetInt32());
                Assert.AreEqual("Request.ValidationFailed", body.RootElement.GetProperty("code").GetString());
            }
        }
        finally
        {
            await app.StopAsync();
        }
    }
}

public sealed class ContractProbeInput
{
    [Required] public string? Name { get; set; }
}

[ApiController, Route("contract-probe/errors")]
public sealed class ErrorContractProbeController : ControllerBase
{
    [HttpGet]
    public ActionResult<APIResponse<string>> Get() => FluentResults.Result.Fail<string>(
        My.XXX.Domain.Menus.Policies.MenuErrors.NotFound().ToError()).ToHttpResult("probe");
    [HttpPost]
    public APIResponse<string> Post(ContractProbeInput input) => new(1, "Success", input.Name);
}
[ApiController, Route("contract-probe/modern")]
public sealed class ModernContractProbeController : ControllerBase
{
    [HttpGet] public APIResponse<string> Get() => new(1, "Success", "value", traceId: "probe");
    [HttpPost] public APIResponse<string> Post(ContractProbeInput input) => new(1, "Success", input.Name!);
}

[ApiController, Route("contract-probe/plain")]
public sealed class PlainContractProbeController : ControllerBase
{
    [HttpGet] public object Get() => new { value = "value" };
    [HttpGet("time")]
    public object GetTime() => new
    {
        createdAt = new System.DateTime(2026, 10, 4, 2, 0, 0, System.DateTimeKind.Utc),
        updatedAt = (System.DateTime?)null
    };
    [HttpPost] public object Post(ContractProbeInput input) => new { value = input.Name };
}
