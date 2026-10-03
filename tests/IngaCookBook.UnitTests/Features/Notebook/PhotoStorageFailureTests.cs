using System.Diagnostics.CodeAnalysis;
using Azure;
using IngaCookBook.Features.Notebook.Services;
using Shouldly;
using Xunit;

namespace IngaCookBook.UnitTests.Features.Notebook;

[SuppressMessage("Maintainability", "CA1515:Consider making public types internal", Justification = "xUnit requires public test classes for discovery.")]
public sealed class PhotoStorageFailureTests
{
    [Fact]
    public void EmptyOrMixedAggregatesAreNotStorageOutages()
    {
        PhotoStorageFailure.IsExpected(new AggregateException()).ShouldBeFalse();
        PhotoStorageFailure.IsExpected(new AggregateException(new RequestFailedException(503, "Storage"), new InvalidOperationException("Unexpected"))).ShouldBeFalse();
        PhotoStorageFailure.IsExpected(new AggregateException(new OperationCanceledException())).ShouldBeFalse();
    }
}
