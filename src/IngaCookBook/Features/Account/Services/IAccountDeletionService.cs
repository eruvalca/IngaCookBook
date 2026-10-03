using IngaCookBook.Data;
using Microsoft.AspNetCore.Identity;

namespace IngaCookBook.Features.Account.Services;

internal interface IAccountDeletionService
{
    Task<IdentityResult> DeleteAsync(ApplicationUser user, CancellationToken cancellationToken = default);
}
