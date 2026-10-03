namespace IngaCookBook.SharedKernel.Notebook;

/// <summary>Records a tasting and its recipe-specific ratings.</summary>
public sealed record EvaluationRequest(Guid Revision, DateTimeOffset TastedAt, string Notes, string NextIdea, IReadOnlyList<MetricScore> Scores);
