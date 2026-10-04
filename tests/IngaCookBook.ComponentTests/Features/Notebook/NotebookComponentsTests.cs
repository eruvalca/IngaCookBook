using System.Diagnostics.CodeAnalysis;
using Bunit;
using IngaCookBook.SharedKernel.Notebook;
using IngaCookBook.UI.Features.Notebook.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;
using NSubstitute;
using Shouldly;
using Xunit;

namespace IngaCookBook.ComponentTests.Features.Notebook;

[SuppressMessage("Maintainability", "CA1515:Consider making public types internal", Justification = "xUnit requires public test classes for discovery.")]
public sealed class NotebookComponentsTests
{
    [Fact]
    public async Task VariationNavigatesToCreatedDraftAndCarriesTheExpectedRevision()
    {
        await using var context = new BunitContext();
        context.Services.AddFluentUIComponents();
        context.ComponentFactories.AddStub<FluentProviders>();
        context.JSInterop.Mode = JSRuntimeMode.Loose; // Real Fluent JavaScript is exercised in the browser suite.
        var service = Substitute.For<INotebookService>();
        context.Services.AddSingleton(service);
        context.Renderer.SetRendererInfo(new RendererInfo("Server", true));
        var recipe = Guid.NewGuid();
        var version = Guid.NewGuid();
        var revision = Guid.NewGuid();
        var created = Guid.NewGuid();
        service.VaryAsync(recipe, version, new(revision), Arg.Any<CancellationToken>()).Returns(new ChangeSaved(created));
        var component = context.Render<VersionActions>(p => p.Add(c => c.RecipeId, recipe).Add(c => c.VersionId, version).Add(c => c.Revision, revision).Add(c => c.OnlyVersion, true));
        await component.FindAll("fluent-button").Single(b => b.TextContent.Contains("Try a variation", StringComparison.Ordinal)).ClickAsync();
        await component.WaitForAssertionAsync(() =>
            context.Services.GetRequiredService<NavigationManager>().Uri.ShouldEndWith($"/recipes/{recipe}/versions/{created}/edit"));
        await service.Received(1).VaryAsync(recipe, version, new(revision), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConflictingEditShowsRecoveryMessageWithoutNavigating()
    {
        await using var context = new BunitContext();
        context.Services.AddFluentUIComponents();
        context.ComponentFactories.AddStub<FluentProviders>();
        context.JSInterop.Mode = JSRuntimeMode.Loose; // Real Fluent JavaScript is exercised in the browser suite.
        var service = Substitute.For<INotebookService>();
        context.Services.AddSingleton(service);
        context.Renderer.SetRendererInfo(new RendererInfo("Server", true));
        service.VaryAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<RevisionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ChangeRejected("Another device saved changes. Reload first.", 409));
        var component = context.Render<VersionActions>(p => p.Add(c => c.RecipeId, Guid.NewGuid())
            .Add(c => c.VersionId, Guid.NewGuid()).Add(c => c.Revision, Guid.NewGuid()).Add(c => c.OnlyVersion, true));
        var before = context.Services.GetRequiredService<NavigationManager>().Uri;
        await component.FindAll("fluent-button").Single(b => b.TextContent.Contains("Try a variation", StringComparison.Ordinal)).ClickAsync();
        await component.WaitForAssertionAsync(() => component.Find("[role=alert]").TextContent.ShouldContain("Reload first"));
        context.Services.GetRequiredService<NavigationManager>().Uri.ShouldBe(before);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PrerenderedActionsStayDisabledUntilInteractive(bool interactive)
    {
        await using var context = new BunitContext();
        context.Services.AddFluentUIComponents();
        context.ComponentFactories.AddStub<FluentProviders>();
        context.JSInterop.Mode = JSRuntimeMode.Loose; // Real Fluent JavaScript is exercised in the browser suite.
        context.Services.AddSingleton(Substitute.For<INotebookService>());
        context.Renderer.SetRendererInfo(new RendererInfo("Server", interactive));
        var component = context.Render<VersionActions>(p => p.Add(c => c.RecipeId, Guid.NewGuid())
            .Add(c => c.VersionId, Guid.NewGuid()).Add(c => c.Revision, Guid.NewGuid()).Add(c => c.OnlyVersion, true));
        component.FindAll("fluent-button")[0].HasAttribute("disabled").ShouldBe(!interactive);
    }

    [Fact]
    public async Task RecipeSheetRendersPinnedSubrecipeAndPreservesTextWithoutInterpretingMarkup()
    {
        await using var context = new BunitContext();
        context.Services.AddFluentUIComponents();
        var nested = new RecipeContent { Label = "Caramel", Steps = [new(Guid.NewGuid(), "Cook gently", "")] };
        var content = new RecipeContent
        {
            Ingredients = [new Ingredient { Name = "<b>Sauce</b>", Quantity = 25, LinkedContent = nested }],
            Steps = [new(Guid.NewGuid(), "Fold into the base", "Keep chilled")],
        };
        var component = context.Render<RecipeSheet>(p => p.Add(c => c.Content, content));
        component.FindAll("b").ShouldBeEmpty();
        component.Markup.ShouldContain("&lt;b&gt;Sauce&lt;/b&gt;");
        component.Find("summary").TextContent.ShouldContain("Caramel");
        component.Markup.ShouldContain("Cook gently");
        component.Markup.ShouldContain("Keep chilled");
    }

    [Fact]
    public async Task KitchenSheetRendersRecipeNotesAsPlainTextWithLineBreaks()
    {
        await using var context = new BunitContext();
        context.Services.AddFluentUIComponents();
        context.ComponentFactories.AddStub<FluentProviders>();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Renderer.SetRendererInfo(new RendererInfo("Static", false));
        const string Notes = "Rest overnight.\nServe with <b>caramel</b>.\nStore covered.";
        var content = new RecipeContent { Notes = Notes };
        var component = context.Render<KitchenSheet>(p => p.Add(c => c.Content, content));

        component.FindAll("h2").ShouldContain(h => string.Equals(h.TextContent, "Recipe notes", StringComparison.Ordinal));
        component.Find(".preserve-lines").TextContent.ShouldBe(Notes);
        component.FindAll("b").ShouldBeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" \n\t")]
    public async Task KitchenSheetOmitsEmptyRecipeNotes(string notes)
    {
        await using var context = new BunitContext();
        context.Services.AddFluentUIComponents();
        context.ComponentFactories.AddStub<FluentProviders>();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Renderer.SetRendererInfo(new RendererInfo("Static", false));
        var component = context.Render<KitchenSheet>(p => p.Add(c => c.Content, new RecipeContent { Notes = notes }));

        component.FindAll("h2").ShouldNotContain(h => string.Equals(h.TextContent, "Recipe notes", StringComparison.Ordinal));
        component.FindAll(".preserve-lines").ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InterruptedPhotoSequenceReportsEarlierSavesAndStopsLaterUploads(bool storageRejection)
    {
        await using var context = new BunitContext();
        var service = Substitute.For<INotebookService>();
        context.Services.AddSingleton(service);
        context.Renderer.SetRendererInfo(new RendererInfo("Server", true));
        var uploaded = 0;
        var failedUpload = storageRejection ? Task.FromResult<NotebookChange>(new ChangeRejected("Storage unavailable. Cleanup queued.", 503)) :
            Task.FromException<NotebookChange>(new HttpRequestException("Connection lost"));
        service.UploadPhotoAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => ++uploaded == 1 ? Task.FromResult<NotebookChange>(new ChangeSaved(Guid.NewGuid())) :
                failedUpload);
        var component = context.Render<PhotoUpload>(p => p.Add(c => c.RecipeId, Guid.NewGuid()).Add(c => c.VersionId, Guid.NewGuid()));
        component.FindComponent<InputFile>().UploadFiles(
            InputFileContent.CreateFromBinary([1], "first.png", contentType: "image/png"),
            InputFileContent.CreateFromBinary([2], "second.png", contentType: "image/png"),
            InputFileContent.CreateFromBinary([3], "third.png", contentType: "image/png"));
        await component.WaitForAssertionAsync(() => component.Find("[role=alert]").TextContent.ShouldContain(
            storageRejection ? "1 photos have already been saved" : "1 earlier photos were saved"));
        component.Find("[role=alert]").TextContent.ShouldContain(storageRejection ? "Cleanup queued" : "Reload to check the current photo");
        component.FindAll("[data-kind=success]").ShouldBeEmpty();
        uploaded.ShouldBe(2);
    }
}
