namespace IngaCookBook.SharedKernel.Notebook;

/// <summary>Saves a draft or an explicitly explained correction.</summary>
public sealed record VersionRequest(Guid Revision, RecipeContent Content, string? CorrectionReason);
