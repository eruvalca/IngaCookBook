using System.Text.Json;
using IngaCookBook.Features.Notebook.Services;
using IngaCookBook.SharedKernel.Notebook;
using Shouldly;
using Xunit;

namespace IngaCookBook.IntegrationTests;

public sealed partial class NotebookPersistenceTests
{
    [Fact]
    public async Task RecipesRequireAWorkspaceAndWorkspaceCreationValidatesCurrencyAndDuplicates()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var store = await NotebookTestStore.CreateAsync(ct);
        var service = await store.OwnerAsync("owner", ct);
        (await service.CreateRecipeAsync(new("Vanilla", "", []), ct)).ShouldBeOfType<ChangeRejected>().Status.ShouldBe(404);
        (await service.VaryAsync(Guid.NewGuid(), Guid.NewGuid(), new(Guid.NewGuid()), ct)).ShouldBeOfType<ChangeRejected>().Status.ShouldBe(404);
        (await service.CreateWorkspaceAsync(new("Kitchen", "XYZ"), ct)).ShouldBeOfType<ChangeRejected>().Status.ShouldBe(400);
        (await service.GetWorkspaceAsync(ct)).ShouldBeNull();
        (await service.CreateWorkspaceAsync(new("  Kitchen  ", "CAD"), ct)).ShouldBeOfType<ChangeSaved>();
        var workspace = (await service.GetWorkspaceAsync(ct)).ShouldNotBeNull();
        workspace.Name.ShouldBe("Kitchen");
        workspace.Currency.ShouldBe("CAD");
        (await service.CreateWorkspaceAsync(new("Other kitchen", "USD"), ct)).ShouldBeOfType<ChangeRejected>().Status.ShouldBe(409);
        (await service.GetWorkspaceAsync(ct)).ShouldBe(workspace);
    }

    [Fact]
    public async Task MissingVersionsRejectRecipeActionsWithoutChangingTheRecipe()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var store = await NotebookTestStore.CreateAsync(ct);
        var service = await store.OwnerAsync("owner", ct);
        var recipe = await CreateAsync(service, ct);
        var missing = Guid.NewGuid();
        var revision = new RevisionRequest(recipe.Revision);
        (await service.SaveVersionAsync(recipe.Id, missing, new(recipe.Revision, recipe.Versions[0].Content, null), ct)).ShouldBeOfType<ChangeRejected>().Status.ShouldBe(404);
        (await service.VaryAsync(recipe.Id, missing, revision, ct)).ShouldBeOfType<ChangeRejected>().Status.ShouldBe(404);
        (await service.SetStandardAsync(recipe.Id, missing, revision, ct)).ShouldBeOfType<ChangeRejected>().Status.ShouldBe(404);
        (await service.MakeBatchAsync(recipe.Id, missing, new(recipe.Revision, DateTimeOffset.UtcNow, ""), ct)).ShouldBeOfType<ChangeRejected>().Status.ShouldBe(404);
        (await service.EvaluateAsync(recipe.Id, missing, missing, new(recipe.Revision, DateTimeOffset.UtcNow, "Tasty", "", []), ct)).ShouldBeOfType<ChangeRejected>().Status.ShouldBe(404);
        (await service.PromoteAsync(recipe.Id, missing, new(recipe.Revision, "New recipe"), ct)).ShouldBeOfType<ChangeRejected>().Status.ShouldBe(404);
        await AssertUnchangedAsync(service, recipe, ct);
        (await service.GetRecipesAsync(ct)).Count.ShouldBe(1);
    }

    [Fact]
    public async Task RejectedFormulationAndPromotionInputsCannotMutatePersistedContent()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var store = await NotebookTestStore.CreateAsync(ct);
        var service = await store.OwnerAsync("owner", ct);
        var recipe = await CreateAsync(service, ct);
        var version = recipe.Versions[0];
        (await service.SaveVersionAsync(recipe.Id, version.Id, new(recipe.Revision, version.Content with { Label = "" }, null), ct))
            .ShouldBeOfType<ChangeRejected>().Message.ShouldContain("short label");
        (await service.SaveVersionAsync(recipe.Id, version.Id, new(recipe.Revision, version.Content with { TargetMetricId = Guid.NewGuid() }, null), ct))
            .ShouldBeOfType<ChangeRejected>().Message.ShouldContain("current evaluation metrics");
        (await service.SaveSettingsAsync(recipe.Id, new(recipe.Revision, "", "", recipe.Metrics), ct)).ShouldBeOfType<ChangeRejected>().Status.ShouldBe(400);
        (await service.MakeBatchAsync(recipe.Id, version.Id, new(recipe.Revision, default, ""), ct)).ShouldBeOfType<ChangeRejected>().Message.ShouldContain("when the batch was made");
        (await service.PromoteAsync(recipe.Id, version.Id, new(recipe.Revision, " "), ct)).ShouldBeOfType<ChangeRejected>().Status.ShouldBe(400);
        (await service.PromoteAsync(recipe.Id, version.Id, new(Guid.NewGuid(), "New recipe"), ct)).ShouldBeOfType<ChangeRejected>().Status.ShouldBe(409);
        await AssertUnchangedAsync(service, recipe, ct);
        (await service.GetRecipesAsync(ct)).Count.ShouldBe(1);
    }

    [Fact]
    public async Task IncompleteDraftCannotBecomeABatchOrStandard()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var store = await NotebookTestStore.CreateAsync(ct);
        var service = await store.OwnerAsync("owner", ct);
        (await service.CreateWorkspaceAsync(new("Kitchen", "USD"), ct)).ShouldBeOfType<ChangeSaved>();
        var id = (await service.CreateRecipeAsync(new("Unfinished", "", []), ct)).ShouldBeOfType<ChangeSaved>().Id;
        var recipe = (await service.GetRecipeAsync(id, ct)).ShouldNotBeNull();
        var version = recipe.Versions[0];
        (await service.MakeBatchAsync(id, version.Id, new(recipe.Revision, DateTimeOffset.UtcNow, ""), ct))
            .ShouldBeOfType<ChangeRejected>().Message.ShouldContain("add ingredients");
        (await service.SetStandardAsync(id, version.Id, new(recipe.Revision), ct))
            .ShouldBeOfType<ChangeRejected>().Message.ShouldContain("add ingredients");
        await AssertUnchangedAsync(service, recipe, ct);
    }

    [Fact]
    public async Task InvalidTastingsDoNotConsumeTheRevisionOrAppendAnEvaluation()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var store = await NotebookTestStore.CreateAsync(ct);
        var service = await store.OwnerAsync("owner", ct);
        var recipe = await CreateAsync(service, ct);
        var version = recipe.Versions[0];
        var madeAt = new DateTimeOffset(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);
        var batch = (await service.MakeBatchAsync(recipe.Id, version.Id, new(recipe.Revision, madeAt, ""), ct)).ShouldBeOfType<ChangeSaved>();
        recipe = (await service.GetRecipeAsync(recipe.Id, ct)).ShouldNotBeNull();
        (await service.EvaluateAsync(recipe.Id, version.Id, batch.Id,
            new(recipe.Revision, madeAt, "", "", [new(recipe.Metrics[0].Id, 11, "")]), ct))
            .ShouldBeOfType<ChangeRejected>().Message.ShouldContain("1 to 10");
        (await service.EvaluateAsync(recipe.Id, version.Id, batch.Id,
            new(recipe.Revision, madeAt.AddMinutes(-1), "Smooth", "", []), ct))
            .ShouldBeOfType<ChangeRejected>().Message.ShouldContain("earlier than the batch");
        await AssertUnchangedAsync(service, recipe, ct);
        (await service.EvaluateAsync(recipe.Id, version.Id, batch.Id, new(recipe.Revision, madeAt, "Smooth", "", []), ct)).ShouldBeOfType<ChangeSaved>();
        var saved = (await service.GetRecipeAsync(recipe.Id, ct)).ShouldNotBeNull();
        saved.Versions[0].Batches[0].Evaluations.Single().Notes.ShouldBe("Smooth");
    }

    [Fact]
    public async Task LinkedIngredientsRejectUnpreservedSourcesAndKeepServerSnapshotsAcrossEdits()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var store = await NotebookTestStore.CreateAsync(ct);
        var service = await store.OwnerAsync("owner", ct);
        var source = await CreateAsync(service, ct);
        var targetId = (await service.CreateRecipeAsync(new("Caramel ice cream", "", []), ct)).ShouldBeOfType<ChangeSaved>().Id;
        var target = (await service.GetRecipeAsync(targetId, ct)).ShouldNotBeNull();
        var ingredient = new Ingredient { Name = "Caramel", Quantity = 20, RecipeId = source.Id, VersionId = source.Versions[0].Id };
        var content = target.Versions[0].Content with { Ingredients = [ingredient] };
        (await service.SaveVersionAsync(target.Id, target.Versions[0].Id, new(target.Revision, content, null), ct))
            .ShouldBeOfType<ChangeRejected>().Message.ShouldContain("preserved versions");
        await AssertUnchangedAsync(service, target, ct);
        (await service.SetStandardAsync(source.Id, source.Versions[0].Id, new(source.Revision), ct)).ShouldBeOfType<ChangeSaved>();
        (await service.SaveVersionAsync(target.Id, target.Versions[0].Id, new(target.Revision, content, null), ct)).ShouldBeOfType<ChangeSaved>();
        target = (await service.GetRecipeAsync(target.Id, ct)).ShouldNotBeNull();
        var snapshot = target.Versions[0].Content.Ingredients[0].LinkedContent.ShouldNotBeNull();
        source = (await service.GetRecipeAsync(source.Id, ct)).ShouldNotBeNull();
        (await service.SaveVersionAsync(source.Id, source.Versions[0].Id,
            new(source.Revision, source.Versions[0].Content with { Notes = "Corrected after linking" }, "Correct note"), ct)).ShouldBeOfType<ChangeSaved>();
        content = content with { Ingredients = [ingredient with { Quantity = 30, LinkedContent = new() { Notes = "Untrusted client snapshot" } }] };
        (await service.SaveVersionAsync(target.Id, target.Versions[0].Id, new(target.Revision, content, null), ct)).ShouldBeOfType<ChangeSaved>();
        target = (await service.GetRecipeAsync(target.Id, ct)).ShouldNotBeNull();
        var savedIngredient = target.Versions[0].Content.Ingredients[0];
        savedIngredient.Quantity.ShouldBe(30);
        JsonSerializer.Serialize(savedIngredient.LinkedContent).ShouldBe(JsonSerializer.Serialize(snapshot));
        content = content with { Ingredients = [savedIngredient with { RecipeId = null, VersionId = null }] };
        (await service.SaveVersionAsync(target.Id, target.Versions[0].Id, new(target.Revision, content, null), ct)).ShouldBeOfType<ChangeSaved>();
        (await service.GetRecipeAsync(target.Id, ct)).ShouldNotBeNull().Versions[0].Content.Ingredients[0].LinkedContent.ShouldBeNull();
    }

    private static async Task AssertUnchangedAsync(NotebookService service, RecipeDocument recipe, CancellationToken ct)
    {
        var persisted = (await service.GetRecipeAsync(recipe.Id, ct)).ShouldNotBeNull();
        JsonSerializer.Serialize(persisted).ShouldBe(JsonSerializer.Serialize(recipe));
    }
}
