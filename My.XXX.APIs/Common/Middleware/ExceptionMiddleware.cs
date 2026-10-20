using My.XXX.APIs.Common.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace My.XXX.APIs.Common.Middleware;

public sealed class ExceptionHandlingMiddleware : IMiddleware
{
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(ILogger<ExceptionHandlingMiddleware> logger)
    {
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var traceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;

        context.TraceIdentifier = traceId;
        context.Response.Headers["X-Request-Id"] = traceId;

        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception for {Method} {Path} (RequestId={RequestId})",
                context.Request.Method, context.Request.Path, traceId);

            if (context.Response.HasStarted)
            {
                context.Abort();
            }
            else
            {
                // Clear() 会连同响应头一起清除，因此关联 ID 必须重新写入。
                context.Response.Clear();
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                context.Response.Headers["X-Request-Id"] = traceId;
                await context.Response.WriteErrorAsync(StatusCodes.Status500InternalServerError,
                    "An internal error occurred.", "Server.Unexpected", context.RequestAborted);
            }
        }
    }
}
