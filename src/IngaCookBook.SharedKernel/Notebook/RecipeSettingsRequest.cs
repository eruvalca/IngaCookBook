namespace IngaCookBook.SharedKernel.Notebook;

/// <summary>Updates recipe details and the criteria shared across all its evaluations.</summary>
public sealed record RecipeSettingsRequest(Guid Revision, string Name, string Description, IReadOnlyList<EvaluationMetric> Metrics);
