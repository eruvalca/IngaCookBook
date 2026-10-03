namespace IngaCookBook.SharedKernel.Notebook;

/// <summary>A dated tasting of a particular batch.</summary>
public sealed record BatchEvaluation(Guid Id, DateTimeOffset TastedAt, string Notes, string NextIdea, IReadOnlyList<MetricScore> Scores)
{
    public DateTimeOffset RecordedAt { get; init; } = DateTimeOffset.UtcNow;
}
