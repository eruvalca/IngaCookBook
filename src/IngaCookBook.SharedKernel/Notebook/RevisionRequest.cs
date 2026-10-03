namespace IngaCookBook.SharedKernel.Notebook;

/// <summary>Identifies the recipe revision on which a change is based.</summary>
public sealed record RevisionRequest(Guid Revision);
