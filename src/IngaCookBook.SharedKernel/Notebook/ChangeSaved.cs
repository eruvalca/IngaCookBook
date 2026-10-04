namespace IngaCookBook.SharedKernel.Notebook;

/// <summary>A persisted change, identifying the created, modified, or deleted item.</summary>
public sealed record ChangeSaved(Guid Id) : NotebookChange;
