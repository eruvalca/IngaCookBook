namespace IngaCookBook.Features.Notebook.Data;

internal sealed class BatchEntity
{
    public Guid Id { get; set; }
    public Guid VersionId { get; set; }
    public DateTimeOffset MadeAt { get; set; }
    public string Notes { get; set; } = "";
    public List<EvaluationEntity> Evaluations { get; set; } = [];
}
