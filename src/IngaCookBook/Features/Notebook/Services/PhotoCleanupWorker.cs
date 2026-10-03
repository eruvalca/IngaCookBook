using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace IngaCookBook.Features.Notebook.Services;

internal sealed partial class PhotoCleanupWorker(IServiceScopeFactory scopes, ILogger<PhotoCleanupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<PhotoCleanupProcessor>().ProcessAsync(stoppingToken);
            }
            catch (NpgsqlException exception) when (exception.IsTransient)
            {
                LogDatabaseUnavailable(logger, exception);
            }
            catch (DbUpdateException exception) when (exception.InnerException is NpgsqlException { IsTransient: true })
            {
                LogDatabaseUnavailable(logger, exception);
            }
            catch (RetryLimitExceededException exception) when (exception.InnerException is NpgsqlException { IsTransient: true })
            {
                LogDatabaseUnavailable(logger, exception);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    [LoggerMessage(EventId = 2101, Level = LogLevel.Warning, Message = "Photo cleanup is waiting for the database to become available.")]
    private static partial void LogDatabaseUnavailable(ILogger logger, Exception exception);
}
