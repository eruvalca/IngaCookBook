namespace IngaCookBook.SharedKernel.Notebook;

/// <summary>One observable formulation or preparation difference.</summary>
public sealed record RecipeDifference(string Area, string Label, string Before, string After);
