namespace IngaCookBook.SharedKernel.Notebook;

/// <summary>An explicit correction retaining the previous formulation.</summary>
public sealed record VersionCorrection(DateTimeOffset CorrectedAt, string Reason, RecipeContent PreviousContent);
