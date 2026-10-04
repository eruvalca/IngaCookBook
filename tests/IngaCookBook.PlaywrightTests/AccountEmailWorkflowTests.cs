using System.Diagnostics.CodeAnalysis;
using Microsoft.Playwright;
using Shouldly;
using Xunit;

namespace IngaCookBook.PlaywrightTests;

[SuppressMessage("Maintainability", "CA1515:Consider making public types internal", Justification = "xUnit requires public test classes for discovery.")]
public sealed class AccountEmailWorkflowTests(BrowserAppFixture application)
{
    [Fact]
    public async Task CookCanConfirmRecoverAndChangeEmailUsingCapturedMessages()
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync();
        await using var context = await browser.NewContextAsync(new() { BaseURL = application.Endpoint.ToString(), IgnoreHTTPSErrors = true });
        var page = await context.NewPageAsync();
        var email = $"account-{Guid.NewGuid():N}@example.test";
        const string InitialPassword = "Kitchen-Test-123!";
        const string NewPassword = "Better-Kitchen-456!";
        await page.GotoAsync("/Account/Register");
        await page.GetByLabel("Email", new() { Exact = true }).FillAsync(email);
        await page.GetByLabel("Password", new() { Exact = true }).FillAsync(InitialPassword);
        await page.GetByLabel("Confirm Password", new() { Exact = true }).FillAsync(InitialPassword);
        await page.GetByRole(AriaRole.Button, new() { Name = "Register", Exact = true }).ClickAsync();
        await page.GetByText("Please check your email to confirm your account.", new() { Exact = true }).WaitForAsync();
        (await page.Locator("a[href*='code=']").CountAsync()).ShouldBe(0);
        await LoginAsync(page, email, InitialPassword);
        await page.GetByText("Error: Invalid login attempt.", new() { Exact = true }).WaitForAsync();
        await page.GotoAsync(await AccountInbox.ReadLinkAsync(application, email, "/Account/ConfirmEmail", TestContext.Current.CancellationToken));
        await page.GetByText("Thank you for confirming your email.", new() { Exact = true }).WaitForAsync();

        await page.GotoAsync("/Account/ForgotPassword");
        await page.GetByLabel("Email", new() { Exact = true }).FillAsync(email);
        await page.GetByRole(AriaRole.Button, new() { Name = "Reset password", Exact = true }).ClickAsync();
        await page.WaitForURLAsync("**/Account/ForgotPasswordConfirmation");
        var resetLink = await AccountInbox.ReadLinkAsync(application, email, "/Account/ResetPassword", TestContext.Current.CancellationToken);
        await page.GotoAsync(resetLink);
        await ResetAsync(page, email, NewPassword);
        await page.WaitForURLAsync("**/Account/ResetPasswordConfirmation");
        await LoginAsync(page, email, InitialPassword);
        await page.GetByText("Error: Invalid login attempt.", new() { Exact = true }).WaitForAsync();
        await LoginAsync(page, email, NewPassword);
        await page.GetByRole(AriaRole.Heading, new() { Name = "Your recipes", Exact = true }).WaitForAsync();

        var newEmail = $"changed-{Guid.NewGuid():N}@example.test";
        await page.GotoAsync("/Account/Manage/Email");
        await page.GetByLabel("New email", new() { Exact = true }).FillAsync(newEmail);
        await page.GetByRole(AriaRole.Button, new() { Name = "Change email", Exact = true }).ClickAsync();
        await page.GetByText("Confirmation link to change email sent. Please check your email.", new() { Exact = true }).WaitForAsync();
        await page.GotoAsync(await AccountInbox.ReadLinkAsync(application, newEmail, "/Account/ConfirmEmailChange", TestContext.Current.CancellationToken));
        await page.GetByText("Thank you for confirming your email change.", new() { Exact = true }).WaitForAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Logout", Exact = true }).ClickAsync();
        await LoginAsync(page, newEmail, NewPassword);
        await page.GetByRole(AriaRole.Heading, new() { Name = "Your recipes", Exact = true }).WaitForAsync();
        await page.GotoAsync("/Account/Manage/Email");
        (await page.GetByLabel("Email", new() { Exact = true }).InputValueAsync()).ShouldBe(newEmail);
    }

    private static async Task LoginAsync(IPage page, string email, string password)
    {
        await page.GotoAsync("/Account/Login");
        await page.GetByLabel("Email", new() { Exact = true }).FillAsync(email);
        await page.GetByLabel("Password", new() { Exact = true }).FillAsync(password);
        await page.GetByRole(AriaRole.Button, new() { Name = "Log in", Exact = true }).ClickAsync();
    }

    private static async Task ResetAsync(IPage page, string email, string password)
    {
        await page.GetByLabel("Email", new() { Exact = true }).FillAsync(email);
        await page.GetByLabel("Password", new() { Exact = true }).FillAsync(password);
        await page.GetByLabel("Confirm password", new() { Exact = true }).FillAsync(password);
        await page.GetByRole(AriaRole.Button, new() { Name = "Reset", Exact = true }).ClickAsync();
    }
}
