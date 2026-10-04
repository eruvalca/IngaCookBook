using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using IngaCookBook.Client.Services;
using IngaCookBook.SharedKernel.Notebook;
using Shouldly;
using Xunit;

namespace IngaCookBook.UnitTests.Features.Notebook;

[SuppressMessage("Maintainability", "CA1515:Consider making public types internal", Justification = "xUnit requires public test classes for discovery.")]
public sealed class HttpNotebookCancellationTests
{
    [Fact]
    public void NotebookContractRequiresAnExplicitFinalCancellationToken()
    {
        var operations = typeof(INotebookService).GetMethods();
        operations.ShouldNotBeEmpty();
        foreach (var operation in operations)
        {
            var parameter = operation.GetParameters()[^1];
            parameter.ParameterType.ShouldBe(typeof(CancellationToken), operation.Name);
            parameter.IsOptional.ShouldBeFalse(operation.Name);
        }
    }

    [Theory]
    [InlineData("workspace")]
    [InlineData("recipes")]
    [InlineData("recipe")]
    [InlineData("create-workspace")]
    [InlineData("create-recipe")]
    [InlineData("version")]
    [InlineData("vary")]
    [InlineData("delete")]
    [InlineData("batch")]
    [InlineData("correct-batch")]
    [InlineData("evaluate")]
    [InlineData("correct-evaluation")]
    [InlineData("standard")]
    [InlineData("settings")]
    [InlineData("promote")]
    public async Task EveryNotebookOperationAbortsItsHttpRequest(string operation)
    {
        using var cancellation = new CancellationTokenSource();
        var entered = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new CallbackHandler(async (request, ct) =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/token", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create("antiforgery") };
            }
            if (request.Method != HttpMethod.Get)
            {
                request.Headers.GetValues("RequestVerificationToken").ShouldBe(["antiforgery"]);
            }
            entered.SetResult(ct);
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            throw new InvalidOperationException("The request should have been canceled.");
        });
        using var http = Client(handler);
        var pending = InvokeAsync(new HttpNotebookService(http), operation, cancellation.Token);
        var transportToken = await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await cancellation.CancelAsync();
        transportToken.IsCancellationRequested.ShouldBeTrue();
        await Should.ThrowAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        pending.IsCanceled.ShouldBeTrue();
    }

    [Fact]
    public async Task CancelingAntiforgeryLookupPreventsTheWrite()
    {
        using var cancellation = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var paths = new List<string>();
        using var handler = new CallbackHandler(async (request, ct) =>
        {
            paths.Add(request.RequestUri!.AbsolutePath);
            entered.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            throw new InvalidOperationException("Canceled lookup should not finish.");
        });
        using var http = Client(handler);
        var pending = new HttpNotebookService(http).CreateRecipeAsync(new("Vanilla", "", []), cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await cancellation.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        paths.ShouldBe(["/api/notebook/token"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [SuppressMessage("Reliability", "CA2025:Do not pass disposable objects into unawaited tasks", Justification = "The conditionally selected upload task is awaited in the try/catch before the stream leaves this scope; CA2025 does not follow the conditional Task assignment.")]
    public async Task CancellationReachesStreamingResponseAndUploadBodies(bool upload)
    {
        using var cancellation = new CancellationTokenSource();
        await using var body = new PendingReadStream();
        using var handler = new CallbackHandler(async (request, ct) =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/token", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create("token") };
            }
            if (upload)
            {
                await request.Content!.CopyToAsync(Stream.Null, ct);
                throw new InvalidOperationException("Canceled upload should not finish.");
            }
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(body) };
        });
        using var http = Client(handler);
        var service = new HttpNotebookService(http);
        Task pending = upload
            ? service.UploadPhotoAsync(Guid.NewGuid(), Guid.NewGuid(), body, "cream.png", "image/png", cancellation.Token)
            : service.GetWorkspaceAsync(cancellation.Token);
        var readToken = await body.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await cancellation.CancelAsync();
        readToken.IsCancellationRequested.ShouldBeTrue();
        OperationCanceledException? canceled = null;
        try { await pending; }
        catch (OperationCanceledException exception) { canceled = exception; }
        canceled.ShouldNotBeNull();
    }

    private static HttpClient Client(HttpMessageHandler handler) => new(handler) { BaseAddress = new Uri("https://notebook.test/") };

    private static Task InvokeAsync(HttpNotebookService service, string operation, CancellationToken ct)
    {
        var recipe = Guid.NewGuid();
        var version = Guid.NewGuid();
        var revision = Guid.NewGuid();
        var batch = new BatchRequest(revision, DateTimeOffset.UtcNow, "");
        var evaluation = new EvaluationRequest(revision, DateTimeOffset.UtcNow, "", "", []);
        return operation switch
        {
            "workspace" => service.GetWorkspaceAsync(ct),
            "recipes" => service.GetRecipesAsync(ct),
            "recipe" => service.GetRecipeAsync(recipe, ct),
            "create-workspace" => service.CreateWorkspaceAsync(new("Kitchen", "USD"), ct),
            "create-recipe" => service.CreateRecipeAsync(new("Vanilla", "", []), ct),
            "version" => service.SaveVersionAsync(recipe, version, new(revision, new(), null), ct),
            "vary" => service.VaryAsync(recipe, version, new(revision), ct),
            "delete" => service.DeleteDraftAsync(recipe, version, new(revision), ct),
            "batch" => service.MakeBatchAsync(recipe, version, batch, ct),
            "correct-batch" => service.CorrectBatchAsync(recipe, version, Guid.NewGuid(), new(batch, "Reason"), ct),
            "evaluate" => service.EvaluateAsync(recipe, version, Guid.NewGuid(), evaluation, ct),
            "correct-evaluation" => service.CorrectEvaluationAsync(recipe, version, Guid.NewGuid(), Guid.NewGuid(), new(evaluation, "Reason"), ct),
            "standard" => service.SetStandardAsync(recipe, version, new(revision), ct),
            "settings" => service.SaveSettingsAsync(recipe, new(revision, "Vanilla", "", []), ct),
            "promote" => service.PromoteAsync(recipe, version, new(revision, "New recipe"), ct),
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };
    }

    private sealed class CallbackHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> callback) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => callback(request, cancellationToken);
    }

    private sealed class PendingReadStream : Stream
    {
        internal TaskCompletionSource<CancellationToken> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Started.TrySetResult(cancellationToken);
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        }
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        public override void Flush() => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
