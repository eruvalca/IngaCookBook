using IngaCookBook.Features.Notebook.Data;
using IngaCookBook.SharedKernel.Notebook;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Shouldly;
using Xunit;

namespace IngaCookBook.IntegrationTests;

public sealed partial class NotebookPersistenceTests
{
    [Theory]
    [InlineData("batch", false)]
    [InlineData("standard", false)]
    [InlineData("variation", false)]
    [InlineData("promotion", false)]
    [InlineData("batch", true)]
    [InlineData("standard", true)]
    [InlineData("variation", true)]
    [InlineData("promotion", true)]
    public async Task PreservationWinningDuringDeletionIsRetainedAsAConflict(string preservation, bool variation)
    {
        var ct = TestContext.Current.CancellationToken;
        var race = new BeforeVersionWrite();
        await using var store = await NotebookTestStore.CreateAsync(ct, race);
        var service = await store.OwnerAsync("owner", ct);
        var recipe = await CreateAsync(service, ct);
        var versionId = recipe.Versions[0].Id;
        if (variation)
        {
            versionId = (await service.VaryAsync(recipe.Id, versionId, new(recipe.Revision), ct)).ShouldBeOfType<ChangeSaved>().Id;
            recipe = (await service.GetRecipeAsync(recipe.Id, ct))!;
        }
        // Let deletion finish reading and validating, then commit the other tab's
        // operation before its SQL executes. No timing sleeps or shared fixtures.
        race.CommitOtherTab = async () =>
        {
            var change = preservation switch
            {
                "batch" => await service.MakeBatchAsync(recipe.Id, versionId, new(recipe.Revision, DateTimeOffset.UtcNow, "Keep this batch"), ct),
                "standard" => await service.SetStandardAsync(recipe.Id, versionId, new(recipe.Revision), ct),
                "variation" => await service.VaryAsync(recipe.Id, versionId, new(recipe.Revision), ct),
                _ => await service.PromoteAsync(recipe.Id, versionId, new(recipe.Revision, "Keep this recipe"), ct),
            };
            change.ShouldBeOfType<ChangeSaved>();
        };
        (await service.DeleteDraftAsync(recipe.Id, versionId, new(recipe.Revision), ct)).ShouldBeOfType<ChangeRejected>().Status.ShouldBe(409);
        var retained = (await service.GetRecipeAsync(recipe.Id, ct))!;
        var version = retained.Versions.Single(v => v.Id == versionId);
        version.IsLocked.ShouldBeTrue();
        retained.Revision.ShouldNotBe(recipe.Revision);
        switch (preservation)
        {
            case "batch":
                version.Batches.Single().Notes.ShouldBe("Keep this batch");
                break;
            case "standard":
                retained.StandardVersionId.ShouldBe(versionId);
                break;
            case "variation":
                retained.Versions.Count(v => v.ParentId == versionId).ShouldBe(1);
                break;
            default:
                (await service.GetRecipesAsync(ct)).Single(r => r.Id != recipe.Id).OriginVersionId.ShouldBe(versionId);
                break;
        }
        await using var db = await store.Factory.CreateDbContextAsync(ct);
        (await db.Set<PhotoCleanupEntity>().CountAsync(ct)).ShouldBe(0);
    }

    [Theory]
    [InlineData("batch", false)]
    [InlineData("standard", false)]
    [InlineData("variation", false)]
    [InlineData("promotion", false)]
    [InlineData("batch", true)]
    [InlineData("standard", true)]
    [InlineData("variation", true)]
    [InlineData("promotion", true)]
    public async Task DeletionWinningDuringPreservationReturnsAConflictWithoutRecreatingTheDraft(string preservation, bool variation)
    {
        var ct = TestContext.Current.CancellationToken;
        var race = new BeforeVersionWrite { InterceptDeletion = false };
        await using var store = await NotebookTestStore.CreateAsync(ct, race);
        var service = await store.OwnerAsync("owner", ct);
        var recipe = await CreateAsync(service, ct);
        var versionId = recipe.Versions[0].Id;
        if (variation)
        {
            versionId = (await service.VaryAsync(recipe.Id, versionId, new(recipe.Revision), ct)).ShouldBeOfType<ChangeSaved>().Id;
            recipe = (await service.GetRecipeAsync(recipe.Id, ct))!;
        }
        race.CommitOtherTab = async () =>
        {
            (await service.DeleteDraftAsync(recipe.Id, versionId, new(recipe.Revision), ct)).ShouldBeOfType<ChangeSaved>();
        };
        var change = preservation switch
        {
            "batch" => await service.MakeBatchAsync(recipe.Id, versionId, new(recipe.Revision, DateTimeOffset.UtcNow, ""), ct),
            "standard" => await service.SetStandardAsync(recipe.Id, versionId, new(recipe.Revision), ct),
            "variation" => await service.VaryAsync(recipe.Id, versionId, new(recipe.Revision), ct),
            _ => await service.PromoteAsync(recipe.Id, versionId, new(recipe.Revision, "Must not be created"), ct),
        };
        change.ShouldBeOfType<ChangeRejected>().Status.ShouldBe(409);
        (await service.GetRecipesAsync(ct)).Count.ShouldBe(variation ? 1 : 0);
        await using var db = await store.Factory.CreateDbContextAsync(ct);
        (await db.Set<VersionEntity>().AnyAsync(v => v.Id == versionId, ct)).ShouldBeFalse();
        (await db.Set<BatchEntity>().CountAsync(ct)).ShouldBe(0);
        (await db.Set<PhotoCleanupEntity>().CountAsync(ct)).ShouldBe(1);
    }

    private sealed class BeforeVersionWrite : SaveChangesInterceptor
    {
        internal Func<Task>? CommitOtherTab { get; set; }
        internal bool InterceptDeletion { get; init; } = true;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (CommitOtherTab is { } commit && eventData.Context!.ChangeTracker.Entries<VersionEntity>()
                .Any(e => e.State == (InterceptDeletion ? EntityState.Deleted : EntityState.Modified)))
            {
                CommitOtherTab = null;
                await commit();
            }
            return result;
        }
    }
}
