using System.Diagnostics.CodeAnalysis;
using Azure;
using IngaCookBook.Data;
using IngaCookBook.Features.Account.Services;
using IngaCookBook.Features.Notebook.Data;
using IngaCookBook.Features.Notebook.Services;
using IngaCookBook.SharedKernel.Notebook;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace IngaCookBook.IntegrationTests;

[SuppressMessage("Maintainability", "CA1515:Consider making public types internal", Justification = "xUnit requires public test classes for discovery.")]
public sealed class PhotoCleanupTests
{
    [Fact]
    public async Task AccountDeletionQueuesOnlyItsPhotosAndCleanupSurvivesFailureAndRetry()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var store = await NotebookTestStore.CreateAsync(ct);
        var owner = await store.OwnerAsync("owner", ct);
        var (recipe, prefix) = await AddPhotoAsync(owner, ct);
        var other = await store.OwnerAsync("other", ct);
        var (_, otherPrefix) = await AddPhotoAsync(other, ct);
        // Include a branch so cascading deletion also exercises the version-parent FK.
        (await owner.VaryAsync(recipe.Id, recipe.Versions[0].Id, new(recipe.Revision), ct)).ShouldBeOfType<ChangeSaved>();
        await using (var scope = store.CreateScope())
        {
            var user = (await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByIdAsync("owner"))!;
            (await scope.ServiceProvider.GetRequiredService<IAccountDeletionService>().DeleteAsync(user, ct)).Succeeded.ShouldBeTrue();
        }
        await using (var db = await store.Factory.CreateDbContextAsync(ct))
        {
            (await db.Users.AnyAsync(u => u.Id == "owner", ct)).ShouldBeFalse();
            (await db.Set<RecipeEntity>().AnyAsync(r => r.Id == recipe.Id, ct)).ShouldBeFalse();
            (await db.Set<PhotoCleanupEntity>().SingleAsync(ct)).Prefix.ShouldBe(prefix);
            (await db.Set<PhotoEntity>().CountAsync(ct)).ShouldBe(1);
        }
        store.Photos.Items.Count.ShouldBe(2);
        var clock = new CleanupClock(DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds()).AddMinutes(1));
        var processor = new PhotoCleanupProcessor(store.Factory, store.Photos, NullLogger<PhotoCleanupProcessor>.Instance, clock);
        store.Photos.CleanupFailure = new RequestFailedException(503, "Temporary storage failure");
        await processor.ProcessAsync(ct);
        await using (var db = await store.Factory.CreateDbContextAsync(ct))
        {
            var pending = await db.Set<PhotoCleanupEntity>().SingleAsync(ct);
            pending.NextAttemptAt.ShouldBe(clock.GetUtcNow().AddMinutes(1));
        }
        store.Photos.Items.Count.ShouldBe(2);
        store.Photos.CleanupFailure = null;
        await processor.ProcessAsync(ct); // Retry is not due yet.
        store.Photos.Items.Count.ShouldBe(2);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        await processor.ProcessAsync(ct);
        store.Photos.Items.Keys.Single().ShouldStartWith(otherPrefix);
        await using (var db = await store.Factory.CreateDbContextAsync(ct))
        {
            (await db.Set<PhotoCleanupEntity>().CountAsync(ct)).ShouldBe(0);
            (await db.Users.AnyAsync(u => u.Id == "other", ct)).ShouldBeTrue();
        }
        await processor.ProcessAsync(ct);
        store.Photos.Items.Count.ShouldBe(1);
    }

    [Fact]
    public async Task FailedAccountDeletionDoesNotQueueCleanupOrRemoveRecipes()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var store = await NotebookTestStore.CreateAsync(ct);
        var owner = await store.OwnerAsync("owner", ct);
        var (recipe, _) = await AddPhotoAsync(owner, ct);
        await using var scope = store.CreateScope();
        var user = (await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByIdAsync("owner"))!;
        await using (var concurrent = await store.Factory.CreateDbContextAsync(ct))
        {
            await concurrent.Users.Where(u => u.Id == user.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(u => u.ConcurrencyStamp, "new-stamp"), ct);
        }
        var result = await scope.ServiceProvider.GetRequiredService<IAccountDeletionService>().DeleteAsync(user, ct);
        result.Succeeded.ShouldBeFalse();
        result.Errors.Single().Code.ShouldBe("ConcurrencyFailure");
        await using var db = await store.Factory.CreateDbContextAsync(ct);
        (await db.Set<PhotoCleanupEntity>().CountAsync(ct)).ShouldBe(0);
        (await db.Set<RecipeEntity>().AnyAsync(r => r.Id == recipe.Id, ct)).ShouldBeTrue();
        (await db.Users.AnyAsync(u => u.Id == user.Id, ct)).ShouldBeTrue();
        store.Photos.Items.Count.ShouldBe(1);
    }

    [Fact]
    public async Task CleanupQueueFailureRollsBackSuccessfulIdentityDeletion()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var store = await NotebookTestStore.CreateAsync(ct);
        var owner = await store.OwnerAsync("owner", ct);
        var (recipe, _) = await AddPhotoAsync(owner, ct);
        await using (var db = await store.Factory.CreateDbContextAsync(ct))
        {
            // This disposable database deliberately rejects the outbox insert after Identity's delete.
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"PhotoCleanupJobs\" ADD CONSTRAINT \"RejectCleanup\" CHECK (false)", ct);
        }
        await using (var scope = store.CreateScope())
        {
            var user = (await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByIdAsync("owner"))!;
            await Should.ThrowAsync<DbUpdateException>(() => scope.ServiceProvider.GetRequiredService<IAccountDeletionService>().DeleteAsync(user, ct));
        }
        await using (var db = await store.Factory.CreateDbContextAsync(ct))
        {
            (await db.Users.AnyAsync(u => u.Id == "owner", ct)).ShouldBeTrue();
            (await db.Set<RecipeEntity>().AnyAsync(r => r.Id == recipe.Id, ct)).ShouldBeTrue();
            (await db.Set<PhotoCleanupEntity>().CountAsync(ct)).ShouldBe(0);
        }
        store.Photos.Items.Count.ShouldBe(1);
    }

    private static async Task<(RecipeDocument Recipe, string Prefix)> AddPhotoAsync(NotebookService service, CancellationToken ct)
    {
        (await service.CreateWorkspaceAsync(new("Kitchen", "USD"), ct)).ShouldBeOfType<ChangeSaved>();
        var created = (await service.CreateRecipeAsync(new("Recipe", "", []), ct)).ShouldBeOfType<ChangeSaved>();
        var recipe = (await service.GetRecipeAsync(created.Id, ct))!;
        await using var photo = new MemoryStream([137, 80, 78, 71, 13, 10, 26, 10]);
        (await service.UploadPhotoAsync(recipe.Id, recipe.Versions[0].Id, photo, "test.png", "image/png", ct)).ShouldBeOfType<ChangeSaved>();
        return ((await service.GetRecipeAsync(recipe.Id, ct))!, $"{(await service.GetWorkspaceAsync(ct))!.Id:N}/");
    }

    private sealed class CleanupClock(DateTimeOffset utcNow) : TimeProvider
    {
        internal DateTimeOffset UtcNow { get; set; } = utcNow;
        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}
