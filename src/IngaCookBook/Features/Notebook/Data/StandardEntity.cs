namespace IngaCookBook.Features.Notebook.Data;

internal sealed class StandardEntity
{
    public Guid Id { get; set; }
    public Guid RecipeId { get; set; }
    public Guid VersionId { get; set; }
    public DateTimeOffset SelectedAt { get; set; }
}
