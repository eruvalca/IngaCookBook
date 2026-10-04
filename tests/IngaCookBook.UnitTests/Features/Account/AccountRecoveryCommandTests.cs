using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using IngaCookBook.Data;
using IngaCookBook.Features.Account.Services;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

namespace IngaCookBook.UnitTests.Features.Account;

[SuppressMessage("Maintainability", "CA1515:Consider making public types internal", Justification = "xUnit requires public test classes for discovery.")]
public sealed class AccountRecoveryCommandTests
{
    [Theory]
    [InlineData(null, "https://recipes.example/")]
    [InlineData(" ", "https://recipes.example/")]
    [InlineData("member", null)]
    [InlineData("member", "http://recipes.example/")]
    [InlineData("member", "https://user:password@recipes.example/")]
    [InlineData("member", "https://recipes.example/path")]
    [InlineData("member", "https://recipes.example/?code=untrusted")]
    [InlineData("member", "https://recipes.example/#fragment")]
    [SuppressMessage("Design", "CA1054:URI parameters should not be strings", Justification = "Exercises validation of missing and malformed command-line arguments before URI construction.")]
    public async Task InvalidArgumentsDoNotLookUpOrModifyAnAccount(string? userId, string? baseUrl)
    {
        using var identity = IdentityTestContext.Create();
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);

        (await new AccountRecoveryCommand(identity.Users).RunAsync(userId, baseUrl, output, error)).ShouldBe(2);

        output.ToString().ShouldBeEmpty();
        error.ToString().ShouldContain("Usage:");
        await identity.Users.DidNotReceiveWithAnyArgs().FindByIdAsync(default!);
        await identity.Users.DidNotReceiveWithAnyArgs().GeneratePasswordResetTokenAsync(default!);
    }

    [Fact]
    public async Task UnknownAccountProducesNoResetLink()
    {
        using var identity = IdentityTestContext.Create();
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);

        (await new AccountRecoveryCommand(identity.Users).RunAsync("missing", "https://recipes.example/", output, error)).ShouldBe(1);

        output.ToString().ShouldBeEmpty();
        error.ToString().ShouldContain("No account matches");
        await identity.Users.DidNotReceiveWithAnyArgs().GeneratePasswordResetTokenAsync(default!);
    }

    [Fact]
    public async Task VerifiedOperatorSelectionProducesPrivateEncodedLinkWithoutChangingAccount()
    {
        using var identity = IdentityTestContext.Create();
        var user = new ApplicationUser { Id = "member" };
        identity.Users.FindByIdAsync("member").Returns(user);
        identity.Users.GeneratePasswordResetTokenAsync(user).Returns("token");
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);

        (await new AccountRecoveryCommand(identity.Users).RunAsync("member", "https://recipes.example", output, error)).ShouldBe(0);

        error.ToString().ShouldBeEmpty();
        output.ToString().ShouldContain("Account reference: member");
        output.ToString().ShouldContain("not proof of ownership");
        output.ToString().ShouldContain("https://recipes.example/Account/ResetPassword?code=dG9rZW4");
        await identity.Users.Received(1).GeneratePasswordResetTokenAsync(user);
        await identity.Users.DidNotReceiveWithAnyArgs().UpdateAsync(default!);
        await identity.Users.DidNotReceiveWithAnyArgs().ResetPasswordAsync(default!, default!, default!);
    }

    [Fact]
    public async Task TokenFailureIsNotReportedAsSuccess()
    {
        using var identity = IdentityTestContext.Create();
        var user = new ApplicationUser();
        identity.Users.FindByIdAsync("member").Returns(user);
        identity.Users.GeneratePasswordResetTokenAsync(user).ThrowsAsync(new InvalidOperationException("Token unavailable"));
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);

        await Should.ThrowAsync<InvalidOperationException>(() => new AccountRecoveryCommand(identity.Users)
            .RunAsync("member", "https://recipes.example/", output, error));

        output.ToString().ShouldBeEmpty();
    }
}
