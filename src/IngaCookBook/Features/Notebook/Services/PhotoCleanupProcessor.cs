using IngaCookBook.Data;
using IngaCookBook.Features.Notebook.Data;
using Microsoft.EntityFrameworkCore;

namespace IngaCookBook.Features.Notebook.Services;

internal sealed partial class PhotoCleanupProcessor(
    IDbContextFactory<ApplicationDbContext> contextFactory, IRecipePhotoStore photos, ILogger<PhotoCleanupProcessor> logger, TimeProvider clock)
{
    internal async Task ProcessAsync(CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var now = clock.GetUtcNow();
        var pending = await db.Set<PhotoCleanupEntity>().AsNoTracking().Where(j => j.NextAttemptAt <= now)
            .OrderBy(j => j.NextAttemptAt).Take(20).ToListAsync(cancellationToken);
        foreach (var job in pending)
        {
            try
            {
                await photos.DeletePrefixAsync(job.Prefix, cancellationToken);
            }
            catch (Exception exception) when (PhotoStorageFailure.IsExpected(exception))
            {
                LogCleanupDeferred(logger, exception, job.Id);
                await db.Set<PhotoCleanupEntity>().Where(j => j.Id == job.Id)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(j => j.NextAttemptAt, now.AddMinutes(1)), cancellationToken);
                continue;
            }
            // Idempotent deletion permits retries and multiple application instances.
            await db.Set<PhotoCleanupEntity>().Where(j => j.Id == job.Id).ExecuteDeleteAsync(cancellationToken);
        }
    }

    [LoggerMessage(EventId = 2100, Level = LogLevel.Warning, Message = "Photo cleanup job {JobId} will be retried.")]
    private static partial void LogCleanupDeferred(ILogger logger, Exception exception, Guid jobId);
}
