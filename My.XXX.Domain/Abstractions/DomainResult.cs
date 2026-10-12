using System;
using System.Collections.Generic;
using System.Linq;

namespace My.XXX.Domain.Abstractions;

/// <summary>A rule outcome independent of application libraries and transport protocols.</summary>
public class DomainResult
{
    public IReadOnlyList<BusinessError> Errors { get; }
    public bool IsSuccess => Errors.Count == 0;
    public bool IsFailed => !IsSuccess;

    private protected DomainResult(IEnumerable<BusinessError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        var snapshot = errors.ToArray();
        if (snapshot.Any(error => error == null)) throw new ArgumentException("Errors cannot contain null.", nameof(errors));
        Errors = Array.AsReadOnly(snapshot);
    }

    public static DomainResult Ok() => new(Array.Empty<BusinessError>());
    public static DomainResult<T> Ok<T>(T value) => new(value, Array.Empty<BusinessError>());
    public static DomainResult Fail(BusinessError error) => Fail(new[] { error });
    public static DomainResult Fail(IEnumerable<BusinessError> errors) => new(FailureErrors(errors));
    public static DomainResult<T> Fail<T>(BusinessError error) => Fail<T>(new[] { error });
    public static DomainResult<T> Fail<T>(IEnumerable<BusinessError> errors) => new(default!, FailureErrors(errors));
    public static DomainResult OkIf(bool condition, BusinessError error) => condition ? Ok() : Fail(error);

    private static BusinessError[] FailureErrors(IEnumerable<BusinessError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        var snapshot = errors.ToArray();
        if (snapshot.Length == 0) throw new ArgumentException("A failure requires at least one error.", nameof(errors));
        return snapshot;
    }
}

public sealed class DomainResult<T> : DomainResult
{
    private readonly T value;
    public T Value => IsSuccess ? value : throw new InvalidOperationException("A failed rule has no value.");

    internal DomainResult(T value, IEnumerable<BusinessError> errors) : base(errors) => this.value = value;
}
