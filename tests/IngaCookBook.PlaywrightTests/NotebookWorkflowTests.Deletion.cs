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
    public async Task CookCanDiscardDraftsWithoutLosingPreservedHistory(string renderer, int width)
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
        var artifacts = Path.Combine(AppContext.BaseDirectory, "TestResults", $"draft-deletion-{renderer}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(artifacts);
        try
        {
            await RegisterAndCreateWorkspaceAsync(page);
            await CreateBaselineAsync(page);
            var baseline = page.Url.Replace("/edit", "", StringComparison.Ordinal);
            await page.GotoAsync(baseline);
            await page.GetByRole(AriaRole.Link, new() { Name = "Edit draft", Exact = true }).ClickAsync();
            await page.Locator($"[data-renderer={renderer}]").WaitForAsync(new() { Timeout = 60000 });
            await page.GetByRole(AriaRole.Link, new() { Name = "← Version details", Exact = true }).ClickAsync();
            await ClickButtonAsync(page, "Try a variation");
            await page.Locator($"[data-renderer={renderer}]").WaitForAsync();
            await VerifyUnsavedDraftDeletionAsync(page, artifacts);
            (await page.Locator(".version-timeline > li").CountAsync()).ShouldBe(1);
            await page.GetByRole(AriaRole.Link, new() { Name = "V1 · First attempt", Exact = true }).ClickAsync();
            await InteractiveButton(page, "Try a variation").WaitForAsync();
            (await page.GetByText("Delete draft", new() { Exact = true }).CountAsync()).ShouldBe(0);
            (await page.Locator(".recipe-sheet").InnerTextAsync()).ShouldContain("Heavy cream");
            // Also delete from version details, independently of the editor guard.
            await ClickButtonAsync(page, "Try a variation");
            await InteractiveButton(page, "Save draft").WaitForAsync();
            await page.GetByRole(AriaRole.Link, new() { Name = "← Version details", Exact = true }).ClickAsync();
            // Both pages offer Delete draft. Wait for details content so a click
            // cannot target the outgoing editor during enhanced navigation.
            var versionActions = page.Locator(".version-actions");
            await versionActions.WaitForAsync();
            var deleteDraft = versionActions.Locator("fluent-button:not([disabled])").Filter(new() { HasText = "Delete draft" });
            await deleteDraft.WaitForAsync(new() { State = WaitForSelectorState.Attached });
            await versionActions.GetByText("More recipe actions", new() { Exact = true }).ClickAsync();
            await deleteDraft.ClickAsync();
            await versionActions.Locator("fluent-button:not([disabled])").Filter(new() { HasText = "Delete permanently" }).ClickAsync();
            await page.Locator(".version-timeline").WaitForAsync();
            (await page.Locator(".version-timeline > li").CountAsync()).ShouldBe(1);
            await page.GetByRole(AriaRole.Link, new() { Name = "← Recipe library", Exact = true }).ClickAsync();
            await VerifyOnlyDraftDeletionAsync(page);
            errors.ShouldBeEmpty();
        }
        finally
        {
            await BrowserArtifacts.CaptureAsync(page, context, artifacts, output.WriteLine);
            releaseDownload.TrySetResult();
            await context.UnrouteAllAsync(new() { Behavior = UnrouteBehavior.Wait });
        }
    }

    private static async Task VerifyUnsavedDraftDeletionAsync(IPage page, string artifacts)
    {
        await FillFieldAsync(page, "Recipe notes", "An idea I decided not to pursue.");
        await ClickButtonAsync(page, "Delete draft");
        await page.GetByText("The recipe's other versions and recorded results will stay in your notebook.", new() { Exact = true }).WaitForAsync();
        await ClickButtonAsync(page, "Keep draft");
        (await ReadFieldAsync(page, "Recipe notes")).ShouldBe("An idea I decided not to pursue.");
        await ClickButtonAsync(page, "Delete draft");
        await page.ScreenshotAsync(new() { Path = Path.Combine(artifacts, "confirmation.png"), FullPage = true });
        (await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth")).ShouldBeTrue();
        // No dialog handler: a stale unsaved-edits guard would be dismissed and
        // prevent navigation, failing the history assertion below.
        await ClickButtonAsync(page, "Delete permanently");
        await page.Locator(".version-timeline").WaitForAsync();
    }

    private static async Task VerifyOnlyDraftDeletionAsync(IPage page)
    {
        await page.GetByRole(AriaRole.Link, new() { Name = "+ New recipe", Exact = true }).ClickAsync();
        await InteractiveButton(page, "Create recipe & first version").WaitForAsync();
        await FillFieldAsync(page, "Recipe name", "Abandoned first idea");
        await ClickButtonAsync(page, "Create recipe & first version");
        await RevealAsync(page, InteractiveButton(page, "Delete draft"));
        var draft = page.Url.Replace("/edit", "", StringComparison.Ordinal);
        var api = new Uri(draft).AbsolutePath;
        await using var rejected = await page.Context.APIRequest.DeleteAsync($"/api/notebook{api}/", new()
        {
            DataObject = new { revision = Guid.NewGuid() },
        });
        rejected.Status.ShouldBe(400); // No antiforgery token: must not delete.
        await page.ReloadAsync();
        await ClickButtonAsync(page, "Delete draft");
        await page.GetByText("This is the recipe's only version, so the recipe will also be removed from your library.", new() { Exact = true }).WaitForAsync();
        await ClickButtonAsync(page, "Delete permanently");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Your recipes", Exact = true }).WaitForAsync();
        (await page.Locator(".recipe-card").CountAsync()).ShouldBe(1);
        (await page.Locator(".recipe-card").InnerTextAsync()).ShouldContain("Brown butter vanilla");
        await page.GotoAsync(draft);
        await page.GetByRole(AriaRole.Heading, new() { Name = "Version not found", Exact = true }).WaitForAsync();
    }
}
