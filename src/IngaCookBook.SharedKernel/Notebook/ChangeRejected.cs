namespace IngaCookBook.SharedKernel.Notebook;

/// <summary>An anticipated validation, missing-resource or concurrency failure.</summary>
public sealed record ChangeRejected(string Message, int Status = 400) : NotebookChange;
