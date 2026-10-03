namespace IngaCookBook.SharedKernel.Notebook;

public sealed record EvaluationCorrection(Guid Id, DateTimeOffset CorrectedAt, string Reason, EvaluationSnapshot PreviousContent);
