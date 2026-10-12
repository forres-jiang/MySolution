using My.XXX.Application.Authentication.Models;
using My.XXX.Application.Authentication.Ports;
using My.XXX.Domain.Authentication.Models;
using My.XXX.Infrastructure.Authentication;
using FluentResults;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;

namespace My.XXX.IntegrationTests;

[TestClass]
public sealed class SessionMetricsTests
{
    [TestMethod]
    public async Task DecoratorPreservesFailuresCancellationAndIssuanceWhileRecordingValidation()
    {
        var measurements = 0;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, owner) =>
        {
            if (instrument.Meter.Name == "My.XXX.Authentication" && instrument.Name == "authentication.validation.duration")
                owner.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<double>((instrument, value, tags, state) => Interlocked.Increment(ref measurements));
        listener.Start();
        var inner = new Sessions();
        var service = new MeasuredSessionService(inner);
        var failed = await service.ValidateAsync("session", "user", "refresh");
        Assert.AreSame(inner.Failure, failed);
        Assert.AreSame(inner.Tokens, await service.IssueAsync("user"));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => service.ValidateAsync("session", "user", "refresh", cancellation.Token));
        Assert.IsTrue(measurements >= 2);
    }

    private sealed class Sessions : ISessionService
    {
        public Result<ActiveSession> Failure { get; } = Result.Fail<ActiveSession>("invalid");
        public Result<SessionTokens> Tokens { get; } = Result.Ok(new SessionTokens("access", "refresh", 5));
        public Task<Result<ActiveSession>> ValidateAsync(string sessionId, string? userId, string? refreshTokenId = null, CancellationToken cancellationToken = default)
        {
            Assert.AreEqual("session", sessionId);
            Assert.AreEqual("user", userId);
            Assert.AreEqual("refresh", refreshTokenId);
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Failure);
        }
        public Task<Result<SessionTokens>> IssueAsync(string userId, CancellationToken cancellationToken = default) => Task.FromResult(Tokens);
    }
}
