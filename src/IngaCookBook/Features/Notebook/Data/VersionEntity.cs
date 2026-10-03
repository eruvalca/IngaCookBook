namespace IngaCookBook.Features.Notebook.Data;

internal sealed class VersionEntity
{
    public Guid Id { get; set; }
    public Guid RecipeId { get; set; }
    public Guid? ParentId { get; set; }
    public int Number { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public bool IsLocked { get; set; }
    public string Label { get; set; } = "";
    public string Notes { get; set; } = "";
    public Guid? TargetMetricId { get; set; }
    public string Hypothesis { get; set; } = "";
    public string RelatedChanges { get; set; } = "";
    public decimal? Yield { get; set; }
    public string YieldUnit { get; set; } = "g";
    public List<IngredientEntity> Ingredients { get; set; } = [];
    public List<StepEntity> Steps { get; set; } = [];
    public List<BatchEntity> Batches { get; set; } = [];
    public List<PhotoEntity> Photos { get; set; } = [];
    public List<CorrectionEntity> Corrections { get; set; } = [];
}
