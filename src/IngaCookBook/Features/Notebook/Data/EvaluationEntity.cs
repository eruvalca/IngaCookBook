namespace IngaCookBook.Features.Notebook.Data;

internal sealed class EvaluationEntity
{
    public Guid Id { get; set; }
    public Guid BatchId { get; set; }
    public DateTimeOffset TastedAt { get; set; }
    public DateTimeOffset RecordedAt { get; set; } = DateTimeOffset.UtcNow;
    public string Notes { get; set; } = "";
    public string NextIdea { get; set; } = "";
    public List<ScoreEntity> Scores { get; set; } = [];
}
