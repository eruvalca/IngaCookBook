using System.Diagnostics.CodeAnalysis;
using IngaCookBook.Features.Notebook.Services;
using IngaCookBook.SharedKernel.Notebook;
using Shouldly;
using Xunit;

namespace IngaCookBook.UnitTests.Features.Notebook;

[SuppressMessage("Maintainability", "CA1515:Consider making public types internal", Justification = "xUnit requires public test classes for discovery.")]
public sealed class NotebookValidationTests
{
    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(200, 4000, true)]
    [InlineData(201, 0, false)]
    [InlineData(1, 4001, false)]
    public void RecipeTextRespectsPersistenceBoundaries(int nameLength, int descriptionLength, bool valid)
    {
        var error = NotebookValidation.Settings(new string('n', nameLength), new string('d', descriptionLength), []);
        if (valid) { error.ShouldBeNull(); }
        else { error.ShouldNotBeNull().ShouldContain("200 characters"); }
    }

    [Fact]
    public void MetricsRejectDuplicateNamesAndIdentitiesButAllowNoCriteria()
    {
        var texture = new EvaluationMetric(Guid.NewGuid(), "Texture");
        NotebookValidation.Settings("Vanilla", "", [texture, texture with { Name = "Flavor" }]).ShouldBe("Give each evaluation metric a unique name.");
        NotebookValidation.Settings("Vanilla", "", [texture, new(Guid.NewGuid(), " texture ")]).ShouldBe("Give each evaluation metric a unique name.");
        NotebookValidation.Settings("Vanilla", "", [texture, new(Guid.NewGuid(), "Flavor")]).ShouldBeNull();
        NotebookValidation.Settings("Vanilla", "", []).ShouldBeNull();
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void MetricNamesHaveAnExplicitLengthLimit(int length, bool valid)
    {
        var error = NotebookValidation.Settings("Vanilla", "", [new(Guid.NewGuid(), new string('m', length))]);
        if (valid) { error.ShouldBeNull(); }
        else { error.ShouldNotBeNull().ShouldContain("100 characters"); }
    }

    [Fact]
    public void IncompleteDraftCanBeSavedButCannotBeCookedOrSelectedAsStandard()
    {
        var draft = new RecipeContent();
        NotebookValidation.Content(draft, complete: false).ShouldBeNull();
        NotebookValidation.Content(draft, complete: true).ShouldNotBeNull().ShouldContain("add ingredients with quantities");
        var content = draft with
        {
            Ingredients = [new Ingredient { Name = "Cream", Quantity = 100 }],
            Steps = [new(Guid.NewGuid(), "Mix", "")],
        };
        NotebookValidation.Content(content, complete: true).ShouldBeNull();
        NotebookValidation.Content(content with { Ingredients = [content.Ingredients[0] with { Quantity = null }] }, complete: true)
            .ShouldNotBeNull().ShouldContain("add ingredients with quantities");
    }

    [Fact]
    public void MalformedDraftRowsAreRejectedBeforePersistence()
    {
        var ingredient = new Ingredient { Name = "Cream", Quantity = 100 };
        var step = new PreparationStep(Guid.NewGuid(), "Mix", "");
        NotebookValidation.Content(null, false).ShouldBe("Give this version a short label.");
        NotebookValidation.Content(new() { Label = " " }, false).ShouldBe("Give this version a short label.");
        NotebookValidation.Content(new() { Ingredients = null! }, false).ShouldBe("The recipe data is incomplete.");
        NotebookValidation.Content(new() { Ingredients = [ingredient with { Unit = "pinch" }] }, false)
            .ShouldBe("Check ingredient names and select supported units.");
        NotebookValidation.Content(new() { Ingredients = [ingredient, ingredient] }, false)
            .ShouldBe("Ingredient and step rows must be unique and contain valid text.");
        NotebookValidation.Content(new() { Steps = [step, step] }, false)
            .ShouldBe("Ingredient and step rows must be unique and contain valid text.");
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(0.0001, true)]
    [InlineData(1000000000, true)]
    [InlineData(1000000001, false)]
    public void YieldAndIngredientAmountsEnforceTheSamePositiveRange(decimal amount, bool valid)
    {
        var quantityError = NotebookValidation.Content(new() { Ingredients = [new Ingredient { Quantity = amount }] }, false);
        var yieldError = NotebookValidation.Content(new() { Yield = amount }, false);
        if (valid)
        {
            quantityError.ShouldBeNull();
            yieldError.ShouldBeNull();
        }
        else
        {
            quantityError.ShouldNotBeNull().ShouldContain("Quantities must be positive");
            yieldError.ShouldBe("Enter a positive yield and a supported yield unit.");
        }
    }

    [Fact]
    public void TastingRequiresAnObservationAndDateButNotANumericScore()
    {
        var metric = new EvaluationMetric(Guid.NewGuid(), "Texture");
        var recipe = new RecipeDocument { Metrics = [metric] };
        var request = new EvaluationRequest(recipe.Revision, DateTimeOffset.UtcNow, "", "", [new(metric.Id, null, "")]);
        NotebookValidation.Evaluation(recipe, request).ShouldBe("Add at least one score or observation before saving an evaluation.");
        NotebookValidation.Evaluation(recipe, request with { Scores = [new(metric.Id, null, "Smooth")] }).ShouldBeNull();
        NotebookValidation.Evaluation(recipe, request with { NextIdea = "Try less sugar" }).ShouldBeNull();
        NotebookValidation.Evaluation(recipe, request with { Notes = "Smooth", TastedAt = default }).ShouldBe("Choose a tasting date.");
        NotebookValidation.Evaluation(recipe, request with { Scores = [new(Guid.NewGuid(), 8, "")] })
            .ShouldNotBeNull().ShouldContain("recipe's current metrics");
        NotebookValidation.Evaluation(recipe, request with { Scores = [new(metric.Id, 8, ""), new(metric.Id, 9, "")] })
            .ShouldNotBeNull().ShouldContain("recipe's current metrics");
    }
}
