using IngaCookBook.Features.Notebook.Data;
using IngaCookBook.SharedKernel.Notebook;
using Microsoft.EntityFrameworkCore;

namespace IngaCookBook.Features.Notebook.Services;

internal sealed partial class NotebookService
{
    private async Task<NotebookChange> InsertPromotionAsync(RecipeDocument recipe, Guid revision, CancellationToken cancellationToken)
    {
        var workspace = await GetWorkspaceAsync(cancellationToken);
        if (workspace is null)
        {
            return new ChangeRejected("This recipe could not be found.", 404);
        }
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var original = await db.Set<RecipeEntity>().Include(r => r.Versions)
            .SingleOrDefaultAsync(r => r.Id == recipe.OriginRecipeId && r.WorkspaceId == workspace.Id, cancellationToken);
        var source = original?.Versions.FirstOrDefault(v => v.Id == recipe.OriginVersionId);
        if (original is null || source is null)
        {
            return new ChangeRejected("This version could not be found.", 404);
        }
        if (original.Revision != revision)
        {
            return Conflict();
        }
        // Preserve the origin and create its independent copy atomically. A delete
        // or edit in another tab must not race past the original revision check.
        source.IsLocked = true;
        original.Revision = Guid.NewGuid();
        original.UpdatedAt = DateTimeOffset.UtcNow;
        var entity = new RecipeEntity { WorkspaceId = workspace.Id };
        RecipeMapping.Apply(entity, recipe);
        db.Add(entity);
        return await SaveRecipeChangeAsync(db, original.Id, revision, recipe.Id, cancellationToken);
    }
}
