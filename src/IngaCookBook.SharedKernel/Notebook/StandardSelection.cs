namespace IngaCookBook.SharedKernel.Notebook;

/// <summary>The historical selection of a recipe's standard.</summary>
public sealed record StandardSelection(Guid VersionId, DateTimeOffset SelectedAt);
