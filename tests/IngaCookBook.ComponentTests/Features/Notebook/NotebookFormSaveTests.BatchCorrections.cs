using Bunit;
using IngaCookBook.SharedKernel.Notebook;
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
    public async Task NewRecipeRetainsRejectedInputAndClearsGuardBeforeSuccessfulNavigation()
    {
        await using var context = new BunitContext();
        var (service, recipe) = Configure(context);
        var module = context.JSInterop.SetupModule("./_content/IngaCookBook.UI/Features/Notebook/Pages/VersionEditor.razor.js");
        var component = context.Render<NewRecipe>();
        await component.Find("fluent-text-input").ChangeAsync(new ChangeEventArgs { Value = "Keep my recipe" });
        service.CreateRecipeAsync(Arg.Any<NewRecipeRequest>(), Arg.Any<CancellationToken>()).Returns(new ChangeRejected("Try again"));
        await component.FindAll("fluent-button").Single(b => b.TextContent.Contains("Create recipe", StringComparison.Ordinal)).ClickAsync();
        component.Find("fluent-text-input").GetAttribute("value").ShouldBe("Keep my recipe");
        component.Find("[role=alert]").TextContent.ShouldBe("Try again");
        module.Invocations["updateGuard"][^1].Arguments[1].ShouldBe(true);
        service.CreateRecipeAsync(Arg.Any<NewRecipeRequest>(), Arg.Any<CancellationToken>()).Returns(new ChangeSaved(recipe.Id));
        await component.FindAll("fluent-button").Single(b => b.TextContent.Contains("Create recipe", StringComparison.Ordinal)).ClickAsync();
        module.Invocations["updateGuard"][^1].Arguments[1].ShouldBe(false);
        context.Services.GetRequiredService<NavigationManager>().Uri.ShouldEndWith($"/recipes/{recipe.Id}/versions/{recipe.Versions[0].Id}/edit");
        await service.Received(2).CreateRecipeAsync(Arg.Is<NewRecipeRequest>(r => r.Name == "Keep my recipe"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SettingsMetricEditsRemainGuardedAfterRejectionAndClearAfterSave()
    {
        await using var context = new BunitContext();
        var (service, recipe) = Configure(context);
        var module = context.JSInterop.SetupModule("./_content/IngaCookBook.UI/Features/Notebook/Pages/VersionEditor.razor.js");
        var component = context.Render<RecipeSettings>(p => p.Add(c => c.RecipeId, recipe.Id));
        await component.FindAll("fluent-button").Single(b => b.TextContent.Contains("Add metric", StringComparison.Ordinal)).ClickAsync();
        module.Invocations["updateGuard"][^1].Arguments[1].ShouldBe(true);
        var metric = component.FindComponents<FluentTextInput>()[^1];
        await metric.Find("fluent-text-input").ChangeAsync(new ChangeEventArgs { Value = "Fruit freshness" });
        service.SaveSettingsAsync(recipe.Id, Arg.Any<RecipeSettingsRequest>(), Arg.Any<CancellationToken>()).Returns(new ChangeRejected("Stale recipe", 409));
        await component.FindAll("fluent-button").Single(b => b.TextContent.Contains("Save settings", StringComparison.Ordinal)).ClickAsync();
        metric.Instance.Value.ShouldBe("Fruit freshness");
        module.Invocations["updateGuard"][^1].Arguments[1].ShouldBe(true);
        component.Find("[role=alert]").TextContent.ShouldBe("Stale recipe");
        service.SaveSettingsAsync(recipe.Id, Arg.Any<RecipeSettingsRequest>(), Arg.Any<CancellationToken>()).Returns(new ChangeSaved(recipe.Id));
        service.GetRecipeAsync(recipe.Id, Arg.Any<CancellationToken>()).Returns(recipe with { Metrics = [.. recipe.Metrics, new(Guid.NewGuid(), "Fruit freshness")] });
        await component.FindAll("fluent-button").Single(b => b.TextContent.Contains("Save settings", StringComparison.Ordinal)).ClickAsync();
        module.Invocations["updateGuard"][^1].Arguments[1].ShouldBe(false);
        component.FindComponents<FluentTextInput>()[^1].Instance.Value.ShouldBe("Fruit freshness");
        await service.Received(2).SaveSettingsAsync(recipe.Id,
            Arg.Is<RecipeSettingsRequest>(r => r.Metrics.Count == 2 && r.Metrics[1].Name == "Fruit freshness"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BatchCorrectionRetainsConflictInputsAndDoesNotCreateAnotherBatch()
    {
        await using var context = new BunitContext();
        var (service, recipe) = Configure(context);
        var version = recipe.Versions[0];
        var batch = version.Batches[0];
        var component = context.Render<BatchJournal>(p => p.Add(c => c.RecipeId, recipe.Id).Add(c => c.VersionId, version.Id));
        await component.FindAll("fluent-button").Single(b => b.TextContent.Trim().Equals("Correct batch", StringComparison.Ordinal)).ClickAsync();
        await component.Find("#made-date").ChangeAsync(new ChangeEventArgs { Value = "2026-10-01" });
        await component.FindComponents<FluentTextArea>().Single(c => string.Equals(c.Instance.Label, "What happened during preparation?", StringComparison.Ordinal))
            .Find("fluent-textarea").ChangeAsync(new ChangeEventArgs { Value = "Actually aged 18 hours" });
        var reason = component.FindComponents<FluentTextArea>().Single(c => string.Equals(c.Instance.Label, "Why are you correcting this batch?", StringComparison.Ordinal));
        await reason.Find("fluent-textarea").ChangeAsync(new ChangeEventArgs { Value = "Misread the clock" });
        service.CorrectBatchAsync(recipe.Id, version.Id, batch.Id, Arg.Any<BatchCorrectionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ChangeRejected("Another tab saved first", 409));
        await component.FindAll("fluent-button").Single(b => b.TextContent.Contains("Save batch correction", StringComparison.Ordinal)).ClickAsync();
        await service.Received(1).CorrectBatchAsync(recipe.Id, version.Id, batch.Id,
            Arg.Is<BatchCorrectionRequest>(r => r.Reason == "Misread the clock" && r.Batch.Revision == recipe.Revision
                && r.Batch.MadeAt == new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero) && r.Batch.Notes == "Actually aged 18 hours"), Arg.Any<CancellationToken>());
        await service.DidNotReceive().MakeBatchAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<BatchRequest>(), Arg.Any<CancellationToken>());
        component.Find("#made-date").GetAttribute("value").ShouldBe("2026-10-01");
        reason.Instance.Value.ShouldBe("Misread the clock");
        component.Find("[role=alert]").TextContent.ShouldBe("Another tab saved first");
    }
}
