using IngaCookBook.SharedKernel.Notebook;
using Shouldly;
using Xunit;

namespace IngaCookBook.IntegrationTests;

public sealed partial class NotebookPersistenceTests
{
    [Fact]
    public async Task PhotoPresentationPersistsAndRejectsForeignPhotosAndStaleSettings()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var store = await NotebookTestStore.CreateAsync(ct);
        var owner = await store.OwnerAsync("owner", ct);
        var recipe = await CreateAsync(owner, ct);
        var photo = await AddDraftPhotoAsync(owner, recipe, ct);
        recipe = (await owner.GetRecipeAsync(recipe.Id, ct))!;
        var request = new RecipeSettingsRequest(recipe.Revision, recipe.Name, recipe.Description, recipe.Metrics, photo,
            new Dictionary<Guid, string> { [photo] = "Texture after 24 hours" });
        (await owner.SaveSettingsAsync(recipe.Id, request, ct)).ShouldBeOfType<ChangeSaved>();
        var saved = (await owner.GetRecipeAsync(recipe.Id, ct))!;
        saved.CoverPhotoId.ShouldBe(photo);
        saved.Versions[0].Photos[0].Caption.ShouldBe("Texture after 24 hours");
        (await owner.SaveSettingsAsync(recipe.Id, request with { CoverPhotoId = null }, ct)).ShouldBeOfType<ChangeRejected>().Status.ShouldBe(409);

        var other = await store.OwnerAsync("other", ct);
        (await other.SaveSettingsAsync(recipe.Id, request with { Revision = saved.Revision }, ct)).ShouldBeOfType<ChangeRejected>().Status.ShouldBe(404);
        var foreign = await CreateAsync(other, ct);
        var foreignPhoto = await AddDraftPhotoAsync(other, foreign, ct);
        (await owner.SaveSettingsAsync(recipe.Id, request with { Revision = saved.Revision, CoverPhotoId = foreignPhoto }, ct)).ShouldBeOfType<ChangeRejected>().Status.ShouldBe(400);
        (await owner.SaveSettingsAsync(recipe.Id, request with { Revision = saved.Revision, PhotoCaptions = new Dictionary<Guid, string> { [foreignPhoto] = "Wrong owner" } }, ct)).ShouldBeOfType<ChangeRejected>().Status.ShouldBe(400);
        (await owner.GetRecipeAsync(recipe.Id, ct))!.CoverPhotoId.ShouldBe(photo);
    }

    [Fact]
    public async Task DeletingTheDraftCoverFallsBackWithoutDeletingAnotherVersion()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var store = await NotebookTestStore.CreateAsync(ct);
        var owner = await store.OwnerAsync("owner", ct);
        var recipe = await CreateAsync(owner, ct);
        var parent = recipe.Versions[0].Id;
        var draft = (await owner.VaryAsync(recipe.Id, parent, new(recipe.Revision), ct)).ShouldBeOfType<ChangeSaved>().Id;
        await using var image = new MemoryStream([137, 80, 78, 71, 13, 10, 26, 10]);
        var photo = (await owner.UploadPhotoAsync(recipe.Id, draft, image, "scoop.png", "image/png", ct)).ShouldBeOfType<ChangeSaved>().Id;
        recipe = (await owner.GetRecipeAsync(recipe.Id, ct))!;
        (await owner.SaveSettingsAsync(recipe.Id, new(recipe.Revision, recipe.Name, recipe.Description, recipe.Metrics, photo), ct)).ShouldBeOfType<ChangeSaved>();
        recipe = (await owner.GetRecipeAsync(recipe.Id, ct))!;
        (await owner.DeleteDraftAsync(recipe.Id, draft, new(recipe.Revision), ct)).ShouldBeOfType<ChangeSaved>();
        recipe = (await owner.GetRecipeAsync(recipe.Id, ct))!;
        recipe.CoverPhotoId.ShouldBeNull();
        recipe.Versions.Single().Id.ShouldBe(parent);
    }
}
