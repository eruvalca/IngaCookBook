namespace IngaCookBook.SharedKernel.Notebook;

/// <summary>The signed-in owner's private workspace.</summary>
public sealed record WorkspaceView(Guid Id, string Name, string Currency);
