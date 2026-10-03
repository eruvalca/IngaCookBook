using Bunit;
using IngaCookBook.SharedKernel.Notebook;
using IngaCookBook.UI.Features.Notebook.Components;
using IngaCookBook.UI.Features.Notebook.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components;
using NSubstitute;
using Shouldly;
using Xunit;

namespace IngaCookBook.ComponentTests.Features.Notebook;

public sealed partial class NotebookFormSaveTests
{
    [Fact]
    public async Task HistoryTimestampHasAnExplicitUtcFallbackAndAnUnambiguousInstant()
    {
        await using var context = new BunitContext();
        var component = context.Render<LocalDate>(p => p.Add(c => c.Value, new DateTimeOffset(2001, 10, 2, 19, 30, 0, TimeSpan.FromHours(-5))));
        var time = component.Find("time[data-local-date]");
        time.TextContent.ShouldBe("Oct 3, 2001 UTC");
        time.GetAttribute("datetime").ShouldBe("2001-10-02T19:30:00.0000000-05:00");
    }

    [Fact]
    public async Task PreservedVersionShowsItsCorrectionDiffWithoutExperimentWarning()
    {
        await using var context = new BunitContext();
        var (service, recipe) = Configure(context);
        var parent = recipe.Versions[0];
        var content = parent.Content with { Ingredients = [parent.Content.Ingredients[0] with { Quantity = 120 }], Hypothesis = "Will more cream help?" };
        var version = new RecipeVersion { Number = 2, ParentId = parent.Id, IsLocked = true, Content = content };
        recipe = recipe with { Versions = [parent, version] };
        service.GetRecipeAsync(recipe.Id, Arg.Any<CancellationToken>()).Returns(recipe);
        var component = context.Render<VersionEditor>(p => p.Add(c => c.RecipeId, recipe.Id).Add(c => c.VersionId, version.Id));
        var yield = component.FindComponents<FluentNumberInput<decimal?>>().Single(c => string.Equals(c.Instance.Label, "Yield amount", StringComparison.Ordinal));
        await yield.Find("fluent-number-input, fluent-text-input").ChangeAsync(new ChangeEventArgs { Value = "120" });
        component.Find(".experiment-panel").TextContent.ShouldContain("Changes in this correction");
        component.FindAll(".change-summary li").Count.ShouldBe(1);
        component.Find(".change-summary").TextContent.ShouldContain("Yield");
        component.Markup.ShouldNotContain("Several things changed");
        component.Markup.ShouldNotContain("How do these changes belong together?");
        component.FindComponents<FluentTextArea>().Single(c => string.Equals(c.Instance.Label, "What are you testing?", StringComparison.Ordinal))
            .Instance.Value.ShouldBe("Will more cream help?");
    }

    [Theory]
    [InlineData("", null)]
    [InlineData("1", 1)]
    [InlineData("10", 10)]
    public async Task JournalSavesBlankAndBoundaryScoresWithoutChangingTheirMeaning(string input, int? expected)
    {
        await using var context = new BunitContext();
        var (service, recipe) = Configure(context);
        var component = context.Render<BatchJournal>(p => p.Add(c => c.RecipeId, recipe.Id).Add(c => c.VersionId, recipe.Versions[0].Id));
        var score = component.FindComponents<FluentTextInput>().Single(c => string.Equals(c.Instance.Label, "Texture", StringComparison.Ordinal));
        await score.Find("fluent-text-input").ChangeAsync(new ChangeEventArgs { Value = input });
        await component.FindComponents<FluentTextArea>().Single(c => string.Equals(c.Instance.Label, "Overall observations", StringComparison.Ordinal))
            .Find("fluent-textarea").ChangeAsync(new ChangeEventArgs { Value = "Tasting notes" });
        service.EvaluateAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<EvaluationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ChangeSaved(Guid.NewGuid()));
        await component.FindAll("fluent-button").Single(b => b.TextContent.Contains("Save evaluation", StringComparison.Ordinal)).ClickAsync();
        await service.Received(1).EvaluateAsync(recipe.Id, recipe.Versions[0].Id, recipe.Versions[0].Batches[0].Id,
            Arg.Is<EvaluationRequest>(r => r.Scores[0].Score == expected && r.Notes == "Tasting notes"), Arg.Any<CancellationToken>());
        score.Instance.Value.ShouldBeEmpty();
        component.FindAll("[role=alert]").ShouldBeEmpty();
    }

    [Theory]
    [InlineData("11")]
    [InlineData("0")]
    [InlineData("1.5")]
    [InlineData("oops")]
    public async Task JournalRetainsInvalidScoresAndDoesNotSaveThem(string input)
    {
        await using var context = new BunitContext();
        var (service, recipe) = Configure(context);
        var component = context.Render<BatchJournal>(p => p.Add(c => c.RecipeId, recipe.Id).Add(c => c.VersionId, recipe.Versions[0].Id));
        var score = component.FindComponents<FluentTextInput>().Single(c => string.Equals(c.Instance.Label, "Texture", StringComparison.Ordinal));
        await score.Find("fluent-text-input").ChangeAsync(new ChangeEventArgs { Value = input });
        await component.FindAll("fluent-button").Single(b => b.TextContent.Contains("Save evaluation", StringComparison.Ordinal)).ClickAsync();
        score.Instance.Value.ShouldBe(input);
        component.Markup.ShouldContain("Enter a whole number from 1 to 10");
        component.Find("[role=alert]").TextContent.ShouldContain("Correct the highlighted scores");
        await service.DidNotReceive().EvaluateAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<EvaluationRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task JournalCorrectionUsesOriginalIdAndRetainsEditsAfterConflict()
    {
        await using var context = new BunitContext();
        var (service, recipe) = Configure(context);
        var version = recipe.Versions[0];
        var batch = version.Batches[0];
        var evaluation = new BatchEvaluation(Guid.NewGuid(), batch.MadeAt, "Original", "Try aging", [new(recipe.Metrics[0].Id, 10, "Original note")]);
        recipe = recipe with { Versions = [version with { Batches = [batch with { Evaluations = [evaluation] }] }] };
        service.GetRecipeAsync(recipe.Id, Arg.Any<CancellationToken>()).Returns(recipe);
        service.CorrectEvaluationAsync(recipe.Id, version.Id, batch.Id, evaluation.Id, Arg.Any<EvaluationCorrectionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ChangeRejected("Another tab saved first", 409));
        var component = context.Render<BatchJournal>(p => p.Add(c => c.RecipeId, recipe.Id).Add(c => c.VersionId, version.Id));
        await component.FindAll("fluent-button").Single(b => b.TextContent.Contains("Correct tasting", StringComparison.Ordinal)).ClickAsync();
        var score = component.FindComponents<FluentTextInput>().Single(c => string.Equals(c.Instance.Label, "Texture", StringComparison.Ordinal));
        score.Instance.Value.ShouldBe("10");
        await score.Find("fluent-text-input").ChangeAsync(new ChangeEventArgs { Value = "7" });
        var reason = component.FindComponents<FluentTextArea>().Single(c => string.Equals(c.Instance.Label, "What are you correcting, and why?", StringComparison.Ordinal));
        await reason.Find("fluent-textarea").ChangeAsync(new ChangeEventArgs { Value = "Mistyped score" });
        await component.FindAll("fluent-button").Single(b => b.TextContent.Contains("Save tasting correction", StringComparison.Ordinal)).ClickAsync();
        await service.Received(1).CorrectEvaluationAsync(recipe.Id, version.Id, batch.Id, evaluation.Id,
            Arg.Is<EvaluationCorrectionRequest>(r => r.Reason == "Mistyped score" && r.Evaluation.Revision == recipe.Revision
                && r.Evaluation.Scores[0].Score == 7 && r.Evaluation.Scores[0].Notes == "Original note" && r.Evaluation.Notes == "Original"), Arg.Any<CancellationToken>());
        component.Markup.ShouldContain("Another tab saved first");
        score.Instance.Value.ShouldBe("7");
        reason.Instance.Value.ShouldBe("Mistyped score");
    }
}
