namespace IngaCookBook.SharedKernel.Notebook;

public sealed record BatchCorrectionRequest(BatchRequest Batch, string Reason);
