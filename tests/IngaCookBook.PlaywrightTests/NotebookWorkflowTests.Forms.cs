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
        await FillFieldAsync(page, "Email", email);
        await FillFieldAsync(page, "Password", Password);
        await FillFieldAsync(page, "Confirm Password", Password);
        await page.GetByRole(AriaRole.Button, new() { Name = "Register", Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Link, new() { Name = "Click here to confirm your account" }).ClickAsync();
        await page.GetByText("Thank you for confirming your email.").WaitForAsync();
        await page.GotoAsync("/Account/Login");
        await FillFieldAsync(page, "Email", email);
        await FillFieldAsync(page, "Password", Password);
        await page.GetByRole(AriaRole.Button, new() { Name = "Log in", Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Heading, new() { Name = "Your recipes", Exact = true }).WaitForAsync();
        await page.GotoAsync("/workspace");
        await FillFieldAsync(page, "Workspace name", "Inga's test kitchen");
        await page.GetByRole(AriaRole.Button, new() { Name = "Create my workspace" }).ClickAsync();
        await page.GetByRole(AriaRole.Heading, new() { Name = "Your recipes", Exact = true }).WaitForAsync();
    }

    private static async Task CreateBaselineAsync(IPage page)
    {
        await page.GetByRole(AriaRole.Link, new() { Name = "+ New recipe", Exact = true }).ClickAsync();
        await InteractiveButton(page, "Create recipe & first version").WaitForAsync();
        await ClickButtonAsync(page, "Create recipe & first version");
        await page.GetByText("Enter a recipe name.", new() { Exact = true }).WaitForAsync();
        await page.WaitForFunctionAsync("() => { let el = document.activeElement; while (el.shadowRoot?.activeElement) el = el.shadowRoot.activeElement; return el.getAttribute('aria-label')?.startsWith('Recipe name'); }");
        await FillFieldAsync(page, "Recipe name", "Brown butter vanilla");
        await FillFieldAsync(page, "About this recipe", "A smooth, scoopable ice cream.");
        await FillFieldAsync(page, "Evaluation metric", "Texture");
        await VerifyLongFormLayoutAsync(page);
        await ClickButtonAsync(page, "Create recipe & first version");
        await ClickButtonAsync(page, "Add ingredient");
        await VerifyDropdownLayoutAsync(page);
        await FillFieldAsync(page, "Ingredient name", "Heavy cream");
        await FillFieldAsync(page, "Amount", "100.123456");
        await Field(page, "Amount").PressAsync("Tab");
        (await ReadFieldAsync(page, "Amount")).ShouldBe("100.123456");
        await FillFieldAsync(page, "Amount", "100.1256");
        await page.GetByText("Purchase cost or linked recipe", new() { Exact = true }).ClickAsync();
        await FillFieldAsync(page, "Purchased amount", "1000");
        await FillFieldAsync(page, "Purchase price (USD)", "8");
        await ClickButtonAsync(page, "Add step");
        await FillFieldAsync(page, "Step 1", "Mix and chill");
        await SaveDraftAsync(page);
        (await ReadFieldAsync(page, "Amount")).ShouldBe("100.1256");
        (await ReadFieldAsync(page, "Purchased amount")).ShouldBe("1000");
        (await page.Locator("main").InnerTextAsync()).ShouldContain("USD 0.80");
        (await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth")).ShouldBeTrue();
    }

    private static async Task SaveDraftAsync(IPage page)
    {
        await ClickButtonAsync(page, "Save draft");
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
        await ClickButtonAsync(page, "Record batch");
        await ClickButtonAsync(page, "Taste now");
        var score = page.Locator("fluent-radio[value='8']");
        await score.ClickAsync();
        await page.WaitForFunctionAsync("document.querySelector('fluent-radio[value=\"8\"]').matches(':state(checked)')");
        await score.PressAsync("ArrowRight");
        await page.WaitForFunctionAsync("document.querySelector('fluent-radio[value=\"9\"]').matches(':state(checked)')");
        await page.Locator("fluent-radio[value='9']").PressAsync("ArrowLeft");
        await page.WaitForFunctionAsync("document.querySelector('fluent-radio[value=\"8\"]').matches(':state(checked)')");
        var targets = await page.Locator("fluent-radio").EvaluateAllAsync<bool>("es => es.every(e => { const r=e.getBoundingClientRect(); return r.width >= 44 && r.height >= 44; })");
        targets.ShouldBeTrue("Score choices should be comfortable tap targets.");
        await FillFieldAsync(page, "Notes for Texture", "Smooth, but a little firm.");
        await FillFieldAsync(page, "One thing to try next", "Try a little more cream.");
        await ClickButtonAsync(page, "Save evaluation");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Your tasting is saved.", Exact = true }).WaitForAsync();
        (await page.Locator(".completion-receipt").InnerTextAsync()).ShouldContain("8 / 10");
        (await page.Locator(".completion-receipt").InnerTextAsync()).ShouldContain("Try a little more cream.");
        await ClickButtonAsync(page, "Recorded tastings");
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
        (await page.Locator(".print-sheet h1").EvaluateAsync<string>("el => getComputedStyle(el).outlineStyle")).ShouldBe("none");
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
        await FillFieldAsync(page, "Recipe notes", "Saved from the browser renderer.");
        await VerifyInputsLockedDuringSaveAsync(page);
        await page.ReloadAsync();
        await page.Locator("[data-renderer=WebAssembly]").WaitForAsync();
        (await ReadFieldAsync(page, "Recipe notes")).ShouldBe("Saved from the browser renderer.");
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
            await ClickButtonAsync(page, "Save draft");
            await received.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);
            await page.Locator(".save-bar").GetByText("Saving…", new() { Exact = true }).WaitForAsync();
            foreach (var label in new[] { "Version label", "Amount", "Step 1", "Recipe notes" })
            {
                (await Field(page, label).IsDisabledAsync()).ShouldBeTrue(label);
            }
            (await ReadFieldAsync(page, "Recipe notes")).ShouldBe("Saved from the browser renderer.");
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
