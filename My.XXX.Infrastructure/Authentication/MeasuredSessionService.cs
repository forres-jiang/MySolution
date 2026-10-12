using My.XXX.Application.Authentication.Models;
using My.XXX.Application.Authentication.Ports;
using My.XXX.Domain.Authentication.Models;
using FluentResults;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;

namespace My.XXX.Infrastructure.Authentication;

public sealed class MeasuredSessionService(ISessionService inner) : ISessionService
{
    private static readonly ActivitySource Activities = new("My.XXX.Authentication");
    private static readonly Meter Meter = new("My.XXX.Authentication");
    private static readonly Histogram<double> ValidationDuration = Meter.CreateHistogram<double>("authentication.validation.duration", "ms");

    public async Task<Result<ActiveSession>> ValidateAsync(string sessionId, string? userId,
        string? refreshTokenId = null, CancellationToken cancellationToken = default)
    {
        using var activity = Activities.StartActivity("authentication.validate");
        var start = Stopwatch.GetTimestamp();
        try
        {
            var result = await inner.ValidateAsync(sessionId, userId, refreshTokenId, cancellationToken);
            activity?.SetTag("authentication.outcome", result.IsSuccess ? "success" : "rejected");
            return result;
        }
        catch (System.OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            activity?.SetTag("authentication.outcome", "cancelled");
            throw;
        }
        catch (System.Exception exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            activity?.SetTag("error.type", exception.GetType().FullName);
            throw;
        }
        finally { ValidationDuration.Record(Stopwatch.GetElapsedTime(start).TotalMilliseconds); }
    }

    public async Task<Result<SessionTokens>> IssueAsync(string userId, CancellationToken cancellationToken = default)
    {
        using var activity = Activities.StartActivity("authentication.issue");
        try
        {
            var result = await inner.IssueAsync(userId, cancellationToken);
            activity?.SetTag("authentication.outcome", result.IsSuccess ? "success" : "rejected");
            return result;
        }
        catch (System.OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (System.Exception exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            activity?.SetTag("error.type", exception.GetType().FullName);
            throw;
        }
    }
}
