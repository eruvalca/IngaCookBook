using Microsoft.Playwright;
using Shouldly;

namespace IngaCookBook.PlaywrightTests;

public sealed partial class NotebookWorkflowTests
{
    private static async Task RegisterAndCreateWorkspaceAsync(IPage page)
    {
        var email = $"cook-{Guid.NewGuid():N}@example.test";
        const string Password = "Kitchen-Test-123!";
        await page.GotoAsync("/Account/Register");
        await Field(page, "Email").FillAsync(email);
        await Field(page, "Password").FillAsync(Password);
        await Field(page, "Confirm Password").FillAsync(Password);
        await page.GetByRole(AriaRole.Button, new() { Name = "Register", Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Link, new() { Name = "Click here to confirm your account" }).ClickAsync();
        await page.GetByText("Thank you for confirming your email.").WaitForAsync();
        await page.GotoAsync("/Account/Login");
        await Field(page, "Email").FillAsync(email);
        await Field(page, "Password").FillAsync(Password);
        await page.GetByRole(AriaRole.Button, new() { Name = "Log in", Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Heading, new() { Name = "A little better, every batch.", Exact = true }).WaitForAsync();
        await page.GotoAsync("/workspace");
        await Field(page, "Workspace name").FillAsync("Inga's test kitchen");
        await page.GetByRole(AriaRole.Button, new() { Name = "Create my workspace" }).ClickAsync();
        await page.GetByRole(AriaRole.Heading, new() { Name = "Your recipes", Exact = true }).WaitForAsync();
    }

    private static async Task CreateBaselineAsync(IPage page)
    {
        await page.GetByRole(AriaRole.Link, new() { Name = "+ New recipe", Exact = true }).ClickAsync();
        await InteractiveButton(page, "Create recipe & first version").WaitForAsync();
        await Field(page, "Recipe name").FillAsync("Brown butter vanilla");
        await Field(page, "About this recipe").FillAsync("A smooth, scoopable ice cream.");
        await Field(page, "Evaluation metric").FillAsync("Texture");
        await VerifyLongFormLayoutAsync(page);
        await InteractiveButton(page, "Create recipe & first version").ClickAsync();
        await InteractiveButton(page, "Add ingredient").ClickAsync();
        await VerifyDropdownLayoutAsync(page);
        await Field(page, "Ingredient name").FillAsync("Heavy cream");
        await Field(page, "Amount").FillAsync("100.123456");
        await Field(page, "Amount").PressAsync("Tab");
        (await Field(page, "Amount").InputValueAsync()).ShouldBe("100.123456");
        await Field(page, "Amount").FillAsync("100.1256");
        await page.GetByText("Purchase cost or linked recipe", new() { Exact = true }).ClickAsync();
        await Field(page, "Purchased amount").FillAsync("1000");
        await Field(page, "Purchase price (USD)").FillAsync("8");
        await InteractiveButton(page, "Add step").ClickAsync();
        await Field(page, "Step 1").FillAsync("Mix and chill");
        await SaveDraftAsync(page);
        (await Field(page, "Amount").InputValueAsync()).ShouldBe("100.1256");
        (await Field(page, "Purchased amount").InputValueAsync()).ShouldBe("1000");
        (await page.Locator("main").InnerTextAsync()).ShouldContain("USD 0.80");
        (await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth")).ShouldBeTrue();
    }

    private static async Task SaveDraftAsync(IPage page)
    {
        await InteractiveButton(page, "Save draft").ClickAsync();
        await page.GetByText("All changes saved", new() { Exact = true }).WaitForAsync();
        (await page.GetByRole(AriaRole.Alert).CountAsync()).ShouldBe(0);
    }

    private static async Task RecordPhotoAndTastingAsync(IPage page)
    {
        await page.Locator("input[type=file]:not([disabled])").SetInputFilesAsync(new FilePayload
        {
            Name = "texture.png",
            MimeType = "image/png",
            Buffer = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a0S8AAAAASUVORK5CYII="),
        });
        var image = page.GetByRole(AriaRole.Img, new() { Name = "texture", Exact = true });
        await image.WaitForAsync();
        await image.ScrollIntoViewIfNeededAsync();
        await image.EvaluateAsync("image => image.decode()");
        (await image.EvaluateAsync<bool>("image => image.complete && image.naturalWidth > 0")).ShouldBeTrue();
        await page.GetByRole(AriaRole.Link, new() { Name = "Make a batch or add a tasting" }).ClickAsync();
        await InteractiveButton(page, "Record batch").ClickAsync();
        await Field(page, "Texture").FillAsync("8");
        await Field(page, "Notes for Texture").FillAsync("Smooth, but a little firm.");
        await Field(page, "One thing to try next").FillAsync("Try a little more cream.");
        await InteractiveButton(page, "Save evaluation").ClickAsync();
        await page.Locator(".evaluation-entry").Filter(new() { HasText = "Smooth, but a little firm." }).WaitForAsync();
        await page.GetByRole(AriaRole.Link, new() { Name = "← Version details", Exact = true }).ClickAsync();
    }

    private static async Task VerifyThemeAndPrintAsync(IPage page, string baseline, int width)
    {
        if (width < 768)
        {
            await page.Locator("#site-menu").ClickAsync();
        }
        await Field(page, "Appearance").SelectOptionAsync("dark");
        await page.WaitForFunctionAsync("document.documentElement.dataset.appearance === 'dark'");
        await page.GotoAsync(baseline + "/print");
        (await page.Locator("html").GetAttributeAsync("data-appearance")).ShouldBe("dark");
        await page.EmulateMediaAsync(new() { Media = Media.Print });
        (await page.Locator(".print-sheet").InnerTextAsync()).ShouldContain("Heavy cream");
        (await page.Locator(".print-sheet").InnerTextAsync()).ShouldContain("Mix and chill");
        (await page.Locator(".no-print").IsVisibleAsync()).ShouldBeFalse();
        await page.EmulateMediaAsync(new() { Media = Media.Screen });
        if (width < 768)
        {
            await page.Locator("#site-menu").ClickAsync();
        }
        await Field(page, "Appearance").SelectOptionAsync("system");
        await page.EmulateMediaAsync(new() { ColorScheme = ColorScheme.Light });
        await page.WaitForFunctionAsync("document.documentElement.dataset.appearance === 'light'");
    }

    private static async Task VerifyWebAssemblyAndAntiforgeryAsync(IPage page, string variation)
    {
        // A later visit exercises Auto's cached WebAssembly renderer and its HTTP adapter.
        await page.GotoAsync(variation + "/edit");
        await page.Locator("[data-renderer=WebAssembly]").WaitForAsync(new() { Timeout = 60000 });
        var token = await page.Context.APIRequest.GetAsync("/api/notebook/token");
        token.Status.ShouldBe(200);
        token.Headers["cache-control"].ShouldBe("no-cache, no-store");
        token.Headers["pragma"].ShouldBe("no-cache");
        await token.DisposeAsync();
        await Field(page, "Recipe notes").FillAsync("Saved from the browser renderer.");
        await VerifyInputsLockedDuringSaveAsync(page);
        await page.ReloadAsync();
        await page.Locator("[data-renderer=WebAssembly]").WaitForAsync();
        (await Field(page, "Recipe notes").InputValueAsync()).ShouldBe("Saved from the browser renderer.");
        var response = await page.Context.APIRequest.PostAsync("/api/notebook/recipes", new()
        {
            DataObject = new { name = "Must be rejected", description = "", metrics = Array.Empty<string>() },
        });
        response.Status.ShouldBe(400);
        await response.DisposeAsync();
    }

    private static async Task VerifyInputsLockedDuringSaveAsync(IPage page)
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        var received = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        const string SaveRoute = "**/api/notebook/recipes/*/versions/*/";
        await page.RouteAsync(SaveRoute, async route =>
        {
            received.TrySetResult();
            await release.Task.WaitAsync(ct);
            await route.ContinueAsync();
        });
        try
        {
            await InteractiveButton(page, "Save draft").ClickAsync();
            await received.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);
            await page.Locator(".save-bar").GetByText("Saving…", new() { Exact = true }).WaitForAsync();
            foreach (var label in new[] { "Version label", "Amount", "Step 1", "Recipe notes" })
            {
                (await Field(page, label).IsDisabledAsync()).ShouldBeTrue(label);
            }
            (await Field(page, "Recipe notes").InputValueAsync()).ShouldBe("Saved from the browser renderer.");
        }
        finally
        {
            release.TrySetResult();
        }
        await page.GetByText("All changes saved", new() { Exact = true }).WaitForAsync();
        (await Field(page, "Recipe notes").IsDisabledAsync()).ShouldBeFalse();
        await page.UnrouteAsync(SaveRoute);
    }
}
