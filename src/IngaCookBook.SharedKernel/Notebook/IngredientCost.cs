namespace IngaCookBook.SharedKernel.Notebook;

/// <summary>Known ingredient cost and the number of unpriced ingredient lines.</summary>
public sealed record IngredientCost(decimal KnownTotal, int MissingCount);
