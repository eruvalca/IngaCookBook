using IngaCookBook.Data;
using Microsoft.AspNetCore.Identity;
using OneOf;

namespace IngaCookBook.Features.Account.Models;

[GenerateOneOf]
internal sealed partial class RegistrationOutcome : OneOfBase<RegistrationOutcome.Created, RegistrationOutcome.CreationRejected>
{
    internal sealed record Created(ApplicationUser User);
    internal sealed record CreationRejected(IReadOnlyList<IdentityError> Errors);
}
