using IngaCookBook.Features.Notebook.Data;
using IngaCookBook.SharedKernel.Notebook;
using Microsoft.EntityFrameworkCore;

namespace IngaCookBook.Features.Notebook.Services;

internal sealed partial class NotebookService
{
    public async Task<NotebookChange> DeleteDraftAsync(Guid recipeId, Guid versionId, RevisionRequest request, CancellationToken cancellationToken = default)
    {
        var workspace = await GetWorkspaceAsync(cancellationToken);
        if (workspace is null)
        {
            return new ChangeRejected("This draft could not be found.", 404);
        }
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var recipe = await RecipeMapping.Complete(db.Set<RecipeEntity>()
            .Where(r => r.Id == recipeId && r.WorkspaceId == workspace.Id)).SingleOrDefaultAsync(cancellationToken);
        var version = recipe?.Versions.FirstOrDefault(v => v.Id == versionId);
        if (recipe is null || version is null)
        {
            return new ChangeRejected("This draft could not be found.", 404);
        }
        if (recipe.Revision != request.Revision)
        {
            return Conflict();
        }
        if (version.IsLocked || version.Batches.Count > 0 || recipe.StandardVersionId == versionId ||
            recipe.Standards.Any(s => s.VersionId == versionId) || recipe.Versions.Any(v => v.ParentId == versionId) ||
            await db.Set<RecipeEntity>().AnyAsync(r => r.OriginRecipeId == recipeId && r.OriginVersionId == versionId, cancellationToken))
        {
            return new ChangeRejected("Only unused drafts can be deleted. This version is preserved or is the starting point for another recipe.");
        }

        if (recipe.Versions.Count == 1)
        {
            db.Remove(recipe);
        }
        else
        {
            db.Remove(version);
            recipe.Revision = Guid.NewGuid();
            recipe.UpdatedAt = DateTimeOffset.UtcNow;
        }
        // Commit the removal and durable blob cleanup together. Uploads racing with
        // deletion fail the recipe revision check and compensate their own blob.
        db.Add(new PhotoCleanupEntity { Prefix = $"{workspace.Id:N}/{recipeId:N}/{versionId:N}/" });
        return await SaveRecipeChangeAsync(db, recipeId, request.Revision, versionId, cancellationToken);
    }
}
