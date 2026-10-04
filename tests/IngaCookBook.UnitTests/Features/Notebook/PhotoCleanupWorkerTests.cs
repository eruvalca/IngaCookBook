using System.Diagnostics.CodeAnalysis;
using IngaCookBook.Data;
using IngaCookBook.Features.Notebook.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using NSubstitute;
using Shouldly;
using Xunit;

namespace IngaCookBook.UnitTests.Features.Notebook;

[SuppressMessage("Maintainability", "CA1515:Consider making public types internal", Justification = "xUnit requires public test classes for discovery.")]
public sealed class PhotoCleanupWorkerTests
{
    [Theory]
    [InlineData("connection")]
    [InlineData("save")]
    [InlineData("retries")]
    public async Task TransientDatabaseFailuresKeepTheWorkerAliveAndLogADiagnostic(string failure)
    {
        var transient = new NpgsqlException("Connection interrupted", new TimeoutException());
        Exception error = failure switch
        {
            "save" => new DbUpdateException("Save interrupted", transient),
            "retries" => new RetryLimitExceededException("Retries exhausted", transient),
            _ => transient,
        };
        await using var provider = Services(error);
        var logged = new TaskCompletionSource<(LogLevel Level, EventId Event, Exception? Error)>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var worker = new PhotoCleanupWorker(provider.GetRequiredService<IServiceScopeFactory>(), new CleanupLogger((level, id, exception) => logged.TrySetResult((level, id, exception))));
        await worker.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            var entry = await logged.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            entry.Level.ShouldBe(LogLevel.Warning);
            entry.Event.Id.ShouldBe(2101);
            entry.Error.ShouldBeSameAs(error);
            worker.ExecuteTask.ShouldNotBeNull().IsCompleted.ShouldBeFalse();
        }
        finally
        {
            await worker.StopAsync(TestContext.Current.CancellationToken);
        }
        worker.ExecuteTask.IsCompleted.ShouldBeTrue();
    }

    [Fact]
    public async Task UnexpectedFailureIsNotHiddenAsARetryableOutage()
    {
        var error = new InvalidOperationException("Invalid cleanup configuration");
        await using var provider = Services(error);
        using var worker = new PhotoCleanupWorker(provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<PhotoCleanupWorker>.Instance);
        await worker.StartAsync(TestContext.Current.CancellationToken);

        var actual = await Should.ThrowAsync<InvalidOperationException>(() => worker.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));

        actual.ShouldBeSameAs(error);
    }

    private static ServiceProvider Services(Exception error)
    {
        var factory = Substitute.For<IDbContextFactory<ApplicationDbContext>>();
        factory.CreateDbContextAsync(Arg.Any<CancellationToken>()).Returns(_ => Task.FromException<ApplicationDbContext>(error));
        var services = new ServiceCollection();
        services.AddSingleton(factory);
        services.AddSingleton(Substitute.For<IRecipePhotoStore>());
        services.AddSingleton<ILogger<PhotoCleanupProcessor>>(NullLogger<PhotoCleanupProcessor>.Instance);
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<PhotoCleanupProcessor>();
        return services.BuildServiceProvider();
    }

    private sealed class CleanupLogger(Action<LogLevel, EventId, Exception?> record) : ILogger<PhotoCleanupWorker>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => record(logLevel, eventId, exception);
    }
}
