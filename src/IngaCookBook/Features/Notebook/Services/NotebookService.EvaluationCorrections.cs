using IngaCookBook.SharedKernel.Notebook;

namespace IngaCookBook.Features.Notebook.Services;

internal sealed partial class NotebookService
{
    public async Task<NotebookChange> CorrectEvaluationAsync(Guid recipeId, Guid versionId, Guid batchId, Guid evaluationId,
        EvaluationCorrectionRequest request, CancellationToken cancellationToken = default)
    {
        var recipe = await GetRecipeAsync(recipeId, cancellationToken);
        var batch = recipe?.Versions.FirstOrDefault(v => v.Id == versionId)?.Batches.FirstOrDefault(b => b.Id == batchId);
        if (recipe is null || batch is null || !batch.Evaluations.Any(e => e.Id == evaluationId))
        {
            return new ChangeRejected("This tasting could not be found.", 404);
        }
        if (request.Evaluation is null || string.IsNullOrWhiteSpace(request.Reason))
        {
            return new ChangeRejected("Explain what you are correcting and why.");
        }
        var input = request.Evaluation;
        var error = NotebookValidation.Evaluation(recipe, input);
        if (error is not null)
        {
            return new ChangeRejected(error);
        }
        if (input.TastedAt < batch.MadeAt)
        {
            return new ChangeRejected("A tasting cannot be earlier than the batch was made.");
        }
        return await UpdateAsync(recipeId, input.Revision, current => Replace(current, versionId, v => v with
        {
            Batches = v.Batches.Select(b => b.Id == batchId ? b with
            {
                Evaluations = b.Evaluations.Select(e => e.Id == evaluationId ? e with
                {
                    TastedAt = input.TastedAt.ToUniversalTime(),
                    Notes = input.Notes,
                    NextIdea = input.NextIdea,
                    Scores = input.Scores,
                    Corrections = [.. e.Corrections, new EvaluationCorrection(Guid.NewGuid(), DateTimeOffset.UtcNow,
                        request.Reason.Trim(), new(e.TastedAt, e.Notes, e.NextIdea, e.Scores))],
                } : e).ToArray(),
            } : b).ToArray(),
        }), evaluationId, cancellationToken);
    }
}
