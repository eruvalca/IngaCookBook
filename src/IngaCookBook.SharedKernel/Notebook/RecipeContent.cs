namespace IngaCookBook.SharedKernel.Notebook;

/// <summary>The formulation and experiment plan captured by a recipe version.</summary>
public sealed record RecipeContent
{
    public string Label { get; init; } = "First attempt";
    public string Notes { get; init; } = "";
    public Guid? TargetMetricId { get; init; }
    public string Hypothesis { get; init; } = "";
    public string RelatedChanges { get; init; } = "";
    public decimal? Yield { get; init; }
    public string YieldUnit { get; init; } = "g";
    public IReadOnlyList<Ingredient> Ingredients { get; init; } = [];
    public IReadOnlyList<PreparationStep> Steps { get; init; } = [];
}
