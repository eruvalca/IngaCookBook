namespace IngaCookBook.Features.Notebook.Data;

internal sealed class WorkspaceEntity
{
    public Guid Id { get; set; }
    public string OwnerId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Currency { get; set; } = "USD";
}
