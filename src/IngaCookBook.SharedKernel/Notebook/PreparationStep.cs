namespace IngaCookBook.SharedKernel.Notebook;

/// <summary>An ordered preparation instruction with a stable identity for comparison.</summary>
public sealed record PreparationStep(Guid Id, string Instruction, string Notes);
