using System.Diagnostics.CodeAnalysis;
using IngaCookBook.Features.Notebook.Data;
using IngaCookBook.Features.Notebook.Services;
using IngaCookBook.SharedKernel.Notebook;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shouldly;
using Xunit;

namespace IngaCookBook.IntegrationTests;

[SuppressMessage("Maintainability", "CA1515:Consider making public types internal", Justification = "xUnit requires public test classes for discovery.")]
public sealed partial class NotebookPersistenceTests
{
    [Fact]
    public async Task RecipeLifecyclePreservesBatchesCorrectionsStandardsAndIndependentPromotion()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var store = await NotebookTestStore.CreateAsync(ct);
        var service = await store.OwnerAsync("owner", ct);
        var recipe = await CreateAsync(service, ct);
        var version = recipe.Versions[0];
        var day = new DateTimeOffset(2026, 10, 2, 0, 0, 0, TimeSpan.Zero);
        var batch = (await service.MakeBatchAsync(recipe.Id, version.Id, new(recipe.Revision, day, "Aged overnight"), ct)).ShouldBeOfType<ChangeSaved>();
        recipe = (await service.GetRecipeAsync(recipe.Id, ct))!;
        var evaluation = await service.EvaluateAsync(recipe.Id, version.Id, batch.Id,
            new(recipe.Revision, day, "Smooth", "Try less sugar", [new(recipe.Metrics[0].Id, 8, "Good texture")]), ct);
        evaluation.ShouldBeOfType<ChangeSaved>();
        recipe = (await service.GetRecipeAsync(recipe.Id, ct))!;
        (await service.EvaluateAsync(recipe.Id, version.Id, batch.Id,
            new(recipe.Revision, day.AddDays(2), "Still smooth", "", [new(recipe.Metrics[0].Id, 9, "")]), ct)).ShouldBeOfType<ChangeSaved>();
        recipe = (await service.GetRecipeAsync(recipe.Id, ct))!;
        (await service.MakeBatchAsync(recipe.Id, version.Id, new(recipe.Revision, day.AddDays(3), "Repeat"), ct)).ShouldBeOfType<ChangeSaved>();
        recipe = (await service.GetRecipeAsync(recipe.Id, ct))!;
        recipe.Versions[0].Batches.Count.ShouldBe(2);
        recipe.Versions[0].Batches[0].Evaluations.Count.ShouldBe(2);
        recipe.Versions[0].Batches[0].Evaluations[0].Scores[0].Score.ShouldBe(8);
        var content = version.Content with { Notes = "Corrected typo" };
        (await service.SaveVersionAsync(recipe.Id, version.Id, new(recipe.Revision, content, null), ct)).ShouldBeOfType<ChangeRejected>();
        (await service.SaveVersionAsync(recipe.Id, version.Id, new(recipe.Revision, content, "Correct notebook transcription"), ct)).ShouldBeOfType<ChangeSaved>();
        recipe = (await service.GetRecipeAsync(recipe.Id, ct))!;
        recipe.Versions[0].Corrections.Single().PreviousContent.Notes.ShouldBe("");
        (await service.SetStandardAsync(recipe.Id, version.Id, new(recipe.Revision), ct)).ShouldBeOfType<ChangeSaved>();
        recipe = (await service.GetRecipeAsync(recipe.Id, ct))!;
        recipe.StandardVersionId.ShouldBe(version.Id);
        recipe.Standards.Single().VersionId.ShouldBe(version.Id);
        var promoted = (await service.PromoteAsync(recipe.Id, version.Id, new(recipe.Revision, "New recipe"), ct)).ShouldBeOfType<ChangeSaved>();
        var independent = (await service.GetRecipeAsync(promoted.Id, ct))!;
        independent.OriginVersionId.ShouldBe(version.Id);
        independent.Metrics[0].Id.ShouldNotBe(recipe.Metrics[0].Id);
        independent.Metrics[0].Name.ShouldBe(recipe.Metrics[0].Name);
        independent.Versions[0].Batches.ShouldBeEmpty();
        independent.Versions[0].Content.Ingredients[0].Quantity.ShouldBe(100m);
    }

    [Fact]
    public async Task VariationsRequireExplanationAndStaleEditsCannotOverwriteSavedWork()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var store = await NotebookTestStore.CreateAsync(ct);
        var service = await store.OwnerAsync("owner", ct);
        var recipe = await CreateAsync(service, ct);
        var original = recipe.Versions[0];
        var fork = (await service.VaryAsync(recipe.Id, original.Id, new(recipe.Revision), ct)).ShouldBeOfType<ChangeSaved>();
        recipe = (await service.GetRecipeAsync(recipe.Id, ct))!;
        var variation = recipe.Versions.Single(v => v.Id == fork.Id);
        variation.ParentId.ShouldBe(original.Id);
        recipe.Versions[0].IsLocked.ShouldBeTrue();
        var content = variation.Content with
        {
            Ingredients = [variation.Content.Ingredients[0] with { Quantity = 120 }],
            Steps = [variation.Content.Steps[0] with { Instruction = "Mix longer" }],
        };
        (await service.SaveVersionAsync(recipe.Id, variation.Id, new(recipe.Revision, content, null), ct)).ShouldBeOfType<ChangeRejected>()
            .Message.ShouldContain("Several things changed");
        (await service.SaveVersionAsync(recipe.Id, variation.Id, new(recipe.Revision, content with { RelatedChanges = "More cream needs more mixing" }, null), ct))
            .ShouldBeOfType<ChangeSaved>();
        (await service.SaveVersionAsync(recipe.Id, variation.Id, new(recipe.Revision, variation.Content, null), ct))
            .ShouldBeOfType<ChangeRejected>().Status.ShouldBe(409);
        var persisted = (await store.Service("owner").GetRecipeAsync(recipe.Id, ct))!;
        persisted.Versions.Single(v => v.Id == fork.Id).Content.Ingredients[0].Quantity.ShouldBe(120m);
        persisted.Versions[0].Content.Ingredients[0].Quantity.ShouldBe(100m);
    }

    [Fact]
    public async Task CriteriaChangesLeaveNewScoresBlankAndDeleteRemovedScores()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var store = await NotebookTestStore.CreateAsync(ct);
        var service = await store.OwnerAsync("owner", ct);
        var recipe = await CreateAsync(service, ct);
        var version = recipe.Versions[0];
        var day = DateTimeOffset.UtcNow;
        var batch = (await service.MakeBatchAsync(recipe.Id, version.Id, new(recipe.Revision, day, ""), ct)).ShouldBeOfType<ChangeSaved>();
        recipe = (await service.GetRecipeAsync(recipe.Id, ct))!;
        (await service.EvaluateAsync(recipe.Id, version.Id, batch.Id, new(recipe.Revision, day.AddHours(1), "Tasty", "",
            [new(recipe.Metrics[0].Id, 7, "Keep this observation")]), ct)).ShouldBeOfType<ChangeSaved>();
        recipe = (await service.GetRecipeAsync(recipe.Id, ct))!;
        var newMetric = new EvaluationMetric(Guid.NewGuid(), "Flavor");
        (await service.SaveSettingsAsync(recipe.Id, new(recipe.Revision, recipe.Name, recipe.Description, [.. recipe.Metrics, newMetric]), ct)).ShouldBeOfType<ChangeSaved>();
        recipe = (await service.GetRecipeAsync(recipe.Id, ct))!;
        newMetric = recipe.Metrics.Single(m => string.Equals(m.Name, "Flavor", StringComparison.Ordinal));
        recipe.Versions[0].Batches[0].Evaluations[0].Scores.Any(s => s.MetricId == newMetric.Id).ShouldBeFalse();
        (await service.SaveSettingsAsync(recipe.Id, new(recipe.Revision, recipe.Name, recipe.Description, [newMetric]), ct)).ShouldBeOfType<ChangeSaved>();
        recipe = (await service.GetRecipeAsync(recipe.Id, ct))!;
        recipe.Versions[0].Batches[0].Evaluations[0].Scores.ShouldBeEmpty();
        recipe.Versions[0].Batches[0].Evaluations[0].Notes.ShouldBe("Tasty");
    }

    [Fact]
    public async Task WorkspaceIsolationProtectsRecipesWritesAndPhotos()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var store = await NotebookTestStore.CreateAsync(ct);
        var owner = await store.OwnerAsync("first", ct);
        var recipe = await CreateAsync(owner, ct);
        var second = await store.OwnerAsync("second", ct);
        (await second.CreateWorkspaceAsync(new("Second", "USD"), ct)).ShouldBeOfType<ChangeSaved>();
        (await second.GetRecipesAsync(ct)).ShouldBeEmpty();
        (await second.GetRecipeAsync(recipe.Id, ct)).ShouldBeNull();
        var version = recipe.Versions[0];
        (await second.SaveVersionAsync(recipe.Id, version.Id, new(recipe.Revision, version.Content, null), ct)).ShouldBeOfType<ChangeRejected>();
        await using var image = new MemoryStream([137, 80, 78, 71, 13, 10, 26, 10]);
        (await second.UploadPhotoAsync(recipe.Id, version.Id, image, "test.png", "image/png", ct)).ShouldBeOfType<ChangeRejected>();
        store.Photos.Items.ShouldBeEmpty();
        (await owner.UploadPhotoAsync(recipe.Id, version.Id, image, "test.png", "image/png", ct)).ShouldBeOfType<ChangeSaved>();
        var persisted = (await owner.GetRecipeAsync(recipe.Id, ct))!;
        var photoId = persisted.Versions[0].Photos.Single().Id;
        (await second.OpenPhotoAsync(recipe.Id, version.Id, photoId, ct)).ShouldBeNull();
        var photo = (await owner.OpenPhotoAsync(recipe.Id, version.Id, photoId, ct))!.Value;
        await using var stream = photo.Content;
        stream.Length.ShouldBe(8);
        photo.ContentType.ShouldBe("image/png");
    }

    [Fact]
    public async Task LinkedRecipesKeepSavedContentWhenSourceIsCorrected()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var store = await NotebookTestStore.CreateAsync(ct);
        var service = await store.OwnerAsync("owner", ct);
        var sauce = await CreateAsync(service, ct);
        var source = sauce.Versions[0];
        (await service.SetStandardAsync(sauce.Id, source.Id, new(sauce.Revision), ct)).ShouldBeOfType<ChangeSaved>();
        var created = (await service.CreateRecipeAsync(new("Ice cream", "", []), ct)).ShouldBeOfType<ChangeSaved>();
        var iceCream = (await service.GetRecipeAsync(created.Id, ct))!;
        var linkedContent = new RecipeContent { Ingredients = [new Ingredient { Name = "Sauce", Quantity = 10, RecipeId = sauce.Id, VersionId = source.Id }] };
        (await service.SaveVersionAsync(iceCream.Id, iceCream.Versions[0].Id, new(iceCream.Revision, linkedContent, null), ct)).ShouldBeOfType<ChangeSaved>();
        sauce = (await service.GetRecipeAsync(sauce.Id, ct))!;
        (await service.SaveVersionAsync(sauce.Id, source.Id, new(sauce.Revision,
            source.Content with { Notes = "Correction after linking" }, "Transcription fix"), ct)).ShouldBeOfType<ChangeSaved>();
        iceCream = (await service.GetRecipeAsync(iceCream.Id, ct))!;
        iceCream.Versions[0].Content.Ingredients[0].LinkedContent.ShouldNotBeNull().Notes.ShouldBe("");
    }

    [Fact]
    public async Task ConcurrentEditsAllowOnlyOneWriterAndRetainItsContent()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var store = await NotebookTestStore.CreateAsync(ct);
        var service = await store.OwnerAsync("owner", ct);
        var recipe = await CreateAsync(service, ct);
        var version = recipe.Versions[0];
        var results = await Task.WhenAll(
            service.SaveVersionAsync(recipe.Id, version.Id, new(recipe.Revision, version.Content with { Notes = "First edit" }, null), ct),
            store.Service("owner").SaveVersionAsync(recipe.Id, version.Id, new(recipe.Revision, version.Content with { Notes = "Second edit" }, null), ct));
        results.OfType<ChangeSaved>().Count().ShouldBe(1);
        results.OfType<ChangeRejected>().Single().Status.ShouldBe(409);
        var saved = (await service.GetRecipeAsync(recipe.Id, ct))!;
        saved.Revision.ShouldNotBe(recipe.Revision);
        saved.Versions[0].Content.Notes.ShouldBe(results[0] is ChangeSaved ? "First edit" : "Second edit");
    }

    [Fact]
    public async Task InvalidPhotosNeverWriteAndConcurrentPhotoConflictRemovesOnlyItsUpload()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var store = await NotebookTestStore.CreateAsync(ct);
        var service = await store.OwnerAsync("owner", ct);
        var recipe = await CreateAsync(service, ct);
        var version = recipe.Versions[0];
        await using var invalid = new MemoryStream([60, 115, 118, 103, 62]);
        (await service.UploadPhotoAsync(recipe.Id, version.Id, invalid, "invalid.png", "image/png", ct)).ShouldBeOfType<ChangeRejected>().Status.ShouldBe(400);
        await using var large = new MemoryStream(new byte[NotebookService.MaximumPhotoBytes + 1]);
        (await service.UploadPhotoAsync(recipe.Id, version.Id, large, "large.png", "image/png", ct)).ShouldBeOfType<ChangeRejected>().Status.ShouldBe(413);
        store.Photos.Items.ShouldBeEmpty();
        (await service.GetRecipeAsync(recipe.Id, ct))!.Revision.ShouldBe(recipe.Revision);
        store.Photos.AfterSave = async () =>
        {
            (await service.SaveVersionAsync(recipe.Id, version.Id,
                new(recipe.Revision, version.Content with { Notes = "Another saved edit" }, null), ct)).ShouldBeOfType<ChangeSaved>();
        };
        await using var photo = new MemoryStream([137, 80, 78, 71, 13, 10, 26, 10]);
        (await service.UploadPhotoAsync(recipe.Id, version.Id, photo, "photo.png", "image/png", ct)).ShouldBeOfType<ChangeRejected>().Status.ShouldBe(409);
        store.Photos.Items.ShouldBeEmpty();
        var saved = (await service.GetRecipeAsync(recipe.Id, ct))!;
        saved.Versions[0].Photos.ShouldBeEmpty();
        saved.Versions[0].Content.Notes.ShouldBe("Another saved edit");
    }

    [Fact]
    public async Task DatabaseConstraintsRejectInvalidAmountsAndScores()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var store = await NotebookTestStore.CreateAsync(ct);
        var service = await store.OwnerAsync("owner", ct);
        var recipe = await CreateAsync(service, ct);
        var version = recipe.Versions[0];
        await using (var db = await store.Factory.CreateDbContextAsync(ct))
        {
            var ingredient = await db.Set<IngredientEntity>().SingleAsync(ct);
            ingredient.Quantity = -1;
            var error = await Should.ThrowAsync<DbUpdateException>(() => db.SaveChangesAsync(ct));
            error.InnerException.ShouldBeOfType<PostgresException>().ConstraintName.ShouldBe("CK_Ingredient_Amounts");
        }
        var day = DateTimeOffset.UtcNow;
        var batch = (await service.MakeBatchAsync(recipe.Id, version.Id, new(recipe.Revision, day, ""), ct)).ShouldBeOfType<ChangeSaved>();
        recipe = (await service.GetRecipeAsync(recipe.Id, ct))!;
        (await service.EvaluateAsync(recipe.Id, version.Id, batch.Id,
            new(recipe.Revision, day, "", "", [new(recipe.Metrics[0].Id, 8, "")]), ct)).ShouldBeOfType<ChangeSaved>();
        await using (var db = await store.Factory.CreateDbContextAsync(ct))
        {
            var score = await db.Set<ScoreEntity>().SingleAsync(ct);
            score.Score = 11;
            var error = await Should.ThrowAsync<DbUpdateException>(() => db.SaveChangesAsync(ct));
            error.InnerException.ShouldBeOfType<PostgresException>().ConstraintName.ShouldBe("CK_Score_Range");
        }
        recipe = (await service.GetRecipeAsync(recipe.Id, ct))!;
        recipe.Versions[0].Content.Ingredients[0].Quantity.ShouldBe(100m);
        recipe.Versions[0].Batches[0].Evaluations[0].Scores[0].Score.ShouldBe(8);
    }

    private static async Task<RecipeDocument> CreateAsync(NotebookService service, CancellationToken ct)
    {
        (await service.CreateWorkspaceAsync(new("Test kitchen", "USD"), ct)).ShouldBeOfType<ChangeSaved>();
        var result = (await service.CreateRecipeAsync(new("Vanilla", "Test recipe", ["Texture"]), ct)).ShouldBeOfType<ChangeSaved>();
        var recipe = (await service.GetRecipeAsync(result.Id, ct))!;
        var version = recipe.Versions[0];
        var content = new RecipeContent
        {
            Ingredients = [new Ingredient { Name = "Cream", Quantity = 100 }],
            Steps = [new(Guid.NewGuid(), "Mix and chill", "")],
        };
        (await service.SaveVersionAsync(recipe.Id, version.Id, new(recipe.Revision, content, null), ct)).ShouldBeOfType<ChangeSaved>();
        return (await service.GetRecipeAsync(recipe.Id, ct))!;
    }
}
