using Bunit;
using IngaCookBook.SharedKernel.Notebook;
using IngaCookBook.UI.Features.Notebook.Components;
using IngaCookBook.UI.Features.Notebook.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace IngaCookBook.ComponentTests.Features.Notebook;

public sealed partial class NotebookFormSaveTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DraftDeletionRequiresConfirmationAndNavigatesAfterSuccess(bool onlyVersion)
    {
        await using var context = new BunitContext();
        var (service, recipe) = Configure(context);
        service.DeleteDraftAsync(recipe.Id, recipe.Versions[0].Id, new(recipe.Revision), Arg.Any<CancellationToken>())
            .Returns(new ChangeSaved(recipe.Versions[0].Id));
        var component = context.Render<VersionActions>(p => p.Add(c => c.RecipeId, recipe.Id)
            .Add(c => c.VersionId, recipe.Versions[0].Id).Add(c => c.Revision, recipe.Revision).Add(c => c.OnlyVersion, onlyVersion));
        await DeleteButton(component, "Delete draft").ClickAsync();
        component.Find("[aria-label='Delete draft confirmation']").TextContent.ShouldContain(onlyVersion ? "only version" : "other versions");
        await DeleteButton(component, "Keep draft").ClickAsync();
        await service.DidNotReceive().DeleteDraftAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<RevisionRequest>(), Arg.Any<CancellationToken>());
        await DeleteButton(component, "Delete draft").ClickAsync();
        await DeleteButton(component, "Delete permanently").ClickAsync();
        await service.Received(1).DeleteDraftAsync(recipe.Id, recipe.Versions[0].Id, new(recipe.Revision), Arg.Any<CancellationToken>());
        context.Services.GetRequiredService<NavigationManager>().Uri.ShouldEndWith(onlyVersion ? "/recipes" : $"/recipes/{recipe.Id}");
    }

    [Fact]
    public async Task RejectedDraftDeletionRetainsEditorInputsAndUnlocksTheForm()
    {
        await using var context = new BunitContext();
        var (service, recipe) = Configure(context);
        var pending = new TaskCompletionSource<NotebookChange>(TaskCreationOptions.RunContinuationsAsynchronously);
        service.DeleteDraftAsync(recipe.Id, recipe.Versions[0].Id, new(recipe.Revision), Arg.Any<CancellationToken>()).Returns(pending.Task);
        var component = context.Render<VersionEditor>(p => p.Add(c => c.RecipeId, recipe.Id).Add(c => c.VersionId, recipe.Versions[0].Id));
        await component.Find("fluent-text-input").ChangeAsync(new ChangeEventArgs { Value = "Unfinished idea" });
        await DeleteButton(component, "Delete draft").ClickAsync();
        var before = context.Services.GetRequiredService<NavigationManager>().Uri;
        var deletion = DeleteButton(component, "Delete permanently").ClickAsync();
        await component.WaitForAssertionAsync(() => InputsAreDisabled(component, true));
        DeleteButton(component, "Delete permanently").HasAttribute("disabled").ShouldBeTrue();
        pending.SetResult(new ChangeRejected("Reload this changed recipe.", 409));
        await deletion;
        InputsAreDisabled(component, false);
        component.Find("fluent-text-input").GetAttribute("value").ShouldBe("Unfinished idea");
        component.Find("[role=alert]").TextContent.ShouldContain("Reload");
        component.Find(".save-bar").TextContent.ShouldContain("Unsaved changes");
        context.Services.GetRequiredService<NavigationManager>().Uri.ShouldBe(before);
    }

    [Fact]
    public async Task PreservedVersionsDoNotOfferDraftDeletion()
    {
        await using var context = new BunitContext();
        var (service, recipe) = Configure(context);
        service.GetRecipeAsync(recipe.Id, Arg.Any<CancellationToken>()).Returns(recipe with
        {
            Versions = [recipe.Versions[0] with { IsLocked = true }],
        });
        var editor = context.Render<VersionEditor>(p => p.Add(c => c.RecipeId, recipe.Id).Add(c => c.VersionId, recipe.Versions[0].Id));
        editor.FindComponents<DraftDeletion>().ShouldBeEmpty();
        var actions = context.Render<VersionActions>(p => p.Add(c => c.RecipeId, recipe.Id)
            .Add(c => c.VersionId, recipe.Versions[0].Id).Add(c => c.Revision, recipe.Revision).Add(c => c.OnlyVersion, true).Add(c => c.Locked, true));
        actions.FindComponents<DraftDeletion>().ShouldBeEmpty();
    }

    private static AngleSharp.Dom.IElement DeleteButton<T>(IRenderedComponent<T> component, string text) where T : IComponent =>
        component.FindAll("fluent-button").Single(b => string.Equals(b.TextContent.Trim(), text, StringComparison.Ordinal));
}
