using System.Diagnostics;

namespace My.XXX.APIs.Contracts.Shared;

public sealed class APIResponse<T>
{
    public int StatusCode { get; }
    public string Message { get; }
    public T? Data { get; }
    public string? Code { get; }
    public string? TraceId { get; }
    public APIResponse(int statusCode, string message, T? data, string? code = null, string? traceId = null)
    {
        StatusCode = statusCode;
        Message = message;
        Data = data;
        Code = code;
        TraceId = traceId ?? Activity.Current?.TraceId.ToString();
    }
}