namespace IngaCookBook.SharedKernel.Notebook;

/// <summary>A recipe's complete development notebook, with an optimistic concurrency revision.</summary>
public sealed record RecipeDocument
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid Revision { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public IReadOnlyList<EvaluationMetric> Metrics { get; init; } = [];
    public IReadOnlyList<RecipeVersion> Versions { get; init; } = [];
    public Guid? StandardVersionId { get; init; }
    public IReadOnlyList<StandardSelection> Standards { get; init; } = [];
    public Guid? OriginRecipeId { get; init; }
    public Guid? OriginVersionId { get; init; }
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;
}
