using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace My.XXX.APIs.Common.Telemetry;

/// <summary>在 DI 释放导出器依赖的 HttpClientFactory 之前完成遥测刷新。</summary>
internal sealed class TelemetryFlushService(IServiceProvider services) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken)
    {
        services.GetService<TracerProvider>()?.ForceFlush(3000);
        services.GetService<MeterProvider>()?.ForceFlush(3000);
        services.GetService<LoggerProvider>()?.ForceFlush(3000);
        return Task.CompletedTask;
    }
}
