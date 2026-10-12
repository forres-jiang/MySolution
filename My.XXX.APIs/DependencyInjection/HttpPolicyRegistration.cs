using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Net;
using System.Threading.RateLimiting;

namespace My.XXX.APIs.DependencyInjection;

public static class HttpPolicyRegistration
{
    public static void AddHttpPolicies(this IServiceCollection services, IConfiguration configuration)
    {
        //跨域,根据实际情况开启
        var allowedHostArr = configuration.GetSection("AllowedHostArray").Get<string[]>() ?? Array.Empty<string>();
        services.AddCors(options =>
        {
            options.AddPolicy("AppPolicy", builder =>
            {
                builder.WithOrigins(allowedHostArr).AllowAnyMethod().AllowAnyHeader().AllowCredentials();
            });
        });

        // 反向代理（k8s ingress / 负载均衡）下正确还原 scheme 与客户端 IP，供 HSTS/HTTPS 重定向使用。
        // 仅信任显式配置的代理地址；未配置时忽略 X-Forwarded-*，防止客户端伪造。
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
            foreach (var proxy in configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? Array.Empty<string>())
            {
                options.KnownProxies.Add(IPAddress.Parse(proxy));
            }
            foreach (var network in configuration.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>() ?? Array.Empty<string>())
            {
                options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
            }
            // 空的可信列表会让中间件信任所有来源，因此无配置时必须关闭转发头处理。
            options.ForwardedHeaders = options.KnownProxies.Count > 0 || options.KnownIPNetworks.Count > 0
                ? ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
                : ForwardedHeaders.None;
        });

        // 令牌刷新限流：按客户端 IP 固定窗口，缓解刷新令牌暴力/重放。
        var refreshPermitLimit = configuration.GetValue("RateLimit:Refresh:PermitLimit", 10);
        var refreshWindowSeconds = configuration.GetValue("RateLimit:Refresh:WindowSeconds", 60);
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("refresh", httpContext => RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = refreshPermitLimit,
                    Window = TimeSpan.FromSeconds(refreshWindowSeconds),
                    QueueLimit = 0
                }));
        });

    }
}
