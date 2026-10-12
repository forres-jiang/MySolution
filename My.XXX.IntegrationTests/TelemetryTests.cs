using My.XXX.APIs.DependencyInjection;
using My.XXX.Application.Authentication.Models;
using My.XXX.Application.Authentication.Ports;
using My.XXX.Domain.Authentication.Models;
using My.XXX.Infrastructure.Authentication;
using FluentResults;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace My.XXX.IntegrationTests;

[TestClass, DoNotParallelize]
public sealed class TelemetryTests
{
    [TestMethod]
    public void DisabledTelemetryDoesNotRegisterProviders()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["OpenTelemetry:Enabled"] = "false" });
        Assert.IsFalse(builder.AddProjectTelemetry());
        using var app = builder.Build();
        Assert.IsNull(app.Services.GetService<TracerProvider>());
        Assert.IsNull(app.Services.GetService<MeterProvider>());
    }

    [TestMethod]
    public void InvalidSamplingFailsBeforeHostStarts()
    {
        foreach (var value in new[] { "-0.1", "1.1", "NaN" })
        {
            var builder = WebApplication.CreateBuilder();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["OpenTelemetry:TraceSampleRatio"] = value });
            Assert.ThrowsExactly<InvalidOperationException>(() => builder.AddProjectTelemetry());
        }
    }

    [TestMethod]
    public async Task HttpSpansMetricsAndSerilogEventsShareTraceAndResource()
    {
        var spans = new CaptureExporter<Activity, SpanSnapshot>(a => new(a.OperationName, a.Kind, a.TraceId, a.SpanId, a.ParentSpanId, a.GetTagItem("url.path")?.ToString()));
        var logs = new CaptureExporter<LogRecord, LogSnapshot>(record => new(record.FormattedMessage, record.TraceId));
        var metrics = new CaptureExporter<Metric, string>(metric => metric.Name);
        var reader = new BaseExportingMetricReader(metrics);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "TelemetryTest" });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OpenTelemetry:ServiceName"] = "telemetry-test",
            ["OpenTelemetry:OtlpEnabled"] = "false",
            ["OpenTelemetry:TraceSampleRatio"] = "1"
        });
        Assert.IsTrue(builder.AddProjectTelemetry());
        builder.Services.AddOpenTelemetry()
            .WithTracing(tracing => tracing.AddProcessor(new SimpleActivityExportProcessor(spans)))
            .WithMetrics(pipeline => pipeline.AddReader(reader));
        builder.Logging.AddOpenTelemetry(logging => logging.AddProcessor(new SimpleLogRecordExportProcessor(logs)));
        builder.Host.UseSerilog((context, logger) => logger.MinimumLevel.Information(), writeToProviders: true);
        await using var app = builder.Build();
        using var meter = new Meter("My.XXX.Authorization");
        var hits = meter.CreateCounter<long>("permissions.cache.hits");
        var sessions = new MeasuredSessionService(new Sessions());
        string address = "";
        app.MapGet("/target", () => "ok");
        app.MapGet("/healthy", () => "ok");
        app.MapGet("/ready", () => "ok");
        app.MapGet("/probe", async (ILogger<TelemetryTests> logger) =>
        {
            logger.LogInformation("Telemetry correlation probe");
            hits.Add(1);
            await sessions.ValidateAsync("session", "user");
            using var downstream = new HttpClient();
            return await downstream.GetStringAsync(address + "/target");
        });
        await app.StartAsync();
        try
        {
            address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var client = new HttpClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, address + "/probe");
            const string upstreamTrace = "12345678901234567890123456789012";
            request.Headers.Add("traceparent", "00-" + upstreamTrace + "-1234567890123456-01");
            HttpResponseMessage response;
            using (SuppressInstrumentationScope.Begin()) response = await client.SendAsync(request);
            using var responseScope = response;
            response.EnsureSuccessStatusCode();
            await client.GetStringAsync(address + "/healthy");
            await client.GetStringAsync(address + "/ready");
            Assert.IsTrue(app.Services.GetRequiredService<TracerProvider>().ForceFlush());
            Assert.IsTrue(reader.Collect());
            var auth = spans.Items.Single(span => span.Name == "authentication.validate");
            var server = spans.Items.Single(span => span.Kind == ActivityKind.Server && span.TraceId == auth.TraceId && span.SpanId == auth.ParentSpanId);
            Assert.AreEqual(upstreamTrace, server.TraceId.ToString());
            Assert.IsTrue(spans.Items.Any(span => span.Kind == ActivityKind.Client && span.TraceId == auth.TraceId));
            Assert.IsFalse(spans.Items.Any(span => span.Kind == ActivityKind.Server && (span.Path == "/healthy" || span.Path == "/ready")));
            Assert.IsTrue(logs.Items.Any(log => log.Message == "Telemetry correlation probe" && log.TraceId == server.TraceId));
            Assert.IsTrue(metrics.Items.Contains("http.server.request.duration"));
            Assert.IsTrue(metrics.Items.Contains("authentication.validation.duration"));
            Assert.IsTrue(metrics.Items.Contains("permissions.cache.hits"));
            var resource = app.Services.GetRequiredService<TracerProvider>().GetResource().Attributes.ToDictionary(pair => pair.Key, pair => pair.Value);
            Assert.AreEqual("telemetry-test", resource["service.name"]);
            Assert.AreEqual("TelemetryTest", resource["deployment.environment.name"]);
            foreach (var provider in new BaseProvider[] { app.Services.GetRequiredService<MeterProvider>(), app.Services.GetRequiredService<LoggerProvider>() })
            {
                var signalResource = provider.GetResource().Attributes.ToDictionary(pair => pair.Key, pair => pair.Value);
                Assert.AreEqual(resource["service.name"], signalResource["service.name"]);
                Assert.AreEqual(resource["service.instance.id"], signalResource["service.instance.id"]);
            }
        }
        finally { await app.StopAsync(); }
    }

    [TestMethod]
    public async Task OtlpExportsAllSignalsToConfiguredHttpCollector()
    {
        var received = new ConcurrentQueue<(string Path, string Body)>();
        var receiverBuilder = WebApplication.CreateBuilder();
        receiverBuilder.Logging.ClearProviders();
        receiverBuilder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var receiver = receiverBuilder.Build();
        receiver.MapPost("/healthy/v1/{signal}", async context =>
        {
            using var body = new MemoryStream();
            await context.Request.Body.CopyToAsync(body);
            received.Enqueue((context.Request.Path.Value!, Encoding.UTF8.GetString(body.ToArray())));
            context.Response.ContentType = "application/x-protobuf";
            context.Response.StatusCode = 200;
        });
        await receiver.StartAsync();
        try
        {
            var endpoint = receiver.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single() + "/healthy/";
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenTelemetry:OtlpEnabled"] = "true",
                ["OpenTelemetry:ServiceName"] = "otlp-test",
                ["OTEL_EXPORTER_OTLP_ENDPOINT"] = endpoint,
                ["OTEL_EXPORTER_OTLP_PROTOCOL"] = "http/protobuf"
            });
            builder.AddProjectTelemetry();
            builder.Host.UseSerilog((context, logger) => logger.MinimumLevel.Information(), writeToProviders: true);
            await using var app = builder.Build();
            await app.StartAsync();
            var traces = app.Services.GetRequiredService<TracerProvider>();
            var metrics = app.Services.GetRequiredService<MeterProvider>();
            using var source = new ActivitySource("My.XXX.OTLPTest");
            using var meter = new Meter("My.XXX.Authorization");
            var counter = meter.CreateCounter<long>("permissions.otlp.probe");
            using (source.StartActivity("otlp-test-span"))
            {
                counter.Add(1);
                app.Services.GetRequiredService<ILogger<TelemetryTests>>().LogInformation("OTLP log probe");
            }
            Assert.IsTrue(traces.ForceFlush(5000));
            Assert.IsTrue(metrics.ForceFlush(5000));
            await app.StopAsync();
            await app.DisposeAsync();
            Assert.IsTrue(received.Any(item => item.Path.EndsWith("/traces") && item.Body.Contains("otlp-test-span")));
            Assert.IsTrue(received.Any(item => item.Path.EndsWith("/metrics") && item.Body.Contains("permissions.otlp.probe")));
            Assert.IsTrue(received.Any(item => item.Path.EndsWith("/logs") && item.Body.Contains("OTLP log probe")), string.Join(", ", received.Select(item => item.Path)));
        }
        finally { await receiver.StopAsync(); }
    }

    private sealed record SpanSnapshot(string Name, ActivityKind Kind, ActivityTraceId TraceId,
        ActivitySpanId SpanId, ActivitySpanId ParentSpanId, string? Path);
    private sealed record LogSnapshot(string? Message, ActivityTraceId TraceId);
    private sealed class CaptureExporter<T, TSnapshot>(Func<T, TSnapshot> snapshot) : BaseExporter<T> where T : class
    {
        public ConcurrentQueue<TSnapshot> Items { get; } = new();
        public override ExportResult Export(in Batch<T> batch)
        {
            foreach (var item in batch) Items.Enqueue(snapshot(item));
            return ExportResult.Success;
        }
    }
    private sealed class Sessions : ISessionService
    {
        public Task<Result<ActiveSession>> ValidateAsync(string sessionId, string? userId, string? refreshTokenId = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Fail<ActiveSession>("invalid"));
        public Task<Result<SessionTokens>> IssueAsync(string userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Ok(new SessionTokens("access", "refresh", 5)));
    }
}
