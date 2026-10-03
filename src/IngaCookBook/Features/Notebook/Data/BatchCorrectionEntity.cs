namespace IngaCookBook.Features.Notebook.Data;

internal sealed class BatchCorrectionEntity
{
    public Guid Id { get; set; }
    public Guid BatchId { get; set; }
    public DateTimeOffset CorrectedAt { get; set; }
    public string Reason { get; set; } = "";
    public DateTimeOffset PreviousMadeAt { get; set; }
    public string PreviousNotes { get; set; } = "";
}
