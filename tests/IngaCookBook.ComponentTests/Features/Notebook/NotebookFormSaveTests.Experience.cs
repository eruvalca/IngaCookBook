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
    public async Task EmptyRecipeNameShowsAFieldErrorWithoutCreatingADraft()
    {
        await using var context = new BunitContext();
        var (service, _) = Configure(context);
        var component = context.Render<NewRecipe>();
        await DeleteButton(component, "Create recipe & first version").ClickAsync();
        var name = component.FindComponents<FluentTextInput>().Single(c => string.Equals(c.Instance.Label, "Recipe name", StringComparison.Ordinal));
        name.Instance.Message.ShouldBe("Enter a recipe name.");
        name.Instance.MessageState.ShouldBe(MessageState.Error);
        await service.DidNotReceive().CreateRecipeAsync(Arg.Any<NewRecipeRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StarterCriteriaPreserveCustomNamesAndDoNotAddDuplicates()
    {
        await using var context = new BunitContext();
        Configure(context);
        var component = context.Render<NewRecipe>();
        await component.FindComponents<FluentTextInput>().Single(c => string.Equals(c.Instance.Label, "Evaluation metric", StringComparison.Ordinal))
            .Find("fluent-text-input").ChangeAsync(new ChangeEventArgs { Value = "Toasted rice flavor" });
        await DeleteButton(component, "Use ice cream starter metrics").ClickAsync();
        await DeleteButton(component, "Use ice cream starter metrics").ClickAsync();
        var names = component.FindComponents<FluentTextInput>().Where(c => string.Equals(c.Instance.Label, "Evaluation metric", StringComparison.Ordinal)).Select(c => c.Instance.Value).ToArray();
        names.Length.ShouldBe(6);
        names.ShouldContain(value => string.Equals(value, "Toasted rice flavor", StringComparison.Ordinal));
        names.ShouldContain(value => string.Equals(value, "Creaminess", StringComparison.Ordinal));
        names.Distinct(StringComparer.OrdinalIgnoreCase).Count().ShouldBe(6);
    }

    [Fact]
    public async Task SwitchingJournalTasksKeepsUnfinishedPreparationAndTastingNotes()
    {
        await using var context = new BunitContext();
        var (_, recipe) = Configure(context);
        var component = context.Render<BatchJournal>(p => p.Add(c => c.RecipeId, recipe.Id).Add(c => c.VersionId, recipe.Versions[0].Id));
        await DeleteButton(component, "Record a batch").ClickAsync();
        await component.FindComponents<FluentTextArea>().Single(c => string.Equals(c.Instance.Label, "What happened during preparation?", StringComparison.Ordinal))
            .Find("fluent-textarea").ChangeAsync(new ChangeEventArgs { Value = "Aged 18 hours" });
        await DeleteButton(component, "Add a tasting").ClickAsync();
        await component.FindComponents<FluentTextArea>().Single(c => string.Equals(c.Instance.Label, "Overall observations", StringComparison.Ordinal))
            .Find("fluent-textarea").ChangeAsync(new ChangeEventArgs { Value = "Smooth after freezing" });
        await DeleteButton(component, "Record a batch").ClickAsync();
        component.FindComponents<FluentTextArea>().Single(c => string.Equals(c.Instance.Label, "What happened during preparation?", StringComparison.Ordinal)).Instance.Value.ShouldBe("Aged 18 hours");
        await DeleteButton(component, "Add a tasting").ClickAsync();
        component.FindComponents<FluentTextArea>().Single(c => string.Equals(c.Instance.Label, "Overall observations", StringComparison.Ordinal)).Instance.Value.ShouldBe("Smooth after freezing");
    }

    [Fact]
    public async Task SavedTastingReceiptRetainsScoresNotesAndNextIdeaAfterFormResets()
    {
        await using var context = new BunitContext();
        var (service, recipe) = Configure(context);
        service.EvaluateAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<EvaluationRequest>(), Arg.Any<CancellationToken>()).Returns(new ChangeSaved(Guid.NewGuid()));
        var component = context.Render<BatchJournal>(p => p.Add(c => c.RecipeId, recipe.Id).Add(c => c.VersionId, recipe.Versions[0].Id));
        await component.FindComponents<FluentTextInput>().Single(c => string.Equals(c.Instance.Label, "Texture", StringComparison.Ordinal)).Find("fluent-text-input").ChangeAsync(new ChangeEventArgs { Value = "8" });
        await component.FindComponents<FluentTextArea>().Single(c => string.Equals(c.Instance.Label, "Notes for Texture", StringComparison.Ordinal)).Find("fluent-textarea").ChangeAsync(new ChangeEventArgs { Value = "Smooth, a little firm" });
        await component.FindComponents<FluentTextArea>().Single(c => string.Equals(c.Instance.Label, "One thing to try next", StringComparison.Ordinal)).Find("fluent-textarea").ChangeAsync(new ChangeEventArgs { Value = "Age for 12 hours" });
        await DeleteButton(component, "Save evaluation").ClickAsync();
        var receipt = component.Find(".completion-receipt").TextContent;
        receipt.ShouldContain("Your tasting is saved.");
        receipt.ShouldContain("Batch 1");
        receipt.ShouldContain("8 / 10");
        receipt.ShouldContain("Smooth, a little firm");
        receipt.ShouldContain("Age for 12 hours");
        component.FindComponents<FluentTextInput>().Single(c => string.Equals(c.Instance.Label, "Texture", StringComparison.Ordinal)).Instance.Value.ShouldBeEmpty();
    }

    [Fact]
    public async Task NextIdeaStartsAnUnsavedExperimentWithoutChangingInheritedIngredients()
    {
        await using var context = new BunitContext();
        var (service, recipe) = Configure(context);
        var parent = recipe.Versions[0];
        var evaluation = new BatchEvaluation(Guid.NewGuid(), DateTimeOffset.UtcNow, "", "Try longer aging", []);
        parent = parent with { Batches = [parent.Batches[0] with { Evaluations = [evaluation] }] };
        var draft = new RecipeVersion { Number = 2, ParentId = parent.Id, Content = parent.Content };
        recipe = recipe with { Versions = [parent, draft] };
        service.GetRecipeAsync(recipe.Id, Arg.Any<CancellationToken>()).Returns(recipe);
        context.Services.GetRequiredService<NavigationManager>().NavigateTo($"/recipes/{recipe.Id}/versions/{draft.Id}/edit?idea={evaluation.Id}");
        var component = context.Render<VersionEditor>(p => p.Add(c => c.RecipeId, recipe.Id).Add(c => c.VersionId, draft.Id));
        component.FindComponents<FluentTextArea>().Single(c => string.Equals(c.Instance.Label, "What are you testing?", StringComparison.Ordinal)).Instance.Value.ShouldBe("Try longer aging");
        component.FindComponents<FluentTextInput>().Single(c => string.Equals(c.Instance.Label, "Ingredient name", StringComparison.Ordinal)).Instance.Value.ShouldBe("Cream");
        component.Find(".save-bar").TextContent.ShouldContain("Unsaved changes");
        await service.DidNotReceive().SaveVersionAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<VersionRequest>(), Arg.Any<CancellationToken>());
        var question = component.FindComponents<FluentTextArea>().Single(c => string.Equals(c.Instance.Label, "What are you testing?", StringComparison.Ordinal));
        await question.Find("fluent-textarea").ChangeAsync(new ChangeEventArgs { Value = "Will exactly 18 hours improve texture?" });
        service.SaveVersionAsync(recipe.Id, draft.Id, Arg.Any<VersionRequest>(), Arg.Any<CancellationToken>()).Returns(new ChangeSaved(draft.Id));
        service.GetRecipeAsync(recipe.Id, Arg.Any<CancellationToken>()).Returns(recipe with
        {
            Versions = [parent, draft with { Content = draft.Content with { Hypothesis = "Will exactly 18 hours improve texture?" } }],
        });
        await DeleteButton(component, "Save draft").ClickAsync();
        await service.Received(1).SaveVersionAsync(recipe.Id, draft.Id,
            Arg.Is<VersionRequest>(r => r.Content.Hypothesis == "Will exactly 18 hours improve texture?"), Arg.Any<CancellationToken>());
        context.Services.GetRequiredService<NavigationManager>().Uri.ShouldEndWith($"/recipes/{recipe.Id}/versions/{draft.Id}/edit");
        question.Instance.Value.ShouldBe("Will exactly 18 hours improve texture?");
    }
}
