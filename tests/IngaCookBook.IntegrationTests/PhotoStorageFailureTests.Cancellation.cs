using IngaCookBook.Features.Notebook.Data;
using IngaCookBook.SharedKernel.Notebook;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace IngaCookBook.IntegrationTests;

public sealed partial class PhotoStorageFailureTests
{
    [Fact]
    public async Task CanceledUploadQueuesOnlyUnconfirmedBlobUsingAnIndependentToken()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var store = await NotebookTestStore.CreateAsync(ct);
        var service = await store.OwnerAsync("owner", ct);
        var recipe = await CreateAsync(service, ct);
        (await UploadAsync(service, recipe, ct)).ShouldBeOfType<ChangeSaved>();
        recipe = (await service.GetRecipeAsync(recipe.Id, ct))!;
        var retained = store.Photos.Items.Keys.Single();
        using var request = CancellationTokenSource.CreateLinkedTokenSource(ct);
        store.Photos.AfterSave = async () =>
        {
            await request.CancelAsync();
            request.Token.ThrowIfCancellationRequested();
        };
        await Should.ThrowAsync<OperationCanceledException>(() => UploadAsync(service, recipe, request.Token));
        var unchanged = (await service.GetRecipeAsync(recipe.Id, ct))!;
        unchanged.Revision.ShouldBe(recipe.Revision);
        unchanged.Versions[0].Photos.Select(p => p.Id).ShouldBe(recipe.Versions[0].Photos.Select(p => p.Id));
        await using (var db = await store.Factory.CreateDbContextAsync(ct))
        {
            (await db.Set<PhotoCleanupEntity>().SingleAsync(ct)).Prefix.ShouldNotBe(retained, StringComparer.Ordinal);
        }
        await ProcessCleanupAsync(store, ct);
        store.Photos.Items.Keys.ShouldBe([retained]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationAfterBlobSuccessFinishesMetadataOrCompensation(bool concurrentEdit)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var store = await NotebookTestStore.CreateAsync(ct);
        var service = await store.OwnerAsync("owner", ct);
        var recipe = await CreateAsync(service, ct);
        using var request = CancellationTokenSource.CreateLinkedTokenSource(ct);
        store.Photos.AfterSave = async () =>
        {
            if (concurrentEdit)
            {
                (await service.SaveVersionAsync(recipe.Id, recipe.Versions[0].Id,
                    new(recipe.Revision, recipe.Versions[0].Content with { Notes = "Another tab" }, null), ct)).ShouldBeOfType<ChangeSaved>();
            }
            await request.CancelAsync();
            // Azure finished successfully, even though the caller has now gone away.
        };
        var result = await UploadAsync(service, recipe, request.Token);
        request.IsCancellationRequested.ShouldBeTrue();
        var saved = (await service.GetRecipeAsync(recipe.Id, ct))!;
        if (concurrentEdit)
        {
            result.ShouldBeOfType<ChangeRejected>().Status.ShouldBe(409);
            saved.Versions[0].Content.Notes.ShouldBe("Another tab");
            saved.Versions[0].Photos.ShouldBeEmpty();
            store.Photos.Items.ShouldBeEmpty();
        }
        else
        {
            var photo = result.ShouldBeOfType<ChangeSaved>();
            saved.Versions[0].Photos.Single().Id.ShouldBe(photo.Id);
            store.Photos.Items.Keys.Single().ShouldEndWith(photo.Id.ToString("N"));
        }
        await using var db = await store.Factory.CreateDbContextAsync(ct);
        (await db.Set<PhotoCleanupEntity>().CountAsync(ct)).ShouldBe(0);
    }

    [Fact]
    public async Task AlreadyCanceledOperationsDoNotWriteRecipesOrStartUploads()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var store = await NotebookTestStore.CreateAsync(ct);
        var service = await store.OwnerAsync("owner", ct);
        var recipe = await CreateAsync(service, ct);
        var canceled = new CancellationToken(canceled: true);
        await Should.ThrowAsync<OperationCanceledException>(() => service.SaveVersionAsync(recipe.Id, recipe.Versions[0].Id,
            new(recipe.Revision, recipe.Versions[0].Content with { Notes = "Canceled" }, null), canceled));
        await Should.ThrowAsync<OperationCanceledException>(() => UploadAsync(service, recipe, canceled));
        var saved = (await service.GetRecipeAsync(recipe.Id, ct))!;
        saved.Revision.ShouldBe(recipe.Revision);
        saved.Versions[0].Content.Notes.ShouldBe(recipe.Versions[0].Content.Notes);
        saved.Versions[0].Photos.ShouldBeEmpty();
        store.Photos.Items.ShouldBeEmpty();
        await using var db = await store.Factory.CreateDbContextAsync(ct);
        (await db.Set<PhotoCleanupEntity>().CountAsync(ct)).ShouldBe(0);
    }
}
