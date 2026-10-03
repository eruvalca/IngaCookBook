namespace IngaCookBook.SharedKernel.Notebook;

/// <summary>An optional quality score and its written context.</summary>
public sealed record MetricScore(Guid MetricId, int? Score, string Notes);
