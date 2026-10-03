namespace IngaCookBook.SharedKernel.Notebook;

/// <summary>One actual preparation of a version, with repeat tastings.</summary>
public sealed record RecipeBatch(Guid Id, DateTimeOffset MadeAt, string Notes, IReadOnlyList<BatchEvaluation> Evaluations)
{
    public IReadOnlyList<BatchCorrection> Corrections { get; init; } = [];
}
