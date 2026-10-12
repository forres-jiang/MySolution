using My.XXX.APIs.Contracts.Shared;
using My.XXX.Application.Abstractions.Results;
using My.XXX.Domain.Abstractions;
using FluentResults;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;

namespace My.XXX.APIs.Common.Responses;

/// <summary>将应用结果转换为强类型响应，并保留业务错误码与 HTTP 状态。</summary>
public static class ResultResponseExtensions
{
    public static ActionResult<APIResponse<bool>> ToHttpResult(this Result result, string? traceId = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        return CreateResponse(result, result.IsSuccess, traceId);
    }

    public static ActionResult<APIResponse<T>> ToHttpResult<T>(this Result<T> result, string? traceId = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        return CreateResponse<T>(result, result.IsSuccess ? result.Value : default, traceId);
    }

    public static ActionResult<APIResponse<TOutput>> ToMappedHttpResult<TInput, TOutput>(
        this Result<TInput> result, Func<TInput, TOutput> map, string? traceId = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(map);
        // 失败结果没有可读取的 Value，也不应执行 DTO 映射。
        return CreateResponse<TOutput>(result, result.IsSuccess ? map(result.Value) : default, traceId);
    }

    private static ActionResult<APIResponse<T>> CreateResponse<T>(ResultBase result, T? data, string? traceId)
    {
        var failure = result.Errors.OfType<BusinessFailure>().FirstOrDefault();
        var response = new APIResponse<T>(result.IsSuccess ? 1 : 0,
            result.IsSuccess ? "Success" : string.Join(",", result.Errors.Select(error => error.Message)),
            data, result.IsSuccess ? null : failure?.Code ?? "Request.Failed", traceId);
        return new ObjectResult(response)
        {
            StatusCode = result.IsSuccess ? StatusCodes.Status200OK : HttpStatus(failure)
        };
    }

    private static int HttpStatus(BusinessFailure? failure) => failure?.Kind switch
    {
        BusinessErrorKind.NotFound => StatusCodes.Status404NotFound,
        BusinessErrorKind.Conflict => StatusCodes.Status409Conflict,
        BusinessErrorKind.Unauthorized => StatusCodes.Status401Unauthorized,
        BusinessErrorKind.Forbidden => StatusCodes.Status403Forbidden,
        _ => StatusCodes.Status400BadRequest
    };
}
