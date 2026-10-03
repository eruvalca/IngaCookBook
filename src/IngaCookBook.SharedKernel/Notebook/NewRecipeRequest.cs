namespace IngaCookBook.SharedKernel.Notebook;

/// <summary>Creates a recipe and its first draft.</summary>
public sealed record NewRecipeRequest(string Name, string Description, IReadOnlyList<string> Metrics);
