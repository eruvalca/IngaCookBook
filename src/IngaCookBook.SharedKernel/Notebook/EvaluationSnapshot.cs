namespace IngaCookBook.SharedKernel.Notebook;

public sealed record EvaluationSnapshot(DateTimeOffset TastedAt, string Notes, string NextIdea, IReadOnlyList<MetricScore> Scores);
