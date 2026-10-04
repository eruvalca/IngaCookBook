using System.Diagnostics.CodeAnalysis;
using Bunit;
using IngaCookBook.Data;
using IngaCookBook.Features.Account.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace IngaCookBook.ComponentTests.Features.Account;

[SuppressMessage("Maintainability", "CA1515:Consider making public types internal",
    Justification = "xUnit requires public test classes for discovery.")]
public sealed class RegisterConfirmationTests
{
    [Fact]
    public async Task MissingEmailRedirectsHomeWithoutLookingUpUserAsync()
    {
        await using var context = new BunitContext();
        var account = context.ConfigureAccount();
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("Account/RegisterConfirmation");

        account.Render<RegisterConfirmation>(context);

        navigation.Uri.ShouldBe("http://localhost/");
        await account.Users.DidNotReceiveWithAnyArgs().FindByEmailAsync(default!);
    }

    [Fact]
    public async Task UnknownEmailReturnsNotFoundWithoutEchoingAddressOrGeneratingTokenAsync()
    {
        await using var context = new BunitContext();
        var account = context.ConfigureAccount();
        context.Services.GetRequiredService<NavigationManager>().NavigateTo("Account/RegisterConfirmation?email=private%40example.test");

        var component = account.Render<RegisterConfirmation>(context);

        account.Http.Response.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
        component.Find(".notice[data-kind='error']").TextContent.ShouldBe("Error finding user for unspecified email");
        component.Markup.ShouldNotContain("private@example.test");
        component.FindAll("a").ShouldBeEmpty();
        await account.Users.Received(1).FindByEmailAsync("private@example.test");
        await account.Users.DidNotReceiveWithAnyArgs().GenerateEmailConfirmationTokenAsync(default!);
    }

    [Fact]
    public async Task RealEmailSenderShowsInstructionsWithoutExposingConfirmationTokenAsync()
    {
        await using var context = new BunitContext();
        var account = context.ConfigureAccount();
        account.Users.FindByEmailAsync("member@example.test").Returns(new ApplicationUser());
        context.Services.GetRequiredService<NavigationManager>().NavigateTo("Account/RegisterConfirmation?email=member%40example.test");

        var component = account.Render<RegisterConfirmation>(context);

        component.Find("p[role='alert']").TextContent.ShouldBe("Please check your email to confirm your account.");
        component.FindAll("a").ShouldBeEmpty();
        await account.Users.DidNotReceiveWithAnyArgs().GenerateEmailConfirmationTokenAsync(default!);
        await account.Emails.DidNotReceiveWithAnyArgs().SendConfirmationLinkAsync(default!, default!, default!);
    }

}
