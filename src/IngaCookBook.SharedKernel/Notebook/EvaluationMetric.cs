namespace IngaCookBook.SharedKernel.Notebook;

/// <summary>A recipe-wide quality criterion, scored from one to ten.</summary>
public sealed record EvaluationMetric(Guid Id, string Name);
