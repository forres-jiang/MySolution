using My.XXX.APIs.Common.Telemetry;
using My.XXX.APIs.Configurations;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System;
using System.Collections.Generic;

namespace My.XXX.APIs.DependencyInjection;

/// <summary>在宿主边界注册可观测性服务，导出器使用标准 OTEL_* 配置。</summary>
public static class TelemetryRegistration
{
    public static bool AddProjectTelemetry(this WebApplicationBuilder builder)
    {
        var settings = builder.Configuration.GetSection("OpenTelemetry").Get<TelemetrySettings>() ?? new();
        if (!settings.Enabled)
        {
            return false;
        }
        if (!double.IsFinite(settings.TraceSampleRatio) || settings.TraceSampleRatio < 0 || settings.TraceSampleRatio > 1)
        {
            throw new InvalidOperationException("OpenTelemetry:TraceSampleRatio must be between 0 and 1.");
        }
        var serviceName = settings.ServiceName ?? builder.Configuration["OTEL_SERVICE_NAME"] ?? builder.Environment.ApplicationName;
        if (string.IsNullOrWhiteSpace(serviceName))
        {
            throw new InvalidOperationException("OpenTelemetry:ServiceName cannot be blank.");
        }

        var serviceInstanceId = Guid.NewGuid().ToString("N");
        void ConfigureResource(ResourceBuilder resource) => resource
            .AddService(serviceName, serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString(), serviceInstanceId: serviceInstanceId)
            .AddAttributes(new Dictionary<string, object>
            {
                ["deployment.environment.name"] = builder.Environment.EnvironmentName
            });

        var telemetry = builder.Services.AddOpenTelemetry()
            .ConfigureResource(ConfigureResource)
            .WithTracing(tracing =>
            {
                tracing.SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(settings.TraceSampleRatio)))
                    .AddAspNetCoreInstrumentation(options => options.Filter = context =>
                        !context.Request.Path.StartsWithSegments("/healthy") && !context.Request.Path.StartsWithSegments("/ready"))
                    .AddHttpClientInstrumentation()
                    .AddSqlClientInstrumentation()
                    .AddSource("Npgsql", "My.XXX.*");
            })
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddSqlClientInstrumentation()
                    .AddMeter("Npgsql", "My.XXX.Authentication", "My.XXX.Authorization");
            });

        // Serilog 已向此提供程序转发日志，移除默认提供程序以避免重复输出。
        builder.Logging.ClearProviders();
        builder.Logging.AddOpenTelemetry(logging =>
        {
            var resource = ResourceBuilder.CreateDefault();
            ConfigureResource(resource);
            logging.SetResourceBuilder(resource);
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });
        if (settings.OtlpEnabled)
        {
            telemetry.UseOtlpExporter();
        }
        builder.Services.AddHostedService<TelemetryFlushService>();
        return true;
    }
}

