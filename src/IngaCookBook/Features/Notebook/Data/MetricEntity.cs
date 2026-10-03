namespace IngaCookBook.Features.Notebook.Data;

internal sealed class MetricEntity
{
    public Guid Id { get; set; }
    public Guid RecipeId { get; set; }
    public int Position { get; set; }
    public string Name { get; set; } = "";
}
