using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace My.XXX.ServiceDefaults;

/// <summary>
/// 为 Aspire 编排下的服务提供通用接线：健康检查、服务发现、HTTP 弹性。
/// OTel 相关注册仍由 API 项目的 TelemetryRegistration 负责，避免重复注册导致采样器冲突。
/// </summary>
public static class Extensions
{
    /// <summary>在 WebApplicationBuilder 上注册 Aspire 服务发现、健康检查、HTTP 弹性。</summary>
    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        // Aspire 约定的两个探针：/alive 只测进程存活，/health 覆盖全部注册的 HealthCheck。
        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        // 允许通过逻辑名（例如 "redis"、"postgres"）解析实际端点，AppHost 会自动注入。
        builder.Services.AddServiceDiscovery();

        // 为所有 HttpClient 加上标准弹性管道（重试 + 熔断 + 超时），替代手写 Polly。
        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler();
            http.AddServiceDiscovery();
        });

        return builder;
    }

    /// <summary>映射 Aspire 约定的默认端点，与 API 项目原有的 /healthy、/ready 并存互不干扰。</summary>
    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        // /alive：进程存活探针，Aspire Dashboard 用它判定资源是否 Running。
        app.MapHealthChecks("/alive", new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains("live")
        }).AllowAnonymous();

        // /health：整体健康状态，覆盖所有已注册检查。
        app.MapHealthChecks("/health").AllowAnonymous();

        return app;
    }
}
