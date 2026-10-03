namespace IngaCookBook.Features.Notebook.Data;

internal sealed class ScoreEntity
{
    public Guid Id { get; set; }
    public Guid EvaluationId { get; set; }
    public Guid MetricId { get; set; }
    public int? Score { get; set; }
    public string Notes { get; set; } = "";
}
