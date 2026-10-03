using Azure;

namespace IngaCookBook.Features.Notebook.Services;

internal static class PhotoStorageFailure
{
    internal static bool IsExpected(Exception exception) => exception is RequestFailedException ||
        exception is AggregateException { InnerExceptions.Count: > 0 } aggregate &&
        aggregate.Flatten().InnerExceptions.All(e => e is RequestFailedException);
}
