using IngaCookBook.SharedKernel.Notebook;

namespace IngaCookBook.Features.Notebook.Services;

internal sealed partial class NotebookService
{
    public async Task<NotebookChange> SaveVersionAsync(Guid recipeId, Guid versionId, VersionRequest request, CancellationToken cancellationToken)
    {
        var recipe = await GetRecipeAsync(recipeId, cancellationToken);
        var version = recipe?.Versions.FirstOrDefault(v => v.Id == versionId);
        if (recipe is null || version is null)
        {
            return new ChangeRejected("This version could not be found.", 404);
        }
        var error = NotebookValidation.Content(request.Content, version.IsLocked);
        if (error is not null)
        {
            return new ChangeRejected(error);
        }
        if (version.IsLocked && string.IsNullOrWhiteSpace(request.CorrectionReason))
        {
            return new ChangeRejected("This version is preserved. Try a variation, or explain the correction.");
        }
        if (request.Content.TargetMetricId is { } metricId && !recipe.Metrics.Any(m => m.Id == metricId))
        {
            return new ChangeRejected("Choose one of this recipe's current evaluation metrics.");
        }
        var parent = recipe.Versions.FirstOrDefault(v => v.Id == version.ParentId);
        if (!version.IsLocked && parent is not null && RecipeComparison.Compare(parent.Content, request.Content).Count > 1 &&
            string.IsNullOrWhiteSpace(request.Content.RelatedChanges))
        {
            return new ChangeRejected("Several things changed. Explain how they belong to one experiment, or try separate variations.");
        }
        var linked = await ResolveIngredientsAsync(request.Content, version.Content, cancellationToken);
        if (linked is null)
        {
            return new ChangeRejected("Linked recipes must be preserved versions from your workspace. Choose a standard or a version already used for a batch.");
        }
        var content = request.Content with { Ingredients = linked };
        return await UpdateAsync(recipeId, request.Revision, current => Replace(current, versionId, v => v with
        {
            Content = content,
            Corrections = v.IsLocked
                ? [.. v.Corrections, new VersionCorrection(DateTimeOffset.UtcNow, request.CorrectionReason!.Trim(), v.Content)]
                : v.Corrections,
        }), versionId, cancellationToken);
    }

    private async Task<IReadOnlyList<Ingredient>?> ResolveIngredientsAsync(RecipeContent content, RecipeContent previous, CancellationToken cancellationToken)
    {
        var ingredients = new List<Ingredient>();
        foreach (var ingredient in content.Ingredients)
        {
            if (ingredient.RecipeId is null && ingredient.VersionId is null)
            {
                ingredients.Add(ingredient with { LinkedContent = null });
                continue;
            }
            var old = previous.Ingredients.FirstOrDefault(i => i.Id == ingredient.Id && i.RecipeId == ingredient.RecipeId &&
                i.VersionId == ingredient.VersionId && i.LinkedContent is not null);
            if (old is not null)
            {
                ingredients.Add(ingredient with { LinkedContent = old.LinkedContent });
                continue;
            }
            var nested = await GetRecipeAsync(ingredient.RecipeId ?? Guid.Empty, cancellationToken);
            var version = nested?.Versions.FirstOrDefault(v => v.Id == ingredient.VersionId && v.IsLocked);
            if (version is null || Depth(version.Content) >= 8)
            {
                return null;
            }
            ingredients.Add(ingredient with { LinkedContent = version.Content with { Label = $"{nested!.Name} · V{version.Number}: {version.Content.Label}" } });
        }
        return ingredients;
    }

    private static int Depth(RecipeContent content) => 1 + content.Ingredients
        .Where(i => i.LinkedContent is not null).Select(i => Depth(i.LinkedContent!)).DefaultIfEmpty(0).Max();

    public async Task<NotebookChange> MakeBatchAsync(Guid recipeId, Guid versionId, BatchRequest request, CancellationToken cancellationToken)
    {
        var recipe = await GetRecipeAsync(recipeId, cancellationToken);
        var version = recipe?.Versions.FirstOrDefault(v => v.Id == versionId);
        if (version is null)
        {
            return new ChangeRejected("This version could not be found.", 404);
        }
        var error = NotebookValidation.Content(version.Content, complete: true);
        if (error is not null)
        {
            return new ChangeRejected(error);
        }
        if (request.MadeAt == default || request.Notes is null)
        {
            return new ChangeRejected("Choose when the batch was made.");
        }
        var batch = new RecipeBatch(Guid.NewGuid(), request.MadeAt.ToUniversalTime(), request.Notes, []);
        return await UpdateAsync(recipeId, request.Revision, current => Replace(current, versionId, v => v with
        {
            IsLocked = true,
            Batches = [.. v.Batches, batch],
        }), batch.Id, cancellationToken);
    }

    public async Task<NotebookChange> EvaluateAsync(Guid recipeId, Guid versionId, Guid batchId, EvaluationRequest request, CancellationToken cancellationToken)
    {
        var recipe = await GetRecipeAsync(recipeId, cancellationToken);
        var batch = recipe?.Versions.FirstOrDefault(v => v.Id == versionId)?.Batches.FirstOrDefault(b => b.Id == batchId);
        if (recipe is null || batch is null)
        {
            return new ChangeRejected("This batch could not be found.", 404);
        }
        var error = NotebookValidation.Evaluation(recipe, request);
        if (error is not null)
        {
            return new ChangeRejected(error);
        }
        if (request.TastedAt < batch.MadeAt)
        {
            return new ChangeRejected("A tasting cannot be earlier than the batch was made.");
        }
        var evaluation = new BatchEvaluation(Guid.NewGuid(), request.TastedAt.ToUniversalTime(), request.Notes, request.NextIdea, request.Scores);
        return await UpdateAsync(recipeId, request.Revision, current => Replace(current, versionId, v => v with
        {
            Batches = v.Batches.Select(b => b.Id == batchId ? b with { Evaluations = [.. b.Evaluations, evaluation] } : b).ToArray(),
        }), evaluation.Id, cancellationToken);
    }
}
