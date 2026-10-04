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
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReusedEditorCancelsOldLoadAndIgnoresItsLateResultOrFailure(bool fails)
    {
        await using var context = new BunitContext();
        var (service, first) = Configure(context);
        var pending = new TaskCompletionSource<RecipeDocument?>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken firstToken = default;
        service.GetRecipeAsync(first.Id, Arg.Any<CancellationToken>()).Returns(call =>
        {
            firstToken = call.ArgAt<CancellationToken>(1);
            return pending.Task; // Deliberately ignores cancellation to test the publication guard.
        });
        var second = first with { Id = Guid.NewGuid(), Name = "Chocolate", Versions = [first.Versions[0] with { Content = first.Versions[0].Content with { Label = "New route" } }] };
        service.GetRecipeAsync(second.Id, Arg.Any<CancellationToken>()).Returns(second);
        var component = context.Render<VersionEditor>(p => p.Add(c => c.RecipeId, first.Id).Add(c => c.VersionId, first.Versions[0].Id));
        component.Render(p => p.Add(c => c.RecipeId, second.Id).Add(c => c.VersionId, second.Versions[0].Id));
        await component.WaitForAssertionAsync(() => component.Find("h1").TextContent.ShouldBe("Chocolate"));
        firstToken.IsCancellationRequested.ShouldBeTrue();
        var rendered = component.RenderCount;
        if (fails) { pending.SetException(new HttpRequestException("Old route failed")); }
        else { pending.SetResult(first); }
        await component.WaitForAssertionAsync(() => component.RenderCount.ShouldBeGreaterThan(rendered));
        await component.WaitForAssertionAsync(() =>
        {
            component.Find("h1").TextContent.ShouldBe("Chocolate");
            component.FindComponents<FluentTextInput>().Single(c => string.Equals(c.Instance.Label, "Version label", StringComparison.Ordinal)).Instance.Value.ShouldBe("New route");
            component.FindAll("[role=alert]").ShouldBeEmpty();
            InputsAreDisabled(component, false);
        });
    }

    [Fact]
    public async Task DisposingEditorCancelsSaveAndCleansGuardWithoutReloadingLateSuccess()
    {
        await using var context = new BunitContext();
        var (service, recipe) = Configure(context);
        var pending = new TaskCompletionSource<NotebookChange>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken saveToken = default;
        service.SaveVersionAsync(recipe.Id, recipe.Versions[0].Id, Arg.Any<VersionRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            saveToken = call.ArgAt<CancellationToken>(3);
            return pending.Task;
        });
        var component = context.Render<VersionEditor>(p => p.Add(c => c.RecipeId, recipe.Id).Add(c => c.VersionId, recipe.Versions[0].Id));
        var saving = component.FindAll("fluent-button").Single(b => b.TextContent.Contains("Save draft", StringComparison.Ordinal)).ClickAsync();
        await component.WaitForAssertionAsync(() => saveToken.CanBeCanceled.ShouldBeTrue());
        await context.DisposeAsync();
        saveToken.IsCancellationRequested.ShouldBeTrue();
        context.JSInterop.Invocations.ShouldContain(invocation => string.Equals(invocation.Identifier, "disposeGuard", StringComparison.Ordinal));
        pending.SetResult(new ChangeSaved(recipe.Versions[0].Id));
        await saving;
        await service.Received(1).GetRecipeAsync(recipe.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TimedOutSaveRetainsEditedInputsAndExplainsUncertainOutcome()
    {
        await using var context = new BunitContext();
        var (service, recipe) = Configure(context);
        service.SaveVersionAsync(recipe.Id, recipe.Versions[0].Id, Arg.Any<VersionRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<NotebookChange>(new TaskCanceledException("HTTP timeout", new TimeoutException())));
        var component = context.Render<VersionEditor>(p => p.Add(c => c.RecipeId, recipe.Id).Add(c => c.VersionId, recipe.Versions[0].Id));
        var label = component.FindComponents<FluentTextInput>().Single(c => string.Equals(c.Instance.Label, "Version label", StringComparison.Ordinal));
        await label.Find("fluent-text-input").ChangeAsync(new ChangeEventArgs { Value = "Careful adjustment" });
        await component.FindAll("fluent-button").Single(b => b.TextContent.Contains("Save draft", StringComparison.Ordinal)).ClickAsync();
        label.Instance.Value.ShouldBe("Careful adjustment");
        component.Find("[role=alert]").TextContent.ShouldContain("save may have completed");
        component.Find(".save-bar").TextContent.ShouldContain("Unsaved changes");
        InputsAreDisabled(component, false);
        await service.Received(1).GetRecipeAsync(recipe.Id, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task JournalRetainsInputsWhenConfirmedSaveCannotRefresh(bool tasting)
    {
        await using var context = new BunitContext();
        var (service, recipe) = Configure(context);
        var version = recipe.Versions[0];
        var component = context.Render<BatchJournal>(p => p.Add(c => c.RecipeId, recipe.Id).Add(c => c.VersionId, version.Id));
        if (!tasting)
        {
            await component.FindAll("fluent-button").Single(b => string.Equals(b.TextContent.Trim(), "Record a batch", StringComparison.Ordinal)).ClickAsync();
        }
        var notes = component.FindComponents<FluentTextArea>().Single(c => string.Equals(c.Instance.Label,
            tasting ? "Overall observations" : "What happened during preparation?", StringComparison.Ordinal));
        await notes.Find("fluent-textarea").ChangeAsync(new ChangeEventArgs { Value = "Keep these careful notes" });
        if (tasting)
        {
            await component.FindComponents<FluentTextInput>().Single(c => string.Equals(c.Instance.Label, "Texture", StringComparison.Ordinal))
                .Find("fluent-text-input").ChangeAsync(new ChangeEventArgs { Value = "8" });
        }
        service.MakeBatchAsync(recipe.Id, version.Id, Arg.Any<BatchRequest>(), Arg.Any<CancellationToken>()).Returns(new ChangeSaved(Guid.NewGuid()));
        service.EvaluateAsync(recipe.Id, version.Id, version.Batches[0].Id, Arg.Any<EvaluationRequest>(), Arg.Any<CancellationToken>()).Returns(new ChangeSaved(Guid.NewGuid()));
        service.GetRecipeAsync(recipe.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<RecipeDocument?>(new TaskCanceledException("Refresh timeout", new TimeoutException())));

        await component.FindAll("fluent-button").Single(b => string.Equals(b.TextContent.Trim(),
            tasting ? "Save evaluation" : "Record batch", StringComparison.Ordinal)).ClickAsync();

        notes.Instance.Value.ShouldBe("Keep these careful notes");
        component.Find("[role=alert]").TextContent.ShouldContain("save may have completed");
        component.FindAll(".completion-receipt").ShouldBeEmpty();
        InputsAreDisabled(component, false);
        await service.Received(2).GetRecipeAsync(recipe.Id, Arg.Any<CancellationToken>());
        if (tasting)
        {
            component.FindComponents<FluentTextInput>().Single(c => string.Equals(c.Instance.Label, "Texture", StringComparison.Ordinal)).Instance.Value.ShouldBe("8");
            await service.Received(1).EvaluateAsync(recipe.Id, version.Id, version.Batches[0].Id,
                Arg.Is<EvaluationRequest>(r => r.Notes == "Keep these careful notes" && r.Scores[0].Score == 8), Arg.Any<CancellationToken>());
        }
        else
        {
            await service.Received(1).MakeBatchAsync(recipe.Id, version.Id,
                Arg.Is<BatchRequest>(r => r.Notes == "Keep these careful notes"), Arg.Any<CancellationToken>());
        }
    }

    [Theory]
    [InlineData("Static", false, true)]
    [InlineData("Server", true, false)]
    [InlineData("WebAssembly", true, false)]
    public async Task RequestAbortCancelsOnlyStaticRendering(string renderer, bool interactive, bool canceled)
    {
        await using var context = new BunitContext();
        using var request = new CancellationTokenSource();
        context.Services.AddCascadingValue("RequestAborted", _ => request.Token);
        var (service, _) = Configure(context);
        context.Renderer.SetRendererInfo(new RendererInfo(renderer, interactive));
        var pending = new TaskCompletionSource<WorkspaceView?>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken loadToken = default;
        service.GetWorkspaceAsync(Arg.Any<CancellationToken>()).Returns(call =>
        {
            loadToken = call.Arg<CancellationToken>();
            return pending.Task;
        });
        var component = context.Render<NewRecipe>();
        loadToken.CanBeCanceled.ShouldBeTrue();
        await request.CancelAsync();
        loadToken.IsCancellationRequested.ShouldBe(canceled);
        component.FindAll("[role=alert]").ShouldBeEmpty();
        await context.DisposeAsync();
        loadToken.IsCancellationRequested.ShouldBeTrue();
        pending.SetResult(new WorkspaceView(Guid.NewGuid(), "Kitchen", "USD"));
    }

    [Fact]
    public async Task FailedEditorDependencyLoadDoesNotExposeAnotherRecipesEditableContent()
    {
        await using var context = new BunitContext();
        var (service, recipe) = Configure(context);
        var component = context.Render<VersionEditor>(p => p.Add(c => c.RecipeId, recipe.Id).Add(c => c.VersionId, recipe.Versions[0].Id));
        service.GetWorkspaceAsync(Arg.Any<CancellationToken>()).Returns(Task.FromException<WorkspaceView?>(new HttpRequestException("Offline")));
        component.Render(p => p.Add(c => c.RecipeId, Guid.NewGuid()));
        await component.WaitForAssertionAsync(() => component.Find("[role=alert]").TextContent.ShouldContain("couldn't reach"));
        component.FindAll("fluent-button").ShouldNotContain(b => b.TextContent.Contains("Save draft", StringComparison.Ordinal));
    }
}
