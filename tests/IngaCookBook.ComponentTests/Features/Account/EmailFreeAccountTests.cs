using System.Diagnostics.CodeAnalysis;
using Bunit;
using IngaCookBook.Features.Account.Pages;
using IngaCookBook.Features.Account.Pages.Manage;
using IngaCookBook.Features.Account.Services.Email;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace IngaCookBook.ComponentTests.Features.Account;

[SuppressMessage("Maintainability", "CA1515:Consider making public types internal", Justification = "xUnit requires public test classes for discovery.")]
public sealed class EmailFreeAccountTests
{
    [Fact]
    public async Task RecoveryAndResendPagesExplainManualHelpWithoutOfferingAnEmailForm()
    {
        await using var context = new BunitContext();
        var account = context.ConfigureAccount();
        context.Services.Configure<AccountEmailOptions>(options => options.Provider = "None");

        var forgot = account.Render<ForgotPassword>(context);
        forgot.FindAll("form").ShouldBeEmpty();
        forgot.Markup.ShouldContain("Contact the person who runs it");
        var resend = account.Render<ResendEmailConfirmation>(context);
        resend.FindAll("form").ShouldBeEmpty();
        resend.Markup.ShouldContain("Email confirmation is not required");
        account.Render<ForgotPasswordConfirmation>(context).Markup.ShouldNotContain("Please check your email");
        account.Render<RegisterConfirmation>(context).Markup.ShouldContain("Email confirmation is not required");
        await account.Users.DidNotReceiveWithAnyArgs().FindByEmailAsync(default!);
        await account.Emails.DidNotReceiveWithAnyArgs().SendConfirmationLinkAsync(default!, default!, default!);
        await account.Emails.DidNotReceiveWithAnyArgs().SendPasswordResetLinkAsync(default!, default!, default!);
    }

    [Fact]
    public async Task LoginAndRegistrationExplainTheEmailFreePolicy()
    {
        await using var context = new BunitContext();
        var account = context.ConfigureAccount();
        context.Services.Configure<AccountEmailOptions>(options => options.Provider = "None");

        account.Render<Register>(context).Markup.ShouldContain("No confirmation email is sent");
        var login = account.Render<Login>(context);
        login.FindAll("a[href='Account/ResendEmailConfirmation']").ShouldBeEmpty();
        login.Find("a[href='Account/ForgotPassword']").TextContent.ShouldContain("Forgot");
    }

    [Fact]
    public async Task LoginDetailsShowAccountReferenceAndDoNotOfferEmailChanges()
    {
        await using var context = new BunitContext();
        var account = context.ConfigureAccount();
        context.Services.Configure<AccountEmailOptions>(options => options.Provider = "None");
        var user = account.Authenticate();
        account.Users.GetEmailAsync(user).Returns("cook@example.test");

        var component = account.Render<Email>(context);

        component.Find("#email").GetAttribute("value").ShouldBe("cook@example.test");
        component.Find("code").TextContent.ShouldBe(user.Id);
        component.FindAll("form").ShouldBeEmpty();
        component.Markup.ShouldContain("does not verify ownership");
        await account.Emails.DidNotReceiveWithAnyArgs().SendConfirmationLinkAsync(default!, default!, default!);
    }

    [Fact]
    public async Task OldConfirmationLinksCannotChangeOrVerifyEmail()
    {
        await using var context = new BunitContext();
        var account = context.ConfigureAccount();
        context.Services.Configure<AccountEmailOptions>(options => options.Provider = "None");
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("Account/ConfirmEmail?userId=member&code=dG9rZW4");

        account.Render<ConfirmEmail>(context).Markup.ShouldContain("Email confirmation is not required");
        navigation.NavigateTo("Account/ConfirmEmailChange?userId=member&email=other%40example.test&code=dG9rZW4");
        account.Render<ConfirmEmailChange>(context).Markup.ShouldContain("Email changes are not available");

        await account.Users.DidNotReceiveWithAnyArgs().FindByIdAsync(default!);
        await account.Users.DidNotReceiveWithAnyArgs().ConfirmEmailAsync(default!, default!);
        await account.Users.DidNotReceiveWithAnyArgs().ChangeEmailAsync(default!, default!, default!);
    }
}
