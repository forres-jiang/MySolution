
namespace My.XXX.Domain.Abstractions;

public enum BusinessErrorKind
{
    Validation,
    NotFound,
    Conflict,
    Unauthorized,
    Forbidden
}

/// <summary>业务失败类型与稳定错误码；公开响应格式由适配器决定。</summary>
public sealed record BusinessError
{
    public string Message { get; }
    public string? Code { get; }
    public BusinessErrorKind Kind { get; }

    public BusinessError(string message, string? code = null,
        BusinessErrorKind kind = BusinessErrorKind.Validation)
    {
        Message = message;
        Code = code;
        Kind = kind;
    }
}
