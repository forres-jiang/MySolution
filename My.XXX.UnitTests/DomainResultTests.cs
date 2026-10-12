using My.XXX.Application.Abstractions.Results;
using My.XXX.Domain.Abstractions;
using My.XXX.Domain.Menus.Policies;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace My.XXX.UnitTests;

[TestClass]
public sealed class DomainResultTests
{
    [TestMethod]
    public void FailureCopiesErrorsAndCannotExposeASuccessValue()
    {
        var errors = new List<BusinessError> { MenuErrors.NotFound() };
        var result = DomainResult.Fail<int>(errors);
        errors.Clear();
        Assert.IsTrue(result.IsFailed);
        Assert.AreEqual("Menu.NotFound", result.Errors.Single().Code);
        Assert.ThrowsExactly<InvalidOperationException>(() => _ = result.Value);
        Assert.ThrowsExactly<ArgumentException>(() => DomainResult.Fail(Array.Empty<BusinessError>()));
        Assert.ThrowsExactly<ArgumentException>(() => DomainResult.Fail(new BusinessError[] { null! }));
    }

    [TestMethod]
    public void ApplicationAdapterPreservesAllDomainErrorsAndSuccessValues()
    {
        var domain = DomainResult.Fail<int>(new[] { MenuErrors.NotFound(), MenuErrors.HasChildren() });
        var result = domain.ToResult();
        Assert.IsTrue(result.IsFailed);
        CollectionAssert.AreEqual(domain.Errors.Select(error => error.Code).ToArray(),
            result.Errors.Cast<BusinessFailure>().Select(error => error.Code).ToArray());
        Assert.AreEqual(BusinessErrorKind.Conflict, ((BusinessFailure)result.Errors[1]).Kind);
        Assert.AreEqual(domain.Errors[1].Message, result.Errors[1].Message);
        Assert.AreEqual(42, DomainResult.Ok(42).ToResult().Value);
        Assert.IsTrue(DomainResult.Ok().ToResult().IsSuccess);
    }
}
