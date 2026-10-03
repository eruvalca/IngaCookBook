namespace IngaCookBook.SharedKernel.Notebook;

public sealed record BatchCorrection(Guid Id, DateTimeOffset CorrectedAt, string Reason, DateTimeOffset PreviousMadeAt, string PreviousNotes);
