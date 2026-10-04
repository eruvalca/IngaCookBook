using System.Diagnostics.CodeAnalysis;
using Azure;
using IngaCookBook.Features.Notebook.Data;
using IngaCookBook.Features.Notebook.Services;
using IngaCookBook.SharedKernel.Notebook;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace IngaCookBook.IntegrationTests;

[SuppressMessage("Maintainability", "CA1515:Consider making public types internal", Justification = "xUnit requires public test classes for discovery.")]
public sealed partial class PhotoStorageFailureTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnconfirmedUploadReturnsRecoverableFailureAndQueuesOnlyItsBlob(bool aggregate)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var store = await NotebookTestStore.CreateAsync(ct);
        var service = await store.OwnerAsync("owner", ct);
        var recipe = await CreateAsync(service, ct);
        // One earlier photo is already committed; only the ambiguous upload can be removed.
        (await UploadAsync(service, recipe, ct)).ShouldBeOfType<ChangeSaved>();
        recipe = (await service.GetRecipeAsync(recipe.Id, ct))!;
        var previousKey = store.Photos.Items.Keys.Single();
        Exception failure = new RequestFailedException(503, "Response lost");
        if (aggregate)
        {
            failure = new AggregateException(failure, new AggregateException(new RequestFailedException(503, "Retry failed")));
        }
        store.Photos.AfterSave = () => Task.FromException(failure);
        var rejected = (await UploadAsync(service, recipe, ct)).ShouldBeOfType<ChangeRejected>();
        rejected.Status.ShouldBe(503);
        rejected.Message.ShouldContain("queued for cleanup");
        store.Photos.Items.Count.ShouldBe(2);
        var saved = (await service.GetRecipeAsync(recipe.Id, ct))!;
        saved.Revision.ShouldBe(recipe.Revision);
        saved.Versions[0].Photos.Select(p => p.Id).ShouldBe(recipe.Versions[0].Photos.Select(p => p.Id));
        await using (var db = await store.Factory.CreateDbContextAsync(ct))
        {
            var job = await db.Set<PhotoCleanupEntity>().SingleAsync(ct);
            job.Prefix.ShouldBe(store.Photos.Items.Keys.Single(key => !string.Equals(key, previousKey, StringComparison.Ordinal)));
        }
        await ProcessCleanupAsync(store, ct);
        store.Photos.Items.Keys.ShouldBe([previousKey]);
        await using var completed = await store.Factory.CreateDbContextAsync(ct);
        (await completed.Set<PhotoCleanupEntity>().CountAsync(ct)).ShouldBe(0);
    }

    [Fact]
    public async Task FailedCompensationPreservesConflictAndQueuesCleanup()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var store = await NotebookTestStore.CreateAsync(ct);
        var service = await store.OwnerAsync("owner", ct);
        var recipe = await CreateAsync(service, ct);
        var version = recipe.Versions[0];
        store.Photos.AfterSave = async () =>
        {
            (await service.SaveVersionAsync(recipe.Id, version.Id,
                new(recipe.Revision, version.Content with { Notes = "Concurrent change" }, null), ct)).ShouldBeOfType<ChangeSaved>();
        };
        store.Photos.DeleteFailure = new RequestFailedException(503, "Delete unavailable");
        (await UploadAsync(service, recipe, ct)).ShouldBeOfType<ChangeRejected>().Status.ShouldBe(409);
        var saved = (await service.GetRecipeAsync(recipe.Id, ct))!;
        saved.Versions[0].Content.Notes.ShouldBe("Concurrent change");
        saved.Versions[0].Photos.ShouldBeEmpty();
        await using (var db = await store.Factory.CreateDbContextAsync(ct))
        {
            (await db.Set<PhotoCleanupEntity>().SingleAsync(ct)).Prefix.ShouldBe(store.Photos.Items.Keys.Single());
        }
        await ProcessCleanupAsync(store, ct);
        store.Photos.Items.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnexpectedFailuresAndCancellationPropagate(bool canceled)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var store = await NotebookTestStore.CreateAsync(ct);
        var service = await store.OwnerAsync("owner", ct);
        var recipe = await CreateAsync(service, ct);
        Exception failure = canceled ? new OperationCanceledException(ct) : new InvalidOperationException("Unexpected failure");
        store.Photos.SaveFailure = failure;
        var upload = UploadAsync(service, recipe, ct);
        var actual = await Should.ThrowAsync<Exception>(() => upload);
        if (canceled)
        {
            actual.ShouldBeAssignableTo<OperationCanceledException>();
            upload.IsCanceled.ShouldBeTrue();
        }
        else
        {
            actual.ShouldBeSameAs(failure);
        }
        store.Photos.Items.ShouldBeEmpty();
        await using var db = await store.Factory.CreateDbContextAsync(ct);
        (await db.Set<PhotoCleanupEntity>().CountAsync(ct)).ShouldBe(canceled ? 1 : 0);
        (await service.GetRecipeAsync(recipe.Id, ct))!.Revision.ShouldBe(recipe.Revision);
    }

    private static async Task ProcessCleanupAsync(NotebookTestStore store, CancellationToken ct)
    {
        var processor = new PhotoCleanupProcessor(store.Factory, store.Photos, NullLogger<PhotoCleanupProcessor>.Instance, TimeProvider.System);
        await processor.ProcessAsync(ct);
    }

    private static async Task<NotebookChange> UploadAsync(NotebookService service, RecipeDocument recipe, CancellationToken ct)
    {
        await using var photo = new MemoryStream([137, 80, 78, 71, 13, 10, 26, 10]);
        return await service.UploadPhotoAsync(recipe.Id, recipe.Versions[0].Id, photo, "photo.png", "image/png", ct);
    }

    private static async Task<RecipeDocument> CreateAsync(NotebookService service, CancellationToken ct)
    {
        (await service.CreateWorkspaceAsync(new("Kitchen", "USD"), ct)).ShouldBeOfType<ChangeSaved>();
        var result = (await service.CreateRecipeAsync(new("Vanilla", "", []), ct)).ShouldBeOfType<ChangeSaved>();
        return (await service.GetRecipeAsync(result.Id, ct))!;
    }
}
