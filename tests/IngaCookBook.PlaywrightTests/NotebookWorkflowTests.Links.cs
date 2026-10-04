using System.Collections.Concurrent;
using Microsoft.Playwright;
using Shouldly;
using Xunit;

namespace IngaCookBook.PlaywrightTests;

public sealed partial class NotebookWorkflowTests
{
    [Theory]
    [InlineData("Server", 1280)]
    [InlineData("WebAssembly", 390)]
    public async Task NotebookLinksReachTheirSectionsAndKeepUnsavedInputs(string renderer, int width)
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync();
        await using var context = await browser.NewContextAsync(new()
        {
            BaseURL = application.Endpoint.ToString(),
            IgnoreHTTPSErrors = true,
            ViewportSize = new() { Width = width, Height = 900 },
        });
        var releaseDownload = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (string.Equals(renderer, "Server", StringComparison.Ordinal))
        {
            await context.RouteAsync("**/_framework/*.wasm", async route =>
            {
                await releaseDownload.Task;
                await route.AbortAsync();
            });
        }
        await context.Tracing.StartAsync(new() { Screenshots = true, Snapshots = true, Sources = true });
        var page = await context.NewPageAsync();
        var errors = new ConcurrentQueue<string>();
        page.PageError += (_, error) => errors.Enqueue(error);
        page.Response += (_, response) =>
        {
            if (response.Status >= 500) { errors.Enqueue($"HTTP {response.Status}: {response.Url}"); }
        };
        var artifacts = Path.Combine(AppContext.BaseDirectory, "TestResults", $"notebook-links-{renderer}-{Guid.NewGuid():N}");
        try
        {
            await RegisterAndCreateWorkspaceAsync(page);
            await CreateBaselineAsync(page);
            var details = page.Url.Replace("/edit", "", StringComparison.Ordinal);
            await VerifyEditorSectionLinksAsync(page, details, renderer);
            await VerifyPhotoSectionLinksAsync(page, details);
            await VerifyRecipeDestinationLinksAsync(page, details);
            await VerifyAccountDestinationLinksAsync(page);
            errors.ShouldBeEmpty();
        }
        finally
        {
            releaseDownload.TrySetResult();
            await BrowserArtifacts.CaptureAsync(page, context, artifacts, output.WriteLine);
            output.WriteLine($"Browser evidence: {artifacts}");
        }
    }

    private static async Task VerifyEditorSectionLinksAsync(IPage page, string details, string renderer)
    {
        var editor = $"{details}/edit?idea={Guid.NewGuid()}";
        await page.GotoAsync(editor);
        await page.Locator($"[data-renderer={renderer}]").WaitForAsync(new() { Timeout = 60000 });
        await FillFieldAsync(page, "Recipe notes", "Keep these unsaved notes while moving around the page.");
        var origin = await page.EvaluateAsync<double>("performance.timeOrigin");
        await page.GetByRole(AriaRole.Link, new() { Name = "Experiment plan ·", Exact = false }).ClickAsync();
        await page.WaitForURLAsync(editor + "#experiment-plan");
        await AssertSectionInViewportAsync(page, "experiment-plan");
        (await ReadFieldAsync(page, "Recipe notes")).ShouldBe("Keep these unsaved notes while moving around the page.");
        (await ReadFieldAsync(page, "Amount")).ShouldBe("100.1256");
        await VerifySkipLinkAsync(page);
        (await page.EvaluateAsync<double>("performance.timeOrigin")).ShouldBe(origin);
        await SaveDraftAsync(page);
        await page.GetByRole(AriaRole.Link, new() { Name = "← Version details", Exact = true }).ClickAsync();
        await page.WaitForURLAsync(details);
    }

    private static async Task VerifyPhotoSectionLinksAsync(IPage page, string details)
    {
        var origin = await page.EvaluateAsync<double>("performance.timeOrigin");
        await page.GetByRole(AriaRole.Link, new() { Name = "Add photos", Exact = true }).ClickAsync();
        await page.WaitForURLAsync(details + "#photos");
        await AssertSectionInViewportAsync(page, "photos");
        (await page.EvaluateAsync<double>("performance.timeOrigin")).ShouldBe(origin);
        await VerifySkipLinkAsync(page);
        await page.GetByRole(AriaRole.Link, new() { Name = "Choose a cover or edit captions", Exact = true }).ClickAsync();
        await page.Locator("#recipe-photos[open]").WaitForAsync();
        await page.GetByText("No photos yet.", new() { Exact = false }).WaitForAsync();
        await VerifySkipLinkAsync(page);
        await page.GetByRole(AriaRole.Link, new() { Name = "← Recipe history", Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Link, new() { Name = "V1 · First attempt", Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Link, new() { Name = "Add photos", Exact = true }).ClickAsync();
        await page.Locator("input[type=file]:not([disabled])").SetInputFilesAsync(new FilePayload
        {
            Name = "texture.png",
            MimeType = "image/png",
            Buffer = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a0S8AAAAASUVORK5CYII="),
        });
        await page.GetByRole(AriaRole.Img, new() { Name = "texture", Exact = true }).WaitForAsync();
        await page.GetByRole(AriaRole.Link, new() { Name = "Choose a cover or edit captions", Exact = true }).ClickAsync();
        await page.Locator("#recipe-photos[open]").WaitForAsync();
        await Field(page, "Caption · V1 · Photo 1").WaitForAsync();
        await AssertSectionInViewportAsync(page, "recipe-photos");
        await page.GetByRole(AriaRole.Link, new() { Name = "← Recipe history", Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Link, new() { Name = "V1 · First attempt", Exact = true }).ClickAsync();
    }

    private static async Task VerifyRecipeDestinationLinksAsync(IPage page, string details)
    {
        await page.GetByRole(AriaRole.Link, new() { Name = "Kitchen view", Exact = true }).ClickAsync();
        await page.WaitForURLAsync(details + "/kitchen");
        await page.GetByRole(AriaRole.Link, new() { Name = "← Back to recipe", Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Link, new() { Name = "Print recipe", Exact = true }).ClickAsync();
        await page.WaitForURLAsync(details + "/print");
        await page.GetByRole(AriaRole.Link, new() { Name = "← Version details", Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Link, new() { Name = "Compare changes", Exact = true }).ClickAsync();
        await page.GetByText("Choose versions to compare", new() { Exact = true }).WaitForAsync();
        await page.GetByRole(AriaRole.Link, new() { Name = "← Recipe history", Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Link, new() { Name = "Branching view", Exact = true }).ClickAsync();
        await page.Locator(".recipe-tree").GetByRole(AriaRole.Link, new() { Name = "V1 · First attempt", Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Link, new() { Name = "Taste", Exact = true }).ClickAsync();
        await page.WaitForURLAsync(details + "/batches?mode=taste");
        await InteractiveButton(page, "Record batch").WaitForAsync();
        await page.GetByRole(AriaRole.Link, new() { Name = "← Version details", Exact = true }).ClickAsync();
        await page.WaitForURLAsync(details);
    }

    private static async Task VerifyAccountDestinationLinksAsync(IPage page)
    {
        var mobile = page.ViewportSize!.Width < 768;
        if (mobile) { await page.Locator("#site-menu").ClickAsync(); }
        await page.GetByRole(AriaRole.Link, new() { Name = "Workspace", Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Link, new() { Name = "Open your recipe library", Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Heading, new() { Name = "Your recipes", Exact = true }).WaitForAsync();
        if (mobile) { await page.Locator("#site-menu").ClickAsync(); }
        await page.GetByRole(AriaRole.Link, new() { Name = "My account", Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Heading, new() { Name = "Manage your account", Exact = true }).WaitForAsync();
        await VerifySkipLinkAsync(page);
        await page.GetByRole(AriaRole.Link, new() { Name = "Email", Exact = true }).ClickAsync();
        await Field(page, "New email").WaitForAsync();
        await VerifySkipLinkAsync(page);
        await page.GetByRole(AriaRole.Link, new() { Name = "Password", Exact = true }).ClickAsync();
        await Field(page, "Old password").WaitForAsync();
    }

    private static async Task VerifySkipLinkAsync(IPage page)
    {
        var url = page.Url;
        var origin = await page.EvaluateAsync<double>("performance.timeOrigin");
        var skip = page.GetByRole(AriaRole.Link, new() { Name = "Skip to content", Exact = true });
        await skip.FocusAsync();
        await skip.PressAsync("Enter");
        await page.WaitForFunctionAsync("document.activeElement?.id === 'main-content'");
        page.Url.ShouldBe(url);
        (await page.EvaluateAsync<double>("performance.timeOrigin")).ShouldBe(origin);
    }

    private static async Task AssertSectionInViewportAsync(IPage page, string id) =>
        await page.WaitForFunctionAsync("id => { const r = document.getElementById(id)?.getBoundingClientRect(); return r && r.top >= 0 && r.top < innerHeight; }", id);
}
