using System.Globalization;
using IngaCookBook.Data;
using IngaCookBook.Features.Notebook.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace IngaCookBook.Features.Account.Services;

internal sealed class AccountDeletionService(IDbContextFactory<ApplicationDbContext> contextFactory, IServiceScopeFactory scopes) : IAccountDeletionService
{
    public async Task<IdentityResult> DeleteAsync(ApplicationUser user, CancellationToken cancellationToken = default)
    {
        await using var strategyContext = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await strategyContext.Database.CreateExecutionStrategy().ExecuteAsync(async ct =>
        {
            // Each retry uses a fresh Identity store/context. Identity's delete and the
            // cleanup job share one transaction; no Azure call holds that transaction open.
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            return await DeleteAttemptAsync(db, users, user, ct);
        }, cancellationToken);
    }

    private static async Task<IdentityResult> DeleteAttemptAsync(ApplicationDbContext db, UserManager<ApplicationUser> users,
        ApplicationUser user, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        // A lost commit acknowledgement may cause a retry after the delete and job
        // have already committed. Account absence means the requested result exists.
        if (!await db.Users.AnyAsync(u => u.Id == user.Id, cancellationToken))
        {
            return IdentityResult.Success;
        }
        var workspaceId = await db.Set<WorkspaceEntity>().Where(w => w.OwnerId == user.Id)
            .Select(w => (Guid?)w.Id).SingleOrDefaultAsync(cancellationToken);
        var result = await users.DeleteAsync(user);
        if (!result.Succeeded)
        {
            return result;
        }
        if (workspaceId is { } id)
        {
            db.Add(new PhotoCleanupEntity { Prefix = string.Create(CultureInfo.InvariantCulture, $"{id:N}/") });
            await db.SaveChangesAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return result;
    }
}
