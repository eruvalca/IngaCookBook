using System.Globalization;
using IngaCookBook.Features.Notebook.Data;
using IngaCookBook.SharedKernel.Notebook;

namespace IngaCookBook.Features.Notebook.Services;

internal sealed partial class NotebookService
{
    internal const int MaximumPhotoBytes = 10 * 1024 * 1024;

    public async Task<NotebookChange> UploadPhotoAsync(Guid recipeId, Guid versionId, Stream content,
        string fileName, string contentType, CancellationToken cancellationToken)
    {
        var workspace = await GetWorkspaceAsync(cancellationToken);
        var recipe = await GetRecipeAsync(recipeId, cancellationToken);
        if (workspace is null || recipe is null || !recipe.Versions.Any(v => v.Id == versionId))
        {
            return new ChangeRejected("This version could not be found.", 404);
        }
        await using var bytes = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = await content.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (bytes.Length + read > MaximumPhotoBytes)
            {
                return new ChangeRejected("Choose a photo smaller than 10 MB.", 413);
            }
            await bytes.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
        var detectedType = PhotoFormat.Detect(bytes.GetBuffer().AsSpan(0, checked((int)bytes.Length)));
        if (detectedType is null || !string.Equals(detectedType, contentType, StringComparison.OrdinalIgnoreCase))
        {
            return new ChangeRejected("Choose a JPEG, PNG, or WebP photo.");
        }
        var id = Guid.NewGuid();
        var key = PhotoKey(workspace.Id, recipeId, versionId, id);
        bytes.Position = 0;
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            await photos.SaveAsync(key, bytes, detectedType, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Azure may have accepted the bytes before cancellation was observed.
            // No metadata write has started, so this unique key is safe to reclaim.
            await QueueUnconfirmedPhotoAsync(key);
            throw;
        }
        catch (Exception exception) when (PhotoStorageFailure.IsExpected(exception))
        {
            LogPhotoStorageFailure(logger, exception, id);
            // An upload failure can have an ambiguous remote result. Its unique key can
            // safely be cleaned up even if Azure accepted the bytes before losing the reply.
            await QueueUnconfirmedPhotoAsync(key);
            return new ChangeRejected("Photo storage is temporarily unavailable. This photo was not added to your notebook; please try again. Any unconfirmed upload is queued for cleanup.", 503);
        }
        var photo = new RecipePhoto(id, Path.GetFileNameWithoutExtension(fileName)[..Math.Min(Path.GetFileNameWithoutExtension(fileName).Length, 200)],
            detectedType, DateTimeOffset.UtcNow);
        // Point of no cancellation: the blob is stored. Finish the metadata write
        // even if the caller leaves. EF/Npgsql command timeouts still apply. Canceling
        // this write and then deleting the blob could delete a successfully committed
        // photo when only the database's commit acknowledgement was lost.
        var result = await UpdateAsync(recipeId, recipe.Revision,
            current => Replace(current, versionId, v => v with { Photos = [.. v.Photos, photo] }), id, CancellationToken.None);
        if (result is ChangeRejected)
        {
            // The blob is not referenced if a concurrent edit wins; compensate only this upload.
            try
            {
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await photos.DeleteAsync(key, cleanup.Token);
            }
            catch (Exception exception) when (PhotoStorageFailure.IsExpected(exception) || exception is OperationCanceledException)
            {
                LogPhotoStorageFailure(logger, exception, id);
                await QueueUnconfirmedPhotoAsync(key);
            }
        }
        return result;
    }

    internal async Task<(Stream Content, string ContentType)?> OpenPhotoAsync(Guid recipeId, Guid versionId, Guid photoId, CancellationToken cancellationToken)
    {
        var workspace = await GetWorkspaceAsync(cancellationToken);
        var recipe = await GetRecipeAsync(recipeId, cancellationToken);
        var photo = recipe?.Versions.FirstOrDefault(v => v.Id == versionId)?.Photos.FirstOrDefault(p => p.Id == photoId);
        if (workspace is null || photo is null)
        {
            return null;
        }
        var content = await photos.OpenAsync(PhotoKey(workspace.Id, recipeId, versionId, photoId), cancellationToken);
        return (content, photo.ContentType);
    }

    private static string PhotoKey(Guid workspaceId, Guid recipeId, Guid versionId, Guid photoId) =>
        string.Create(CultureInfo.InvariantCulture, $"{workspaceId:N}/{recipeId:N}/{versionId:N}/{photoId:N}");

    private async Task QueueUnconfirmedPhotoAsync(string prefix)
    {
        // Compensation must survive request cancellation, but must not wait forever.
        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var db = await contextFactory.CreateDbContextAsync(cleanup.Token);
        db.Add(new PhotoCleanupEntity { Prefix = prefix });
        await db.SaveChangesAsync(cleanup.Token);
    }

    [LoggerMessage(EventId = 2102, Level = LogLevel.Warning, Message = "Photo storage failed for upload {PhotoId}; cleanup is being queued.")]
    private static partial void LogPhotoStorageFailure(ILogger logger, Exception exception, Guid photoId);
}
