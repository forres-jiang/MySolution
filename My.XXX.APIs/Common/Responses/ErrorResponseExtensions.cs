using My.XXX.APIs.Contracts.Shared;
using Microsoft.AspNetCore.Http;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace My.XXX.APIs.Common.Responses;

public static class ErrorResponseExtensions
{
    public static async Task WriteErrorAsync(this HttpResponse response, int statusCode, string message,
        string errorCode, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);
        response.StatusCode = statusCode;
        await response.WriteAsJsonAsync(
            new APIResponse<object>(0, message, null, errorCode, response.HttpContext.TraceIdentifier),
            cancellationToken);
    }
}
