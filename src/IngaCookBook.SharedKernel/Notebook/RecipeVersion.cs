namespace IngaCookBook.SharedKernel.Notebook;

/// <summary>A formulation, its parent and all its actual batches.</summary>
public sealed record RecipeVersion
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public int Number { get; init; }
    public Guid? ParentId { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public bool IsLocked { get; init; }
    public RecipeContent Content { get; init; } = new();
    public IReadOnlyList<RecipeBatch> Batches { get; init; } = [];
    public IReadOnlyList<RecipePhoto> Photos { get; init; } = [];
    public IReadOnlyList<VersionCorrection> Corrections { get; init; } = [];
}
