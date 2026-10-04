using System.Diagnostics.CodeAnalysis;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using IngaCookBook.Testing;
using Microsoft.Playwright;
using Shouldly;
using Xunit;

namespace IngaCookBook.PlaywrightTests;

[SuppressMessage("Maintainability", "CA1515:Consider making public types internal", Justification = "xUnit requires public test classes for discovery.")]
public sealed partial class EmailFreeAccountWorkflowTests
{
    [Fact]
    public async Task RegistrationImmediatelyOpensPrivateWorkspaceWithoutAnEmailService()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(4));
        await using var builder = await TestAppHost.CreateAsync(enableEmail: false, timeout.Token);
        builder.Resources.ShouldNotContain(resource => string.Equals(resource.Name, "mailpit", StringComparison.Ordinal));
        await using var app = await builder.BuildAsync(timeout.Token);
        await TestAppHost.StartAsync(app, message => TestContext.Current.SendDiagnosticMessage(message), timeout.Token);
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync();
        var options = new BrowserNewContextOptions { BaseURL = app.GetEndpoint("ingacookbook", "https").ToString(), IgnoreHTTPSErrors = true };
        await using var context = await browser.NewContextAsync(options);
        var page = await context.NewPageAsync();
        var email = $"cook-{Guid.NewGuid():N}@example.test";
        await RegisterAsync(page, email);
        await page.GotoAsync("/workspace");
        await page.GetByLabel("Workspace name", new() { Exact = true }).FillAsync("Email-free kitchen");
        await page.GetByRole(AriaRole.Button, new() { Name = "Create my workspace", Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Heading, new() { Name = "Your recipes", Exact = true }).WaitForAsync();
        await page.GotoAsync("/Account/Manage/Email");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Login details", Exact = true }).WaitForAsync();
        var accountReference = await page.Locator("main code").InnerTextAsync();
        accountReference.ShouldNotBeNullOrWhiteSpace();
        (await page.GetByLabel("Email", new() { Exact = true }).InputValueAsync()).ShouldBe(email);
        (await page.GetByRole(AriaRole.Button, new() { Name = "Send verification email" }).CountAsync()).ShouldBe(0);
        await RejectDisabledEmailPostAsync(page, "/Account/Manage/Email", "change-email", "Input.NewEmail", "other@example.test");

        await page.Locator(".site-navigation").GetByRole(AriaRole.Button, new() { Name = "Logout", Exact = true }).ClickAsync();
        await page.GotoAsync("/Account/ForgotPassword");
        (await page.Locator("main").InnerTextAsync()).ShouldContain("Contact the person who runs it");
        (await page.Locator("main form").CountAsync()).ShouldBe(0);
        await page.GotoAsync("/Account/ResendEmailConfirmation");
        (await page.Locator("main").InnerTextAsync()).ShouldContain("Email confirmation is not required");
        (await page.Locator("main form").CountAsync()).ShouldBe(0);
        await RejectDisabledEmailPostAsync(page, "/Account/ForgotPassword", "forgot-password", "Input.Email", email);
        await RejectDisabledEmailPostAsync(page, "/Account/ResendEmailConfirmation", "resend-email-confirmation", "Input.Email", email);
        var webProject = builder.Resources.OfType<ProjectResource>().Single(resource => string.Equals(resource.Name, "ingacookbook", StringComparison.Ordinal));
        var resetLink = await IssueResetLinkAsync(app, webProject.GetProjectMetadata().ProjectPath, accountReference, timeout.Token);
        await page.GotoAsync(resetLink);
        await page.GetByLabel("Email", new() { Exact = true }).FillAsync(email);
        await page.GetByLabel("Password", new() { Exact = true }).FillAsync("Recovered-Kitchen-456!");
        await page.GetByLabel("Confirm password", new() { Exact = true }).FillAsync("Recovered-Kitchen-456!");
        await page.GetByRole(AriaRole.Button, new() { Name = "Reset", Exact = true }).ClickAsync();
        await page.WaitForURLAsync("**/Account/ResetPasswordConfirmation");
        await page.GotoAsync("/Account/Login");
        await page.GetByLabel("Email", new() { Exact = true }).FillAsync(email);
        await page.GetByLabel("Password", new() { Exact = true }).FillAsync("Kitchen-Test-123!");
        await page.GetByRole(AriaRole.Button, new() { Name = "Log in", Exact = true }).ClickAsync();
        await page.GetByText("Error: Invalid login attempt.", new() { Exact = true }).WaitForAsync();
        await page.GetByLabel("Password", new() { Exact = true }).FillAsync("Recovered-Kitchen-456!");
        await page.GetByRole(AriaRole.Button, new() { Name = "Log in", Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Heading, new() { Name = "Your recipes", Exact = true }).WaitForAsync();
        await page.GotoAsync("/workspace");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Email-free kitchen", Exact = true }).WaitForAsync();

        // A second public registration cannot inherit the first cook's workspace.
        await using var otherContext = await browser.NewContextAsync(options);
        var otherPage = await otherContext.NewPageAsync();
        await RegisterAsync(otherPage, $"other-{Guid.NewGuid():N}@example.test");
        await otherPage.GotoAsync("/workspace");
        await otherPage.GetByRole(AriaRole.Heading, new() { Name = "Make yourself at home", Exact = true }).WaitForAsync();
        await using var response = await otherContext.APIRequest.GetAsync("/api/notebook/workspace");
        response.Status.ShouldBe(200);
        (await response.TextAsync()).ShouldBeEmpty();
    }

    private static async Task RegisterAsync(IPage page, string email)
    {
        await page.GotoAsync("/Account/Register");
        await page.GetByText("No confirmation email is sent.", new() { Exact = false }).WaitForAsync();
        await page.GetByLabel("Email", new() { Exact = true }).FillAsync(email);
        await page.GetByLabel("Password", new() { Exact = true }).FillAsync("Kitchen-Test-123!");
        await page.GetByLabel("Confirm Password", new() { Exact = true }).FillAsync("Kitchen-Test-123!");
        await page.GetByRole(AriaRole.Button, new() { Name = "Register", Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Heading, new() { Name = "Your recipes", Exact = true }).WaitForAsync();
    }

    private static async Task RejectDisabledEmailPostAsync(IPage page, string path, string handler, string field, string value)
    {
        // Login has a real SSR antiforgery token for the current browser identity.
        await page.GotoAsync("/Account/Login");
        var token = await page.Locator("form input[name='__RequestVerificationToken']").First.InputValueAsync();
        var form = page.Context.APIRequest.CreateFormData();
        form.Set("__RequestVerificationToken", token);
        form.Set("_handler", handler);
        form.Set(field, value);
        await using var response = await page.Context.APIRequest.PostAsync(path, new()
        {
            Form = form,
        });
        // Static SSR rejects submission to a form that is no longer offered in this mode.
        response.Status.ShouldBe(400);
    }
}
