using IngaCookBook.Data;
using IngaCookBook.Features.Account.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;

namespace IngaCookBook.Features.Account.Services;

/// <summary>Trusted operator entry point. It is deliberately not exposed through HTTP.</summary>
internal sealed class AccountRecoveryCommand(UserManager<ApplicationUser> users)
{
    internal async Task<int> RunAsync(string? userId, string? baseUrl, TextWriter output, TextWriter error)
    {
        if (string.IsNullOrWhiteSpace(userId) || !Uri.TryCreate(baseUrl, UriKind.Absolute, out var origin)
            || !string.Equals(origin.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
            || origin.UserInfo.Length != 0 || !string.Equals(origin.AbsolutePath, "/", StringComparison.Ordinal)
            || origin.Query.Length != 0 || origin.Fragment.Length != 0)
        {
            await error.WriteLineAsync("Usage: account-recovery --user-id <account-reference> --base-url https://your-app-host/");
            return 2;
        }

        var user = await users.FindByIdAsync(userId);
        if (user is null)
        {
            await error.WriteLineAsync("No account matches that reference. No reset link was created.");
            return 1;
        }

        var token = await users.GeneratePasswordResetTokenAsync(user);
        var link = QueryHelpers.AddQueryString(new Uri(origin, "Account/ResetPassword").AbsoluteUri,
            "code", token.EncodeIdentityToken());
        await output.WriteLineAsync($"Account reference: {user.Id}");
        await output.WriteLineAsync("Verify the person independently before sharing this link privately. An unverified email address is not proof of ownership.");
        await output.WriteLineAsync("The link expires in one hour and is invalid after a successful password reset. Two-factor authentication remains enabled.");
        await output.WriteLineAsync(link);
        return 0;
    }
}
