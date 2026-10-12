using My.XXX.APIs.Common.Responses;
using My.XXX.APIs.Contracts.Shared;
using My.XXX.Application.Abstractions.Results;
using My.XXX.Domain.Abstractions;
using My.XXX.Domain.Menus.Policies;
using FluentResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace My.XXX.IntegrationTests;

[TestClass]
public sealed class ApiContractTests
{
    [TestMethod]
    public void DomainErrorAdapterPreservesHttpStatusAndStableCode()
    {
        foreach (var (kind, status) in new[] { (BusinessErrorKind.Validation, 400), (BusinessErrorKind.NotFound, 404),
            (BusinessErrorKind.Conflict, 409), (BusinessErrorKind.Unauthorized, 401), (BusinessErrorKind.Forbidden, 403) })
        {
            var result = DomainResult.Fail(new BusinessError("message", "Stable.Code", kind)).ToResult();
            var response = (ObjectResult)result.ToHttpResult("trace").Result!;
            Assert.AreEqual(status, response.StatusCode);
            var body = (APIResponse<bool>)response.Value!;
            Assert.AreEqual("Stable.Code", body.Code);
            Assert.AreEqual("trace", body.TraceId);
            Assert.IsFalse(body.Data);
        }
    }

    [TestMethod]
    public void FailedMappingDoesNotReadValueOrInvokeMapper()
    {
        var result = Result.Fail<string>(MenuErrors.NotFound().ToError());
        var response = (ObjectResult)result.ToMappedHttpResult<string, string>(
            _ => throw new System.InvalidOperationException("Must not map failures."), "trace").Result!;
        Assert.AreEqual(404, response.StatusCode);
        var body = (APIResponse<string>)response.Value!;
        Assert.IsNull(body.Data);
        Assert.AreEqual("Menu.NotFound", body.Code);
        Assert.AreEqual("trace", body.TraceId);
        Assert.AreEqual(0, body.StatusCode);
    }

    [TestMethod]
    public void SuccessMapsDataAndUnknownFailureHasStableFallback()
    {
        var success = (ObjectResult)Result.Ok("value").ToMappedHttpResult(value => value.Length).Result!;
        Assert.AreEqual(200, success.StatusCode);
        Assert.AreEqual(5, ((APIResponse<int>)success.Value!).Data);
        var failure = (ObjectResult)Result.Fail<string>("failure").ToHttpResult().Result!;
        Assert.AreEqual(400, failure.StatusCode);
        Assert.AreEqual("Request.Failed", ((APIResponse<string>)failure.Value!).Code);
    }
}
