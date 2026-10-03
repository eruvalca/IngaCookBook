namespace IngaCookBook.SharedKernel.Notebook;

/// <summary>A persisted change, identifying the created or modified item.</summary>
public sealed record ChangeSaved(Guid Id) : NotebookChange;
