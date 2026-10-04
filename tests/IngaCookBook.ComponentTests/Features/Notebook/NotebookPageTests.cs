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
public sealed partial class NotebookPageTests
{
    [Theory]
    [InlineData("drafts", "Vanilla")]
    [InlineData("tasting", "Horchata")]
    [InlineData("standards", "Caramel")]
    [InlineData("unknown", "Vanilla,Horchata,Caramel")]
    public async Task LibraryStatusFiltersSelectTheCorrespondingRecipes(string status, string expected)
    {
        ArgumentNullException.ThrowIfNull(expected);
        await using var context = new BunitContext();
        var draft = new RecipeDocument { Name = "Vanilla", Versions = [new()] };
        var untasted = new RecipeDocument
        {
            Name = "Horchata",
            Versions = [new() { IsLocked = true, Batches = [new(Guid.NewGuid(), DateTimeOffset.UtcNow, "", [])] }],
        };
        var standard = new RecipeVersion { IsLocked = true };
        var caramel = new RecipeDocument { Name = "Caramel", StandardVersionId = standard.Id, Versions = [standard] };
        Configure(context, draft, untasted, caramel);
        context.Services.GetRequiredService<NavigationManager>().NavigateTo($"/recipes?status={status}");

        var component = context.Render<Library>();

        component.FindAll(".recipe-card h2").Select(e => e.TextContent).ShouldBe(expected.Split(','));
        component.FindAll(".resume-card").ShouldBeEmpty();
    }

    [Theory]
    [InlineData("VANILLA", "Vanilla")]
    [InlineData("toasted RICE", "Horchata")]
    [InlineData("pistachio", "")]
    public async Task LibrarySearchMatchesNamesAndDescriptionsWithoutCaseSensitivity(string query, string expected)
    {
        ArgumentNullException.ThrowIfNull(expected);
        await using var context = new BunitContext();
        Configure(context, new() { Name = "Vanilla" }, new() { Name = "Horchata", Description = "Toasted rice and cinnamon" });
        context.Services.GetRequiredService<NavigationManager>().NavigateTo($"/recipes?q={Uri.EscapeDataString(query)}");

        var component = context.Render<Library>();

        component.FindAll(".recipe-card h2").Select(e => e.TextContent).ShouldBe(expected.Length == 0 ? [] : new[] { expected });
        if (expected.Length == 0) { component.Find(".empty-state").TextContent.ShouldContain("No matching recipes"); }
    }

    [Theory]
    [InlineData("explicit")]
    [InlineData("standard")]
    [InlineData("fallback")]
    public async Task LibraryCoverPrefersTheChosenPhotoThenStandardThenAnyVersion(string selection)
    {
        await using var context = new BunitContext();
        var first = new RecipeVersion { Photos = [Photo("First batch")] };
        var standard = new RecipeVersion { IsLocked = true, Photos = [Photo("Standard")] };
        var chosen = new RecipeVersion { Photos = [Photo("Another photo"), Photo("Chosen cover")] };
        var recipe = new RecipeDocument
        {
            Name = "Vanilla",
            Versions = [first, standard, chosen],
            StandardVersionId = selection is "explicit" or "standard" ? standard.Id : null,
            CoverPhotoId = string.Equals(selection, "explicit", StringComparison.Ordinal) ? chosen.Photos[1].Id : null,
        };
        Configure(context, recipe);

        var component = context.Render<Library>();

        var expectedVersion = selection switch { "explicit" => chosen, "standard" => standard, _ => first };
        var expectedPhoto = string.Equals(selection, "explicit", StringComparison.Ordinal) ? chosen.Photos[1] : expectedVersion.Photos[0];
        component.Find(".recipe-card-cover img").GetAttribute("src")
            .ShouldBe($"/api/notebook/recipes/{recipe.Id}/versions/{expectedVersion.Id}/photos/{expectedPhoto.Id}");
        component.Find(".recipe-card-cover img").GetAttribute("alt").ShouldBe("Vanilla");
    }

    [Fact]
    public async Task LibraryResumesNewestDraftButPrioritizesUntastedBatchesOnItsCard()
    {
        await using var context = new BunitContext();
        var older = new RecipeVersion { Number = 1, CreatedAt = DateTimeOffset.UtcNow.AddDays(-2) };
        var draft = new RecipeVersion { Number = 3, CreatedAt = DateTimeOffset.UtcNow };
        var untasted = new RecipeVersion { Number = 2, IsLocked = true, Batches = [new(Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(-1), "", [])] };
        var recipe = new RecipeDocument { Name = "Vanilla", Versions = [older, untasted, draft] };
        Configure(context, recipe);

        var component = context.Render<Library>();

        component.Find(".resume-card a").GetAttribute("href").ShouldBe($"/recipes/{recipe.Id}/versions/{draft.Id}/edit");
        component.Find(".recipe-card .action-link").GetAttribute("href").ShouldBe($"/recipes/{recipe.Id}/versions/{untasted.Id}/batches?mode=taste");
        component.Find(".recipe-card").TextContent.ShouldContain("3 versions · 1 batch");
    }

    [Fact]
    public async Task MissingWorkspaceOffersCreationBeforeRecipeSearch()
    {
        await using var context = new BunitContext();
        var service = Configure(context);
        service.GetWorkspaceAsync(Arg.Any<CancellationToken>()).Returns((WorkspaceView?)null);

        var component = context.Render<Library>();

        component.Find(".empty-state a").GetAttribute("href").ShouldBe("/workspace");
        component.FindAll("form[role=search]").ShouldBeEmpty();
        component.FindAll(".recipe-card").ShouldBeEmpty();
    }

    private static RecipePhoto Photo(string caption) => new(Guid.NewGuid(), caption, "image/png", DateTimeOffset.UtcNow);

    private static INotebookService Configure(BunitContext context, params RecipeDocument[] recipes)
    {
        context.Services.AddFluentUIComponents();
        context.ComponentFactories.AddStub<FluentProviders>();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var service = Substitute.For<INotebookService>();
        context.Services.AddSingleton(service);
        context.Renderer.SetRendererInfo(new RendererInfo("Static", false));
        service.GetRecipesAsync(Arg.Any<CancellationToken>()).Returns(recipes);
        service.GetWorkspaceAsync(Arg.Any<CancellationToken>()).Returns(new WorkspaceView(Guid.NewGuid(), "Kitchen", "USD"));
        foreach (var recipe in recipes) { service.GetRecipeAsync(recipe.Id, Arg.Any<CancellationToken>()).Returns(recipe); }
        return service;
    }
}
