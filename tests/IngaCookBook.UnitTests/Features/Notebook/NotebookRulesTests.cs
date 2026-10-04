using System.Diagnostics.CodeAnalysis;
using IngaCookBook.Features.Notebook.Services;
using IngaCookBook.SharedKernel.Notebook;
using Shouldly;
using Xunit;

namespace IngaCookBook.UnitTests.Features.Notebook;

[SuppressMessage("Maintainability", "CA1515:Consider making public types internal", Justification = "xUnit requires public test classes for discovery.")]
public sealed class NotebookRulesTests
{
    [Fact]
    public void NestedRecipeCostsRoundOnlyForDisplay()
    {
        var sauce = new RecipeContent
        {
            Yield = 100,
            Ingredients = [new Ingredient { Quantity = 100, PurchaseQuantity = 200, PurchasePrice = 8.25m }],
        };
        var batch = new RecipeContent
        {
            Yield = 400,
            Ingredients = [new Ingredient { Quantity = 400, LinkedContent = sauce }],
        };
        RecipeCosting.Calculate(sauce).KnownTotal.ShouldBe(4.125m);
        RecipeCosting.Calculate(batch).KnownTotal.ShouldBe(16.50m);
        RecipeCosting.CalculateIngredient(new Ingredient { Quantity = 100, LinkedContent = batch }).ShouldBe(4.125m);
        RecipeCosting.Calculate(batch).MissingCount.ShouldBe(0);
    }

    [Theory]
    [InlineData("g", 250, "kg", 1, 8, 2)]
    [InlineData("oz", 4, "lb", 1, 16, 4)]
    [InlineData("tbsp (US)", 4, "cup (US)", 1, 8, 2)]
    [InlineData("count", 3, "count", 12, 6, 1.5)]
    [InlineData("mL", 250, "L", 1, 8, 2)]
    [InlineData("tsp (US)", 6, "fl oz (US)", 1, 3, 3)]
    public void CompatiblePurchaseUnitsProduceProportionalCost(string unit, decimal used, string purchasedUnit, decimal purchased, decimal price, decimal expected)
    {
        var ingredient = new Ingredient { Name = "Ingredient", Quantity = used, Unit = unit, PurchaseQuantity = purchased, PurchaseUnit = purchasedUnit, PurchasePrice = price };
        RecipeCosting.CalculateIngredient(ingredient).ShouldBe(expected);
    }

    [Theory]
    [InlineData("g", "cup (US)")]
    [InlineData("oz", "fl oz (US)")]
    [InlineData("count", "g")]
    [InlineData("pinch", "pinch")]
    public void CostNeverGuessesAcrossMeasurementFamilies(string usedUnit, string purchasedUnit)
    {
        var ingredient = new Ingredient { Quantity = 10, Unit = usedUnit, PurchaseQuantity = 100, PurchaseUnit = purchasedUnit, PurchasePrice = 10 };
        RecipeCosting.CalculateIngredient(ingredient).ShouldBeNull();
        RecipeCosting.Calculate(new() { Ingredients = [ingredient] }).MissingCount.ShouldBe(1);
    }

    [Fact]
    public void MissingPricesRemainUnknownAndNestedRecipesUseTheirSavedYield()
    {
        var subrecipe = new RecipeContent { Yield = 100, YieldUnit = "g", Ingredients = [new Ingredient { Quantity = 100, PurchaseQuantity = 100, PurchasePrice = 8 }] };
        var ingredient = new Ingredient { Quantity = 25, LinkedContent = subrecipe };
        RecipeCosting.CalculateIngredient(ingredient).ShouldBe(2m);
        RecipeCosting.CalculateIngredient(ingredient with { LinkedContent = subrecipe with { Yield = null } }).ShouldBeNull();
        RecipeCosting.CalculateIngredient(ingredient with { LinkedContent = subrecipe with { Ingredients = [new Ingredient { Quantity = 100 }] } }).ShouldBeNull();
        RecipeCosting.Calculate(new() { Ingredients = [ingredient, new Ingredient { Quantity = 1 }] }).ShouldBe(new IngredientCost(2, 1));
    }

    [Fact]
    public void ComparisonDetectsIngredientChangesAndReorderedPreparationWithoutCountingPrices()
    {
        var cream = new Ingredient { Name = "Cream", Quantity = 100, PurchasePrice = 10 };
        var sugar = new Ingredient { Name = "Sugar", Quantity = 20 };
        var step1 = new PreparationStep(Guid.NewGuid(), "Mix", "");
        var step2 = new PreparationStep(Guid.NewGuid(), "Churn", "18 minutes");
        var before = new RecipeContent { Ingredients = [cream, sugar], Steps = [step1, step2] };
        var after = before with { Ingredients = [cream with { Quantity = 120 }, sugar with { PurchasePrice = 4 }], Steps = [step2, step1] };
        var changes = RecipeComparison.Compare(before, after);
        changes.Count.ShouldBe(3);
        changes.Count(c => string.Equals(c.Area, "Ingredient", StringComparison.Ordinal)).ShouldBe(1);
        changes.Single(c => string.Equals(c.Area, "Ingredient", StringComparison.Ordinal)).After.ShouldContain("120");
        RecipeComparison.Compare(before, before with { Ingredients = [cream] }).Single().After.ShouldBe("—");
        RecipeComparison.Compare(before, before).ShouldBeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    [InlineData(-1)]
    public void EvaluationRejectsOutOfRangeScores(int value)
    {
        var metric = new EvaluationMetric(Guid.NewGuid(), "Texture");
        var recipe = new RecipeDocument { Metrics = [metric] };
        NotebookValidation.Evaluation(recipe, new(Guid.NewGuid(), DateTimeOffset.UtcNow, "", "", [new(metric.Id, value, "")])).ShouldNotBeNull();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(null)]
    public void EvaluationAllowsBoundariesAndUnscoredObservations(int? value)
    {
        var metric = new EvaluationMetric(Guid.NewGuid(), "Texture");
        var recipe = new RecipeDocument { Metrics = [metric] };
        NotebookValidation.Evaluation(recipe, new(Guid.NewGuid(), DateTimeOffset.UtcNow, "Tasting notes", "", [new(metric.Id, value, "")])).ShouldBeNull();
    }

    [Fact]
    public void DraftCanBeIncompleteButBatchRequiresIngredientsAndInstructions()
    {
        NotebookValidation.Content(new(), complete: false).ShouldBeNull();
        NotebookValidation.Content(new(), complete: true).ShouldNotBeNull();
        NotebookValidation.Content(new() { Ingredients = [new Ingredient { Quantity = -1 }] }, complete: false).ShouldNotBeNull();
        NotebookValidation.Settings("Vanilla", "", [new(Guid.NewGuid(), "Texture"), new(Guid.NewGuid(), "texture")]).ShouldNotBeNull();
    }

    [Fact]
    public void LatestEvaluationIsExplicitAndSameDayTastingsChooseLastRecorded()
    {
        var date = new DateTimeOffset(2026, 10, 2, 0, 0, 0, TimeSpan.Zero);
        var first = new BatchEvaluation(Guid.NewGuid(), date, "First", "", []) { RecordedAt = date.AddHours(1) };
        var second = new BatchEvaluation(Guid.NewGuid(), date, "Second", "", []) { RecordedAt = date.AddHours(2) };
        var version = new RecipeVersion { Batches = [new(Guid.NewGuid(), date, "", [first, second])] };
        RecipeComparison.LatestEvaluation(version).ShouldBe(second);
        RecipeComparison.LatestEvaluation(version with { Batches = [new(Guid.NewGuid(), date, "", [second]), new(Guid.NewGuid(), date, "", [first])] }).ShouldBe(second);
        RecipeComparison.LatestEvaluation(new()).ShouldBeNull();
    }

    [Theory]
    [InlineData("not an image")]
    [InlineData("<svg onload=alert(1)>")]
    public void PhotoSignatureRejectsTextAndActiveContent(string content) =>
        PhotoFormat.Detect(System.Text.Encoding.UTF8.GetBytes(content)).ShouldBeNull();

    [Fact]
    public void PhotoSignatureRecognizesOnlyTheSupportedHeaders()
    {
        PhotoFormat.Detect([137, 80, 78, 71, 13, 10, 26, 10]).ShouldBe("image/png");
        PhotoFormat.Detect([255, 216, 255]).ShouldBe("image/jpeg");
        PhotoFormat.Detect("RIFF0000WEBP"u8).ShouldBe("image/webp");
        PhotoFormat.Detect("RIFF0000WAVE"u8).ShouldBeNull();
        PhotoFormat.Detect([137, 80, 78, 71, 13, 10, 26]).ShouldBeNull();
    }
}
