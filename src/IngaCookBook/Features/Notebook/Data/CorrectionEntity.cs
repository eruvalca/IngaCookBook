namespace IngaCookBook.Features.Notebook.Data;

internal sealed class CorrectionEntity
{
    public Guid Id { get; set; }
    public Guid VersionId { get; set; }
    public DateTimeOffset CorrectedAt { get; set; }
    public string Reason { get; set; } = "";
    public string PreviousContent { get; set; } = "";
}
