namespace IngaCookBook.SharedKernel.Notebook;

/// <summary>Records an actual preparation of a version.</summary>
public sealed record BatchRequest(Guid Revision, DateTimeOffset MadeAt, string Notes);
