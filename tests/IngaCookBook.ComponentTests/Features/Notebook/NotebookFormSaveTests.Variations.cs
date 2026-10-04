using Bunit;
using IngaCookBook.SharedKernel.Notebook;
using IngaCookBook.UI.Features.Notebook.Components;
using IngaCookBook.UI.Features.Notebook.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;
using NSubstitute;
using Shouldly;
using Xunit;

namespace IngaCookBook.ComponentTests.Features.Notebook;

public sealed partial class NotebookFormSaveTests
{
    [Fact]
    public async Task PreparationReorderingWaitsUntilUnsavedChangesProtectionIsReady()
    {
        await using var context = new BunitContext();
        var (service, recipe) = Configure(context);
        var version = recipe.Versions[0] with { Content = new() { Steps = [new(Guid.NewGuid(), "Mix", ""), new(Guid.NewGuid(), "Chill", "")] } };
        recipe = recipe with { Versions = [version] };
        service.GetRecipeAsync(recipe.Id, Arg.Any<CancellationToken>()).Returns(recipe);
        var module = context.JSInterop.SetupModule("./_content/IngaCookBook.UI/Features/Notebook/Pages/VersionEditor.razor.js");
        var initialization = module.SetupVoid("updateGuard", _ => true);
        var component = context.Render<VersionEditor>(p => p.Add(c => c.RecipeId, recipe.Id).Add(c => c.VersionId, version.Id));
        try
        {
            var buttons = component.FindAll("fluent-button").Where(b => b.TextContent.Contains("Move ", StringComparison.Ordinal)).ToArray();
            buttons.Length.ShouldBe(4);
            buttons.ShouldAllBe(b => b.HasAttribute("disabled"));
        }
        finally { initialization.SetVoidResult(); }
        await component.WaitForAssertionAsync(() => component.FindAll("fluent-button")
            .First(b => string.Equals(b.TextContent.Trim(), "Move down", StringComparison.Ordinal)).HasAttribute("disabled").ShouldBeFalse());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancelingATastingCorrectionHonorsTheDiscardChoice(bool discard)
    {
        await using var context = new BunitContext();
        var (service, recipe) = Configure(context);
        var module = context.JSInterop.SetupModule("./_content/IngaCookBook.UI/Features/Notebook/Pages/VersionEditor.razor.js");
        module.Setup<bool>("confirmDiscard").SetResult(discard);
        var version = recipe.Versions[0];
        var batch = version.Batches[0];
        var tasting = new BatchEvaluation(Guid.NewGuid(), batch.MadeAt, "Original observation", "", [new(recipe.Metrics[0].Id, 8, "")]);
        recipe = recipe with { Versions = [version with { Batches = [batch with { Evaluations = [tasting] }] }] };
        service.GetRecipeAsync(recipe.Id, Arg.Any<CancellationToken>()).Returns(recipe);
        var component = context.Render<BatchJournal>(p => p.Add(c => c.RecipeId, recipe.Id).Add(c => c.VersionId, version.Id));
        await DeleteButton(component, "Correct tasting").ClickAsync();
        var notes = component.FindComponents<FluentTextArea>().Single(c => string.Equals(c.Instance.Label, "Overall observations", StringComparison.Ordinal));
        await notes.Find("fluent-textarea").ChangeAsync(new ChangeEventArgs { Value = "Corrected observation" });

        await DeleteButton(component, "Cancel correction").ClickAsync();

        module.Invocations["confirmDiscard"].Count.ShouldBe(1);
        notes.Instance.Value.ShouldBe(discard ? "" : "Corrected observation");
        component.FindComponents<FluentTextInput>().Single(c => string.Equals(c.Instance.Label, "Texture", StringComparison.Ordinal)).Instance.Value.ShouldBe(discard ? "" : "8");
        component.FindAll("fluent-button").Count(b => string.Equals(b.TextContent.Trim(), "Save tasting correction", StringComparison.Ordinal)).ShouldBe(discard ? 0 : 1);
        await service.DidNotReceive().CorrectEvaluationAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<EvaluationCorrectionRequest>(), Arg.Any<CancellationToken>());
        await service.DidNotReceive().EvaluateAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<EvaluationRequest>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TastingReceiptCreatesAVariationOnlyAfterDiscardConfirmation(bool discard)
    {
        await using var context = new BunitContext();
        var (service, recipe) = Configure(context);
        var module = context.JSInterop.SetupModule("./_content/IngaCookBook.UI/Features/Notebook/Pages/VersionEditor.razor.js");
        module.Setup<bool>("confirmDiscard").SetResult(discard);
        var tasting = Guid.NewGuid();
        var variation = Guid.NewGuid();
        service.EvaluateAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<EvaluationRequest>(), Arg.Any<CancellationToken>()).Returns(new ChangeSaved(tasting));
        service.VaryAsync(recipe.Id, recipe.Versions[0].Id, new(recipe.Revision), Arg.Any<CancellationToken>()).Returns(new ChangeSaved(variation));
        var component = context.Render<BatchJournal>(p => p.Add(c => c.RecipeId, recipe.Id).Add(c => c.VersionId, recipe.Versions[0].Id));
        await component.FindComponents<FluentTextArea>().Single(c => string.Equals(c.Instance.Label, "One thing to try next", StringComparison.Ordinal))
            .Find("fluent-textarea").ChangeAsync(new ChangeEventArgs { Value = "Try longer aging" });
        await DeleteButton(component, "Save evaluation").ClickAsync();
        var before = context.Services.GetRequiredService<NavigationManager>().Uri;

        await DeleteButton(component, "Try this as a variation").ClickAsync();

        module.Invocations["confirmDiscard"].Count.ShouldBe(1);
        if (discard)
        {
            await service.Received(1).VaryAsync(recipe.Id, recipe.Versions[0].Id, new(recipe.Revision), Arg.Any<CancellationToken>());
            context.Services.GetRequiredService<NavigationManager>().Uri.ShouldEndWith($"/recipes/{recipe.Id}/versions/{variation}/edit?idea={tasting}");
            module.Invocations["updateGuard"][^1].Arguments[1].ShouldBe(false);
        }
        else
        {
            await service.DidNotReceive().VaryAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<RevisionRequest>(), Arg.Any<CancellationToken>());
            context.Services.GetRequiredService<NavigationManager>().Uri.ShouldBe(before);
            component.Find(".completion-receipt").TextContent.ShouldContain("Try longer aging");
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PromotionUsesEnteredNameAndNavigatesOnlyAfterSaving(bool succeeds)
    {
        await using var context = new BunitContext();
        var (service, recipe) = Configure(context);
        var promoted = Guid.NewGuid();
        service.PromoteAsync(recipe.Id, recipe.Versions[0].Id, Arg.Any<PromotionRequest>(), Arg.Any<CancellationToken>())
            .Returns(succeeds ? new ChangeSaved(promoted) : new ChangeRejected("Reload first", 409));
        var component = context.Render<VersionActions>(p => p.Add(c => c.RecipeId, recipe.Id).Add(c => c.VersionId, recipe.Versions[0].Id)
            .Add(c => c.Revision, recipe.Revision).Add(c => c.OnlyVersion, true));
        var name = component.FindComponents<FluentTextInput>().Single(c => string.Equals(c.Instance.Label, "New recipe name", StringComparison.Ordinal));
        await name.Find("fluent-text-input").ChangeAsync(new ChangeEventArgs { Value = "Vanilla bean" });
        var before = context.Services.GetRequiredService<NavigationManager>().Uri;

        await DeleteButton(component, "Create separate recipe").ClickAsync();

        await service.Received(1).PromoteAsync(recipe.Id, recipe.Versions[0].Id, new(recipe.Revision, "Vanilla bean"), Arg.Any<CancellationToken>());
        if (succeeds) { context.Services.GetRequiredService<NavigationManager>().Uri.ShouldEndWith($"/recipes/{promoted}"); }
        else
        {
            context.Services.GetRequiredService<NavigationManager>().Uri.ShouldBe(before);
            component.Find("[role=alert]").TextContent.ShouldBe("Reload first");
            name.Instance.Value.ShouldBe("Vanilla bean");
        }
    }

    [Fact]
    public async Task ReorderingPreparationRetainsStepIdentitiesAndSavesTheNewOrder()
    {
        await using var context = new BunitContext();
        var (service, recipe) = Configure(context);
        var first = new PreparationStep(Guid.NewGuid(), "Mix", "Gently");
        var second = new PreparationStep(Guid.NewGuid(), "Chill", "Overnight");
        var version = recipe.Versions[0] with { Content = new() { Steps = [first, second] } };
        recipe = recipe with { Versions = [version] };
        service.GetRecipeAsync(recipe.Id, Arg.Any<CancellationToken>()).Returns(recipe);
        service.SaveVersionAsync(recipe.Id, version.Id, Arg.Any<VersionRequest>(), Arg.Any<CancellationToken>()).Returns(new ChangeRejected("Keep edits", 409));
        var component = context.Render<VersionEditor>(p => p.Add(c => c.RecipeId, recipe.Id).Add(c => c.VersionId, version.Id));

        await component.FindAll("fluent-button").First(b => string.Equals(b.TextContent.Trim(), "Move down", StringComparison.Ordinal)).ClickAsync();
        await DeleteButton(component, "Save draft").ClickAsync();

        await service.Received(1).SaveVersionAsync(recipe.Id, version.Id,
            Arg.Is<VersionRequest>(r => r.Content.Steps.SequenceEqual(new[] { second, first })), Arg.Any<CancellationToken>());
        component.Find(".save-bar").TextContent.ShouldContain("Unsaved changes");
    }
}
