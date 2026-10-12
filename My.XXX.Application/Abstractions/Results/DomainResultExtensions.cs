using My.XXX.Domain.Abstractions;
using FluentResults;
using System;
using System.Linq;

namespace My.XXX.Application.Abstractions.Results;

/// <summary>Preserves domain error identity when crossing into application results.</summary>
public sealed class BusinessFailure : Error
{
    public BusinessError DomainError { get; }
    public string? Code => DomainError.Code;
    public BusinessErrorKind Kind => DomainError.Kind;

    public BusinessFailure(BusinessError error) : base((error ?? throw new ArgumentNullException(nameof(error))).Message)
        => DomainError = error;
}

public static class DomainResultExtensions
{
    public static BusinessFailure ToError(this BusinessError error) => new(error);

    public static Result ToResult(this DomainResult result) => result.IsSuccess
        ? Result.Ok() : Result.Fail(result.Errors.Select(error => error.ToError()));

    public static Result<T> ToResult<T>(this DomainResult<T> result) => result.IsSuccess
        ? Result.Ok(result.Value) : Result.Fail<T>(result.Errors.Select(error => error.ToError()));
}
