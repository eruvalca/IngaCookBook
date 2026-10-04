using IngaCookBook.Features.Notebook.Data;
using IngaCookBook.Features.Notebook.Services;
using IngaCookBook.SharedKernel.Notebook;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace IngaCookBook.IntegrationTests;

public sealed partial class NotebookPersistenceTests
{
    [Fact]
    public async Task DeletingOnlyDraftRemovesRecipeAndQueuesOnlyItsPhotos()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var store = await NotebookTestStore.CreateAsync(ct);
        var owner = await store.OwnerAsync("owner", ct);
        var recipe = await CreateAsync(owner, ct);
        var photo = await AddDraftPhotoAsync(owner, recipe, ct);
        recipe = (await owner.GetRecipeAsync(recipe.Id, ct))!;
        var other = await store.OwnerAsync("other", ct);
        var retained = await CreateAsync(other, ct);
        await AddDraftPhotoAsync(other, retained, ct);
        var workspace = (await owner.GetWorkspaceAsync(ct))!;

        (await owner.DeleteDraftAsync(recipe.Id, recipe.Versions[0].Id, new(recipe.Revision), ct))
            .ShouldBeOfType<ChangeSaved>().Id.ShouldBe(recipe.Versions[0].Id);

        (await owner.GetRecipeAsync(recipe.Id, ct)).ShouldBeNull();
        (await owner.GetRecipesAsync(ct)).ShouldBeEmpty();
        (await owner.GetWorkspaceAsync(ct)).ShouldNotBeNull();
        (await owner.OpenPhotoAsync(recipe.Id, recipe.Versions[0].Id, photo, ct)).ShouldBeNull();
        await using var db = await store.Factory.CreateDbContextAsync(ct);
        (await db.Set<MetricEntity>().CountAsync(ct)).ShouldBe(retained.Metrics.Count);
        (await db.Set<VersionEntity>().CountAsync(ct)).ShouldBe(1);
        (await db.Set<PhotoEntity>().CountAsync(ct)).ShouldBe(1);
        (await db.Set<IngredientEntity>().CountAsync(ct)).ShouldBe(1);
        (await db.Set<StepEntity>().CountAsync(ct)).ShouldBe(1);
        (await db.Set<PhotoCleanupEntity>().SingleAsync(ct)).Prefix
            .ShouldBe($"{workspace.Id:N}/{recipe.Id:N}/{recipe.Versions[0].Id:N}/");
        store.Photos.Items.Count.ShouldBe(2);
        await new PhotoCleanupProcessor(store.Factory, store.Photos, NullLogger<PhotoCleanupProcessor>.Instance, TimeProvider.System).ProcessAsync(ct);
        store.Photos.Items.Keys.Single().ShouldContain($"/{retained.Id:N}/");
        (await db.Set<PhotoCleanupEntity>().CountAsync(ct)).ShouldBe(0);
    }

    [Fact]
    public async Task DeletingVariationRetainsBaselineAndSiblingAndRejectsStaleWrites()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var store = await NotebookTestStore.CreateAsync(ct);
        var service = await store.OwnerAsync("owner", ct);
        var recipe = await CreateAsync(service, ct);
        var baseline = recipe.Versions[0];
        var first = (await service.VaryAsync(recipe.Id, baseline.Id, new(recipe.Revision), ct)).ShouldBeOfType<ChangeSaved>();
        recipe = (await service.GetRecipeAsync(recipe.Id, ct))!;
        var second = (await service.VaryAsync(recipe.Id, baseline.Id, new(recipe.Revision), ct)).ShouldBeOfType<ChangeSaved>();
        recipe = (await service.GetRecipeAsync(recipe.Id, ct))!;
        (await service.DeleteDraftAsync(recipe.Id, first.Id, new(recipe.Revision), ct)).ShouldBeOfType<ChangeSaved>();
        var saved = (await service.GetRecipeAsync(recipe.Id, ct))!;
        saved.Revision.ShouldNotBe(recipe.Revision);
        saved.Versions.Select(v => v.Id).ShouldBe([baseline.Id, second.Id]);
        saved.Versions[0].IsLocked.ShouldBeTrue();
        saved.Versions[0].Content.Ingredients[0].Quantity.ShouldBe(100m);
        saved.Versions[1].ParentId.ShouldBe(baseline.Id);
        (await service.SaveVersionAsync(recipe.Id, second.Id, new(recipe.Revision, baseline.Content, null), ct))
            .ShouldBeOfType<ChangeRejected>().Status.ShouldBe(409);
        (await service.DeleteDraftAsync(recipe.Id, first.Id, new(saved.Revision), ct)).ShouldBeOfType<ChangeRejected>().Status.ShouldBe(404);
    }

    [Theory]
    [InlineData("batch")]
    [InlineData("standard")]
    [InlineData("variation")]
    [InlineData("promotion")]
    public async Task DeletionProtectsPreservedAndReferencedVersions(string preservation)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var store = await NotebookTestStore.CreateAsync(ct);
        var service = await store.OwnerAsync("owner", ct);
        var recipe = await CreateAsync(service, ct);
        var version = recipe.Versions[0];
        var change = preservation switch
        {
            "batch" => await service.MakeBatchAsync(recipe.Id, version.Id, new(recipe.Revision, DateTimeOffset.UtcNow, ""), ct),
            "standard" => await service.SetStandardAsync(recipe.Id, version.Id, new(recipe.Revision), ct),
            "variation" => await service.VaryAsync(recipe.Id, version.Id, new(recipe.Revision), ct),
            _ => await service.PromoteAsync(recipe.Id, version.Id, new(recipe.Revision, "Independent recipe"), ct),
        };
        change.ShouldBeOfType<ChangeSaved>();
        var saved = (await service.GetRecipeAsync(recipe.Id, ct))!;
        saved.Versions[0].IsLocked.ShouldBeTrue();
        saved.Revision.ShouldNotBe(recipe.Revision);
        (await service.DeleteDraftAsync(recipe.Id, version.Id, new(saved.Revision), ct)).ShouldBeOfType<ChangeRejected>().Status.ShouldBe(400);
        (await service.GetRecipeAsync(recipe.Id, ct))!.Revision.ShouldBe(saved.Revision);
        await using var db = await store.Factory.CreateDbContextAsync(ct);
        (await db.Set<PhotoCleanupEntity>().CountAsync(ct)).ShouldBe(0);
    }

    [Fact]
    public async Task DeletionRejectsStaleMissingAndOtherWorkspaceRequestsWithoutCleanup()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var store = await NotebookTestStore.CreateAsync(ct);
        var owner = await store.OwnerAsync("owner", ct);
        var recipe = await CreateAsync(owner, ct);
        var version = recipe.Versions[0];
        var other = await store.OwnerAsync("other", ct);
        await CreateAsync(other, ct);
        (await other.DeleteDraftAsync(recipe.Id, version.Id, new(recipe.Revision), ct)).ShouldBeOfType<ChangeRejected>().Status.ShouldBe(404);
        (await store.Service("missing").DeleteDraftAsync(recipe.Id, version.Id, new(recipe.Revision), ct)).ShouldBeOfType<ChangeRejected>().Status.ShouldBe(404);
        (await owner.DeleteDraftAsync(Guid.NewGuid(), version.Id, new(recipe.Revision), ct)).ShouldBeOfType<ChangeRejected>().Status.ShouldBe(404);
        (await owner.DeleteDraftAsync(recipe.Id, Guid.NewGuid(), new(recipe.Revision), ct)).ShouldBeOfType<ChangeRejected>().Status.ShouldBe(404);
        (await owner.SaveVersionAsync(recipe.Id, version.Id, new(recipe.Revision, version.Content with { Notes = "Keep this edit" }, null), ct)).ShouldBeOfType<ChangeSaved>();
        (await owner.DeleteDraftAsync(recipe.Id, version.Id, new(recipe.Revision), ct)).ShouldBeOfType<ChangeRejected>().Status.ShouldBe(409);
        (await owner.GetRecipeAsync(recipe.Id, ct))!.Versions[0].Content.Notes.ShouldBe("Keep this edit");
        await using var db = await store.Factory.CreateDbContextAsync(ct);
        (await db.Set<PhotoCleanupEntity>().CountAsync(ct)).ShouldBe(0);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task CleanupQueueFailureRollsBackDraftDeletion(bool variation, bool foreignKey)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var store = await NotebookTestStore.CreateAsync(ct);
        var service = await store.OwnerAsync("owner", ct);
        var recipe = await CreateAsync(service, ct);
        var id = recipe.Versions[0].Id;
        if (variation)
        {
            id = (await service.VaryAsync(recipe.Id, id, new(recipe.Revision), ct)).ShouldBeOfType<ChangeSaved>().Id;
            recipe = (await service.GetRecipeAsync(recipe.Id, ct))!;
        }
        await using var db = await store.Factory.CreateDbContextAsync(ct);
        var constraint = foreignKey
            ? "ALTER TABLE \"PhotoCleanupJobs\" ADD CONSTRAINT \"RejectDraftCleanup\" FOREIGN KEY (\"Id\") REFERENCES \"Recipes\" (\"Id\")"
            : "ALTER TABLE \"PhotoCleanupJobs\" ADD CONSTRAINT \"RejectDraftCleanup\" CHECK (false)";
        await db.Database.ExecuteSqlRawAsync(constraint, ct);
        await Should.ThrowAsync<DbUpdateException>(() => service.DeleteDraftAsync(recipe.Id, id, new(recipe.Revision), ct));
        var retained = (await service.GetRecipeAsync(recipe.Id, ct))!;
        retained.Revision.ShouldBe(recipe.Revision);
        retained.Versions.Select(v => v.Id).ShouldContain(id);
        (await db.Set<PhotoCleanupEntity>().CountAsync(ct)).ShouldBe(0);
    }

    [Fact]
    public async Task UploadRacingDraftDeletionCannotRecreateTheDraftOrLeaveItsBlob()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var store = await NotebookTestStore.CreateAsync(ct);
        var service = await store.OwnerAsync("owner", ct);
        var recipe = await CreateAsync(service, ct);
        store.Photos.AfterSave = async () =>
            (await service.DeleteDraftAsync(recipe.Id, recipe.Versions[0].Id, new(recipe.Revision), ct)).ShouldBeOfType<ChangeSaved>();
        await using var image = new MemoryStream([137, 80, 78, 71, 13, 10, 26, 10]);
        (await service.UploadPhotoAsync(recipe.Id, recipe.Versions[0].Id, image, "draft.png", "image/png", ct))
            .ShouldBeOfType<ChangeRejected>().Status.ShouldBe(404);
        (await service.GetRecipeAsync(recipe.Id, ct)).ShouldBeNull();
        store.Photos.Items.ShouldBeEmpty();
    }

    private static async Task<Guid> AddDraftPhotoAsync(NotebookService service, RecipeDocument recipe, CancellationToken ct)
    {
        await using var image = new MemoryStream([137, 80, 78, 71, 13, 10, 26, 10]);
        return (await service.UploadPhotoAsync(recipe.Id, recipe.Versions[0].Id, image, "draft.png", "image/png", ct)).ShouldBeOfType<ChangeSaved>().Id;
    }
}
