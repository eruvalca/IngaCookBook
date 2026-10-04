using Bunit;
using IngaCookBook.SharedKernel.Notebook;
using IngaCookBook.UI.Features.Notebook.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace IngaCookBook.ComponentTests.Features.Notebook;

public sealed partial class NotebookPageTests
{
    [Fact]
    public async Task ComparisonHonorsSelectedHistoricalTastingsAndKeepsUnscoredQualitiesBlank()
    {
        await using var context = new BunitContext();
        var texture = new EvaluationMetric(Guid.NewGuid(), "Texture");
        var flavor = new EvaluationMetric(Guid.NewGuid(), "Flavor");
        var appearance = new EvaluationMetric(Guid.NewGuid(), "Appearance");
        var day = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
        var firstTasting = new BatchEvaluation(Guid.NewGuid(), day, "First day", "Age longer", [new(texture.Id, 4, "Icy"), new(flavor.Id, null, "Good vanilla")]);
        var latestTasting = new BatchEvaluation(Guid.NewGuid(), day.AddDays(1), "Second day", "", [new(texture.Id, 9, "Softer")]);
        var first = new RecipeVersion { Number = 1, Batches = [new(Guid.NewGuid(), day, "", [firstTasting, latestTasting])] };
        var improved = new RecipeVersion
        {
            Number = 2,
            ParentId = first.Id,
            Batches = [new(Guid.NewGuid(), day, "", [new(Guid.NewGuid(), day, "Second version", "", [new(texture.Id, 7, "Smoother")])])],
        };
        var recipe = new RecipeDocument { Metrics = [texture, flavor, appearance], Versions = [first, improved] };
        Configure(context, recipe);
        context.Services.GetRequiredService<NavigationManager>().NavigateTo($"/recipes/{recipe.Id}/compare?left={first.Id}&right={improved.Id}&le={firstTasting.Id}");

        var component = context.Render<CompareVersions>(p => p.Add(c => c.RecipeId, recipe.Id));

        var rows = component.FindAll(".quality-scores tbody tr");
        rows.Count.ShouldBe(2);
        rows[0].QuerySelectorAll("td").Select(e => e.TextContent.Trim()).ShouldBe(["4Icy", "7Smoother", "+3"]);
        rows[1].QuerySelectorAll("td").Select(e => e.TextContent.Trim()).ShouldBe(["—Good vanilla", "—", "—"]);
        component.Markup.ShouldContain("Appearance · not scored in either tasting");
        component.Find("#left-eval option[selected]").GetAttribute("value").ShouldBe(firstTasting.Id.ToString());
        component.FindAll(".score-chip").Single().TextContent.ShouldContain("4 → 7 (+3)");
        component.Markup.ShouldNotContain("Second day");
    }

    [Fact]
    public async Task ComparisonDefaultsToLatestVersionAndItsActualParent()
    {
        await using var context = new BunitContext();
        var first = new RecipeVersion { Number = 1, Content = new() { Label = "Starting recipe" } };
        var sibling = new RecipeVersion { Number = 2, ParentId = first.Id, Content = new() { Label = "More sugar" } };
        var latest = new RecipeVersion { Number = 3, ParentId = first.Id, Content = new() { Label = "Longer aging" } };
        var recipe = new RecipeDocument { Versions = [first, sibling, latest] };
        Configure(context, recipe);
        context.Services.GetRequiredService<NavigationManager>().NavigateTo($"/recipes/{recipe.Id}/compare?right={Guid.NewGuid()}");

        var component = context.Render<CompareVersions>(p => p.Add(c => c.RecipeId, recipe.Id));

        component.Find("#left-version option[selected]").GetAttribute("value").ShouldBe(first.Id.ToString());
        component.Find("#right-version option[selected]").GetAttribute("value").ShouldBe(latest.Id.ToString());
        component.Find(".comparison-overview h2").TextContent.ShouldBe("Starting recipe → Longer aging");
        component.Markup.ShouldContain("V1: no tasting → V3: no tasting");
        component.Markup.ShouldContain("No ingredient, preparation, or yield differences.");
    }
}
