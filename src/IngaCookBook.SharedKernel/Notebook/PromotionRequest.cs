namespace IngaCookBook.SharedKernel.Notebook;

/// <summary>Starts an independent recipe from a selected version.</summary>
public sealed record PromotionRequest(Guid Revision, string Name);
