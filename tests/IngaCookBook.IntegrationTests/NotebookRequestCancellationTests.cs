using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using IngaCookBook.Features.Notebook.Endpoints;
using IngaCookBook.SharedKernel.Notebook;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

namespace IngaCookBook.IntegrationTests;

[SuppressMessage("Maintainability", "CA1515:Consider making public types internal", Justification = "xUnit requires public test classes for discovery.")]
public sealed class NotebookRequestCancellationTests
{
    [Fact]
    public async Task AbortingRealHttpRequestCancelsNotebookEndpointAndEfQuery()
    {
        var ct = TestContext.Current.CancellationToken;
        var query = new PendingQuery();
        await using var store = await NotebookTestStore.CreateAsync(ct, query);
        var service = await store.OwnerAsync("request-owner", ct);
        (await service.CreateWorkspaceAsync(new("Kitchen", "USD"), ct)).ShouldBeOfType<ChangeSaved>();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing",
            ApplicationName = typeof(NotebookRequestCancellationTests).Assembly.GetName().Name,
        });
        builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Services.AddAuthorization();
        builder.Services.AddAntiforgery();
        builder.Services.AddSingleton<INotebookService>(service);
        builder.Services.AddSingleton(service);
        await using var app = builder.Build();
        // Only this disposable test host supplies an authenticated principal.
        app.Use((context, next) =>
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "request-owner")], "test"));
            return next(context);
        });
        app.UseAuthorization();
        app.UseAntiforgery();
        app.MapNotebookEndpoints();
        await app.StartAsync(ct);
        using var http = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        using var request = CancellationTokenSource.CreateLinkedTokenSource(ct);
        query.Armed = true;
        var pending = http.GetAsync(new Uri("/api/notebook/recipes", UriKind.Relative), request.Token);
        var databaseToken = await query.Started.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
        await request.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(async () => { using var response = await pending; });
        await query.Canceled.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
        databaseToken.IsCancellationRequested.ShouldBeTrue();
    }

    private sealed class PendingQuery : DbCommandInterceptor
    {
        internal bool Armed { get; set; }
        internal TaskCompletionSource<CancellationToken> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Canceled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (Armed)
            {
                Started.TrySetResult(cancellationToken);
                try { await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken); }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    Canceled.TrySetResult();
                    throw;
                }
            }
            return result;
        }
    }
}
