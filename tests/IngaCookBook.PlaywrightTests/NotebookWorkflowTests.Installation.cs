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
    public async Task AppInstallationAndUpdatesPreserveNotebookWork(string renderer, int width)
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
        var errors = new ConcurrentQueue<string>();
        var page = await context.NewPageAsync();
        page.PageError += (_, error) => errors.Enqueue(error);
        await context.Tracing.StartAsync(new() { Screenshots = true, Snapshots = true, Sources = true });
        var artifacts = Path.Combine(AppContext.BaseDirectory, "TestResults", $"installation-{renderer}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(artifacts);
        try
        {
            await page.RunAndWaitForResponseAsync(() => page.GotoAsync("/"), IsVersionResponse);
            await VerifyInstallationAssetsAsync(page);
            await VerifyInstallChoiceAsync(page, width);
            await RegisterAndCreateWorkspaceAsync(page);
            await CreateBaselineAsync(page);
            var editor = page.Url;
            await page.GotoAsync(editor);
            await page.Locator($"[data-renderer={renderer}]").WaitForAsync(new() { Timeout = 60000 });
            await VerifySafeUpdateAsync(page, artifacts);
            await VerifyRejectedSessionAsync(page, artifacts);
            errors.ShouldBeEmpty();
        }
        finally
        {
            releaseDownload.TrySetResult();
            await context.UnrouteAllAsync(new() { Behavior = UnrouteBehavior.Wait });
            await BrowserArtifacts.CaptureAsync(page, context, artifacts, output.WriteLine);
        }
    }

    private static bool IsVersionResponse(IResponse response) => response.Url.EndsWith("/app-version", StringComparison.Ordinal);

    private static async Task VerifyInstallationAssetsAsync(IPage page)
    {
        var result = await page.EvaluateAsync<string[]>("""
            async () => {
                const response = await fetch(document.querySelector('link[rel=manifest]').href);
                if (!response.ok) throw new Error('Manifest not served');
                const manifest = await response.json();
                const icons = await Promise.all(manifest.icons.map(async icon => {
                    const image = new Image();
                    image.src = icon.src;
                    await image.decode();
                    if (icon.sizes !== `${image.naturalWidth}x${image.naturalHeight}`) throw new Error('Incorrect icon dimensions');
                    return `${icon.sizes}:${icon.purpose}`;
                }));
                const apple = new Image();
                apple.src = document.querySelector('link[rel=apple-touch-icon]').href;
                await apple.decode();
                if (apple.naturalWidth !== 180 || apple.naturalHeight !== 180) throw new Error('Incorrect Apple icon');
                return [manifest.id, manifest.start_url, manifest.scope, manifest.display, ...icons];
            }
            """);
        result.ShouldBe(["/", "/recipes", "/", "standalone", "192x192:any", "512x512:any", "512x512:maskable"]);
        (await page.EvaluateAsync<int>("async () => (await navigator.serviceWorker.getRegistrations()).length")).ShouldBe(0);
        var version = await page.APIRequest.GetAsync("/app-version");
        version.Ok.ShouldBeTrue();
        version.Headers["cache-control"].ShouldBe("no-store");
        var release = await version.TextAsync();
        release.ShouldMatch("^[a-f0-9]{64}$");
        (await page.Locator("meta[name=app-release]").GetAttributeAsync("content")).ShouldBe(release);
        (await (await page.APIRequest.GetAsync("/app-version")).TextAsync()).ShouldBe(release);
    }

    private static async Task VerifyInstallChoiceAsync(IPage page, int width)
    {
        if (width < 768) { await page.Locator("#site-menu").ClickAsync(); }
        await page.Locator("[data-install-app]:visible summary").ClickAsync();
        await page.GetByText("An internet connection is required.", new() { Exact = true }).And(page.Locator("p:visible")).WaitForAsync();
        // Model the browser-owned permission prompt; native OS installation is a device check.
        await SupplyInstallPromptAsync(page, "dismissed");
        await page.GetByRole(AriaRole.Button, new() { Name = "Install Inga’s", Exact = true }).ClickAsync();
        await page.Locator("[data-install-prompt]:visible").WaitForAsync(new() { State = WaitForSelectorState.Hidden });
        (await page.EvaluateAsync<int>("window.installPromptCalls")).ShouldBe(1);
        (await page.Locator("[data-install-app]:visible").CountAsync()).ShouldBe(1);
        (await page.Locator("[data-install-help]:visible").CountAsync()).ShouldBe(1);
        await Field(page, "Appearance").SelectOptionAsync("dark");
        await page.WaitForFunctionAsync("document.querySelector('meta[name=\"theme-color\"]').content === '#262B24'");
        await Field(page, "Appearance").SelectOptionAsync("light");
        await page.WaitForFunctionAsync("document.querySelector('meta[name=\"theme-color\"]').content === '#FAF5ED'");
        await SupplyInstallPromptAsync(page, "accepted");
        await page.GetByRole(AriaRole.Button, new() { Name = "Install Inga’s", Exact = true }).PressAsync("Enter");
        await page.Locator("[data-install-app]:visible").WaitForAsync(new() { State = WaitForSelectorState.Hidden });
        (await page.EvaluateAsync<int>("window.installPromptCalls")).ShouldBe(2);
        await page.GetByRole(AriaRole.Link, new() { Name = "Home", Exact = true }).ClickAsync();
        (await page.Locator("[data-install-app]:visible").CountAsync()).ShouldBe(0);
    }

    private static async Task SupplyInstallPromptAsync(IPage page, string outcome) => await page.EvaluateAsync("""
        outcome => {
            const event = new Event('beforeinstallprompt', { cancelable: true });
            event.prompt = async () => { window.installPromptCalls = (window.installPromptCalls ?? 0) + 1; };
            event.userChoice = Promise.resolve({ outcome });
            window.dispatchEvent(event);
        }
        """, outcome);

    private static async Task VerifySafeUpdateAsync(IPage page, string artifacts)
    {
        var documentId = await page.EvaluateAsync<string>("window.updateDocumentId = crypto.randomUUID()");
        const string Notes = "Keep this unfinished recipe while the app updates.";
        await FillFieldAsync(page, "Recipe notes", Notes);
        var release = new string('b', 64);
        await page.RouteAsync("**/app-version", route => route.FulfillAsync(new() { ContentType = "text/plain", Body = release }));
        await CheckNewReleaseAsync(page, 10);
        await page.GetByRole(AriaRole.Region, new() { Name = "Application update", Exact = true }).WaitForAsync();
        (await page.EvaluateAsync<string>("window.updateDocumentId")).ShouldBe(documentId);
        await page.ScreenshotAsync(new() { Path = Path.Combine(artifacts, "update-with-unsaved-recipe.png"), FullPage = true });
        await AttemptNavigationAsync(page, () => page.GetByRole(AriaRole.Button, new() { Name = "Refresh app", Exact = true }).ClickAsync(), accept: false, dialogType: "beforeunload");
        (await ReadFieldAsync(page, "Recipe notes")).ShouldBe(Notes);
        (await page.EvaluateAsync<string>("window.updateDocumentId")).ShouldBe(documentId);
        await page.GetByRole(AriaRole.Button, new() { Name = "Later", Exact = true }).ClickAsync();
        await page.Locator("[data-app-update]").WaitForAsync(new() { State = WaitForSelectorState.Hidden });
        await SaveDraftAsync(page);
        await page.GetByRole(AriaRole.Link, new() { Name = "← Version details", Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Link, new() { Name = "Edit draft", Exact = true }).ClickAsync();
        await InteractiveButton(page, "Save draft").WaitForAsync();
        (await page.Locator("[data-app-update]").IsVisibleAsync()).ShouldBeFalse();
        await CheckNewReleaseAsync(page, 20);
        (await page.Locator("[data-app-update]").IsVisibleAsync()).ShouldBeFalse();
        release = new string('c', 64);
        await CheckNewReleaseAsync(page, 30);
        await page.GetByRole(AriaRole.Region, new() { Name = "Application update", Exact = true }).WaitForAsync();
        await page.UnrouteAsync("**/app-version");
        await page.GetByRole(AriaRole.Button, new() { Name = "Refresh app", Exact = true }).ClickAsync();
        await page.WaitForFunctionAsync("previous => window.updateDocumentId !== previous", documentId);
        await InteractiveButton(page, "Save draft").WaitForAsync();
        (await ReadFieldAsync(page, "Recipe notes")).ShouldBe(Notes);
    }

    private static async Task CheckNewReleaseAsync(IPage page, int minutes)
    {
        // Advance only Date; no sleeping or fast-forwarding Blazor/circuit timers.
        await page.Clock.SetFixedTimeAsync(DateTime.UtcNow.AddMinutes(minutes));
        await page.RunAndWaitForResponseAsync(() => page.EvaluateAsync("window.dispatchEvent(new Event('online'))"), IsVersionResponse);
    }
}
