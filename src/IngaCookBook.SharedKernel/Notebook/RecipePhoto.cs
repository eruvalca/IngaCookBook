namespace IngaCookBook.SharedKernel.Notebook;

/// <summary>Private photo metadata; storage credentials and paths never leave the server.</summary>
public sealed record RecipePhoto(Guid Id, string Caption, string ContentType, DateTimeOffset CreatedAt);
