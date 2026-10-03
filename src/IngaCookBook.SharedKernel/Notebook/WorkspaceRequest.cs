namespace IngaCookBook.SharedKernel.Notebook;

/// <summary>Creates the user's initial workspace.</summary>
public sealed record WorkspaceRequest(string Name, string Currency);
