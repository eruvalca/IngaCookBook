namespace IngaCookBook.Features.Notebook.Data;

internal sealed class EvaluationCorrectionEntity
{
    public Guid Id { get; set; }
    public Guid EvaluationId { get; set; }
    public DateTimeOffset CorrectedAt { get; set; }
    public string Reason { get; set; } = "";
    public string PreviousContent { get; set; } = "";
}
