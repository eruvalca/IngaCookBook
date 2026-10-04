using System.Security.Claims;
using IngaCookBook.Data;
using IngaCookBook.Features.Notebook.Data;
using IngaCookBook.SharedKernel.Notebook;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;

namespace IngaCookBook.Features.Notebook.Services;

internal sealed partial class NotebookService(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    IHttpContextAccessor httpContextAccessor,
    AuthenticationStateProvider authentication,
    IRecipePhotoStore photos,
    ILogger<NotebookService> logger) : INotebookService
{

    private async Task<string?> UserIdAsync()
    {
        var user = httpContextAccessor.HttpContext?.User ?? (await authentication.GetAuthenticationStateAsync()).User;
        return user.Identity?.IsAuthenticated == true ? user.FindFirstValue(ClaimTypes.NameIdentifier) : null;
    }

    public async Task<WorkspaceView?> GetWorkspaceAsync(CancellationToken cancellationToken)
    {
        var userId = await UserIdAsync();
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Set<WorkspaceEntity>().Where(w => w.OwnerId == userId)
            .Select(w => new WorkspaceView(w.Id, w.Name, w.Currency)).SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RecipeDocument>> GetRecipesAsync(CancellationToken cancellationToken)
    {
        var workspace = await GetWorkspaceAsync(cancellationToken);
        if (workspace is null)
        {
            return [];
        }
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entities = await RecipeMapping.Complete(db.Set<RecipeEntity>().AsNoTracking().Where(r => r.WorkspaceId == workspace.Id))
            .OrderByDescending(r => r.UpdatedAt).ToListAsync(cancellationToken);
        return entities.Select(RecipeMapping.Read).ToArray();
    }

    public async Task<RecipeDocument?> GetRecipeAsync(Guid id, CancellationToken cancellationToken)
    {
        var workspace = await GetWorkspaceAsync(cancellationToken);
        if (workspace is null)
        {
            return null;
        }
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await RecipeMapping.Complete(db.Set<RecipeEntity>().AsNoTracking()
            .Where(r => r.Id == id && r.WorkspaceId == workspace.Id)).SingleOrDefaultAsync(cancellationToken);
        return entity is null ? null : RecipeMapping.Read(entity);
    }

    private async Task<NotebookChange> UpdateAsync(Guid id, Guid revision,
        Func<RecipeDocument, RecipeDocument?> update, Guid resultId, CancellationToken cancellationToken)
    {
        var workspace = await GetWorkspaceAsync(cancellationToken);
        if (workspace is null)
        {
            return new ChangeRejected("Create your workspace first.", 404);
        }
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await RecipeMapping.Complete(db.Set<RecipeEntity>().Where(r => r.Id == id && r.WorkspaceId == workspace.Id))
            .SingleOrDefaultAsync(cancellationToken);
        if (entity is null)
        {
            return new ChangeRejected("This recipe could not be found.", 404);
        }
        if (entity.Revision != revision)
        {
            return Conflict();
        }
        var changed = update(RecipeMapping.Read(entity));
        if (changed is null)
        {
            return new ChangeRejected("The selected version or batch could not be found.", 404);
        }
        changed = changed with { Revision = Guid.NewGuid(), UpdatedAt = DateTimeOffset.UtcNow };
        RecipeMapping.Apply(entity, changed);
        return await SaveRecipeChangeAsync(db, id, revision, resultId, cancellationToken);
    }

    private async Task<NotebookChange> InsertAsync(RecipeDocument recipe, CancellationToken cancellationToken)
    {
        var workspace = await GetWorkspaceAsync(cancellationToken);
        if (workspace is null)
        {
            return new ChangeRejected("Create your workspace first.", 404);
        }
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = new RecipeEntity { WorkspaceId = workspace.Id };
        RecipeMapping.Apply(entity, recipe);
        db.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return new ChangeSaved(recipe.Id);
    }

    private static ChangeRejected Conflict() => new("This recipe changed in another tab or device. Your inputs are still here. Reload the recipe before saving again.", 409);

    private static RecipeDocument? Replace(RecipeDocument recipe, Guid id, Func<RecipeVersion, RecipeVersion> change) =>
        recipe.Versions.Any(v => v.Id == id)
            ? recipe with { Versions = recipe.Versions.Select(v => v.Id == id ? change(v) : v).ToArray() }
            : null;
}
