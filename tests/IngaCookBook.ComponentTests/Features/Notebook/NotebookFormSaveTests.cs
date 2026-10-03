using System.Diagnostics.CodeAnalysis;
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

[SuppressMessage("Maintainability", "CA1515:Consider making public types internal", Justification = "xUnit requires public test classes for discovery.")]
public sealed class NotebookFormSaveTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task VersionEditorLocksInputsUntilSaveAndReloadFinish(bool succeeds)
    {
        await using var context = new BunitContext();
        var (service, recipe) = Configure(context);
        var pending = new TaskCompletionSource<NotebookChange>(TaskCreationOptions.RunContinuationsAsynchronously);
        var reload = new TaskCompletionSource<RecipeDocument?>(TaskCreationOptions.RunContinuationsAsynchronously);
        service.SaveVersionAsync(recipe.Id, recipe.Versions[0].Id, Arg.Any<VersionRequest>(), Arg.Any<CancellationToken>()).Returns(pending.Task);
        var component = context.Render<VersionEditor>(p => p.Add(c => c.RecipeId, recipe.Id).Add(c => c.VersionId, recipe.Versions[0].Id));
        await component.Find("fluent-text-input").ChangeAsync(new ChangeEventArgs { Value = "Precise formulation" });
        service.GetRecipeAsync(recipe.Id, Arg.Any<CancellationToken>()).Returns(reload.Task);
        var save = component.FindAll("fluent-button").Single(b => b.TextContent.Contains("Save draft", StringComparison.Ordinal)).ClickAsync();
        await component.WaitForAssertionAsync(() => InputsAreDisabled(component, true));
        await service.Received(1).SaveVersionAsync(recipe.Id, recipe.Versions[0].Id,
            Arg.Is<VersionRequest>(r => r.Content.Label == "Precise formulation"), Arg.Any<CancellationToken>());
        pending.SetResult(succeeds ? new ChangeSaved(recipe.Versions[0].Id) : new ChangeRejected("Try again"));
        if (succeeds)
        {
            await component.WaitForAssertionAsync(() => component.Find(".save-bar").TextContent.ShouldContain("Saving"));
            InputsAreDisabled(component, true);
            reload.SetResult(recipe with { Versions = [recipe.Versions[0] with { Content = recipe.Versions[0].Content with { Label = "Precise formulation" } }] });
        }
        await save;
        InputsAreDisabled(component, false);
        component.Find("fluent-text-input").GetAttribute("value").ShouldBe("Precise formulation");
        component.Markup.ShouldContain(succeeds ? "All changes saved" : "Try again");
    }

    [Fact]
    public async Task TastingInputsUnlockAfterARejectedSave()
    {
        await using var context = new BunitContext();
        var (service, recipe) = Configure(context);
        var pending = new TaskCompletionSource<NotebookChange>(TaskCreationOptions.RunContinuationsAsynchronously);
        service.EvaluateAsync(recipe.Id, recipe.Versions[0].Id, recipe.Versions[0].Batches[0].Id,
            Arg.Any<EvaluationRequest>(), Arg.Any<CancellationToken>()).Returns(pending.Task);
        var component = context.Render<BatchJournal>(p => p.Add(c => c.RecipeId, recipe.Id).Add(c => c.VersionId, recipe.Versions[0].Id));
        var save = component.FindAll("fluent-button").Single(b => b.TextContent.Contains("Save evaluation", StringComparison.Ordinal)).ClickAsync();
        await component.WaitForAssertionAsync(() => InputsAreDisabled(component, true));
        component.FindAll("input[type=date]").ShouldAllBe(input => input.HasAttribute("disabled"));
        pending.SetResult(new ChangeRejected("Keep these tasting notes"));
        await save;
        InputsAreDisabled(component, false);
        component.FindAll("input[type=date]").ShouldAllBe(input => !input.HasAttribute("disabled"));
        component.Markup.ShouldContain("Keep these tasting notes");
    }

    [Fact]
    public async Task JournalWaitsForBrowserDateAndPreservesSelectedDatesAfterSaving()
    {
        await using var context = new BunitContext();
        var (service, recipe) = Configure(context, configureDate: false);
        var module = context.JSInterop.SetupModule("./_content/IngaCookBook.UI/Features/Notebook/Pages/BatchJournal.razor.js");
        var date = module.Setup<string>("localDate");
        var component = context.Render<BatchJournal>(p => p.Add(c => c.RecipeId, recipe.Id).Add(c => c.VersionId, recipe.Versions[0].Id));
        component.FindAll("input[type=date]").ShouldAllBe(input => input.HasAttribute("disabled") && string.IsNullOrEmpty(input.GetAttribute("value")));
        date.SetResult("2026-10-02");
        await component.WaitForAssertionAsync(() => component.Find("#made-date").GetAttribute("value").ShouldBe("2026-10-02"));
        component.Find("#tasting-date").GetAttribute("value").ShouldBe("2026-10-02");
        await component.Find("#made-date").ChangeAsync(new ChangeEventArgs { Value = "2026-10-01" });
        await component.Find("#tasting-date").ChangeAsync(new ChangeEventArgs { Value = "2026-10-03" });
        service.MakeBatchAsync(recipe.Id, recipe.Versions[0].Id, Arg.Any<BatchRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ChangeSaved(recipe.Versions[0].Batches[0].Id));
        await component.FindAll("fluent-button").Single(b => b.TextContent.Contains("Record batch", StringComparison.Ordinal)).ClickAsync();
        await service.Received(1).MakeBatchAsync(recipe.Id, recipe.Versions[0].Id,
            Arg.Is<BatchRequest>(r => r.MadeAt == new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero)), Arg.Any<CancellationToken>());
        component.Find("#made-date").GetAttribute("value").ShouldBe("2026-10-01");
        component.Find("#tasting-date").GetAttribute("value").ShouldBe("2026-10-03");
        module.Invocations["localDate"].Count.ShouldBe(1);
    }

    [Fact]
    public async Task RecipeSettingsLockInputsDuringSave()
    {
        await using var context = new BunitContext();
        var (service, recipe) = Configure(context);
        var pending = new TaskCompletionSource<NotebookChange>(TaskCreationOptions.RunContinuationsAsynchronously);
        service.SaveSettingsAsync(recipe.Id, Arg.Any<RecipeSettingsRequest>(), Arg.Any<CancellationToken>()).Returns(pending.Task);
        var component = context.Render<RecipeSettings>(p => p.Add(c => c.RecipeId, recipe.Id));
        var save = component.FindAll("fluent-button").Single(b => b.TextContent.Contains("Save settings", StringComparison.Ordinal)).ClickAsync();
        await component.WaitForAssertionAsync(() => InputsAreDisabled(component, true));
        pending.SetResult(new ChangeSaved(recipe.Id));
        await save;
        InputsAreDisabled(component, false);
    }

    private static void InputsAreDisabled<T>(IRenderedComponent<T> component, bool disabled) where T : IComponent
    {
        var inputs = component.FindAll("fluent-text-input, fluent-text-area, fluent-textarea, fluent-number-input, fluent-select, fluent-checkbox");
        inputs.ShouldNotBeEmpty();
        inputs.ShouldAllBe(input => input.HasAttribute("disabled") == disabled);
    }

    private static (INotebookService Service, RecipeDocument Recipe) Configure(BunitContext context, bool configureDate = true)
    {
        context.Services.AddFluentUIComponents();
        context.ComponentFactories.AddStub<FluentProviders>();
        context.ComponentFactories.AddStub<Microsoft.AspNetCore.Components.Routing.NavigationLock>();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        if (configureDate)
        {
            context.JSInterop.SetupModule("./_content/IngaCookBook.UI/Features/Notebook/Pages/BatchJournal.razor.js")
                .Setup<string>("localDate").SetResult("2026-10-02");
        }
        var service = Substitute.For<INotebookService>();
        context.Services.AddSingleton(service);
        context.Services.AddAuthorizationCore();
        context.Renderer.SetRendererInfo(new RendererInfo("Server", true));
        var recipe = new RecipeDocument
        {
            Metrics = [new(Guid.NewGuid(), "Texture")],
            Versions = [new RecipeVersion
            {
                Number = 1,
                Content = new RecipeContent { Ingredients = [new Ingredient { Name = "Cream", Quantity = 100 }], Steps = [new(Guid.NewGuid(), "Mix", "")] },
                Batches = [new(Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(-1), "", [])],
            }],
        };
        service.GetRecipeAsync(recipe.Id, Arg.Any<CancellationToken>()).Returns(recipe);
        service.GetRecipesAsync(Arg.Any<CancellationToken>()).Returns(new[] { recipe });
        service.GetWorkspaceAsync(Arg.Any<CancellationToken>()).Returns(new WorkspaceView(Guid.NewGuid(), "Kitchen", "USD"));
        return (service, recipe);
    }
}
