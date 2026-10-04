using IngaCookBook.SharedKernel.Notebook;

namespace IngaCookBook.Features.Notebook.Services;

internal sealed partial class NotebookService
{
    public async Task<NotebookChange> CorrectBatchAsync(Guid recipeId, Guid versionId, Guid batchId,
        BatchCorrectionRequest request, CancellationToken cancellationToken)
    {
        var recipe = await GetRecipeAsync(recipeId, cancellationToken);
        var batch = recipe?.Versions.FirstOrDefault(v => v.Id == versionId)?.Batches.FirstOrDefault(b => b.Id == batchId);
        if (batch is null)
        {
            return new ChangeRejected("This batch could not be found.", 404);
        }
        if (request.Batch is null || string.IsNullOrWhiteSpace(request.Reason))
        {
            return new ChangeRejected("Explain what you are correcting and why.");
        }
        var input = request.Batch;
        if (input.MadeAt == default || input.Notes is null)
        {
            return new ChangeRejected("Choose when the batch was made and provide valid preparation notes.");
        }
        if (batch.Evaluations.Any(e => e.TastedAt < input.MadeAt))
        {
            return new ChangeRejected("The batch date cannot be later than an existing tasting. Correct the tasting first if its date is mistaken.");
        }
        return await UpdateAsync(recipeId, input.Revision, current => Replace(current, versionId, v => v with
        {
            Batches = v.Batches.Select(b => b.Id == batchId ? b with
            {
                MadeAt = input.MadeAt.ToUniversalTime(),
                Notes = input.Notes,
                Corrections = [.. b.Corrections, new BatchCorrection(Guid.NewGuid(), DateTimeOffset.UtcNow,
                    request.Reason.Trim(), b.MadeAt, b.Notes)],
            } : b).ToArray(),
        }), batchId, cancellationToken);
    }
}
