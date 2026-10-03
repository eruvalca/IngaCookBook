namespace IngaCookBook.Features.Notebook.Data;

internal sealed class PhotoEntity
{
    public Guid Id { get; set; }
    public Guid VersionId { get; set; }
    public string Caption { get; set; } = "";
    public string ContentType { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}
