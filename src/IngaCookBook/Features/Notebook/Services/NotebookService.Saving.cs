using IngaCookBook.Data;
using IngaCookBook.Features.Notebook.Data;
using IngaCookBook.SharedKernel.Notebook;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace IngaCookBook.Features.Notebook.Services;

internal sealed partial class NotebookService
{
    private static async Task<NotebookChange> SaveRecipeChangeAsync(ApplicationDbContext db, Guid recipeId,
        Guid expectedRevision, Guid resultId, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return new ChangeSaved(resultId);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict();
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        { SqlState: PostgresErrorCodes.ForeignKeyViolation or PostgresErrorCodes.RestrictViolation })
        {
            // EF can execute a dependent insert/delete before the recipe's revision
            // check. A concurrent draft deletion or new child then hits its FK first.
            // SaveChanges rolled back the transaction; confirm a competing write
            // rather than disguising an unrelated integrity error as a conflict.
            if (!await db.Set<RecipeEntity>().AnyAsync(r => r.Id == recipeId && r.Revision == expectedRevision, cancellationToken))
            {
                return Conflict();
            }
            throw;
        }
    }
}
