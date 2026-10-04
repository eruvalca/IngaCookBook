using Aspire.Hosting.Testing;
using IngaCookBook.Testing;
using Microsoft.Playwright;
using Shouldly;
using Xunit;

namespace IngaCookBook.PlaywrightTests;

public sealed partial class NotebookWorkflowTests
{
    [Theory]
    [InlineData("Server")]
    [InlineData("WebAssembly")]
    public async Task EditorProtectsEnhancedNavigationAndJournalUsesBrowserDates(string renderer)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(5));
        await using var builder = await TestAppHost.CreateAsync(timeout.Token);
        await using var app = await builder.BuildAsync(timeout.Token);
        await app.StartAsync(timeout.Token);
        await app.ResourceNotifications.WaitForResourceHealthyAsync("ingacookbook", timeout.Token);
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync();
        await using var context = await browser.NewContextAsync(new()
        {
            BaseURL = app.GetEndpoint("ingacookbook", "https").ToString(),
            IgnoreHTTPSErrors = true,
            TimezoneId = "America/Chicago",
        });
        var releaseDownload = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (string.Equals(renderer, "Server", StringComparison.Ordinal))
        {
            // Keep Auto on the server for this case, regardless of download speed.
            await context.RouteAsync("**/_framework/*.wasm", async route =>
            {
                await releaseDownload.Task;
                await route.AbortAsync();
            });
        }
        await context.Tracing.StartAsync(new() { Screenshots = true, Snapshots = true, Sources = true });
        var page = await context.NewPageAsync();
        var artifacts = Path.Combine(AppContext.BaseDirectory, "TestResults", $"notebook-guards-{renderer}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(artifacts);
        try
        {
            await RegisterAndCreateWorkspaceAsync(page);
            await CreateBaselineAsync(page);
            var details = page.Url.Replace("/edit", "", StringComparison.Ordinal);
            await page.GotoAsync(details);
            await page.GetByRole(AriaRole.Link, new() { Name = "Edit draft", Exact = true }).ClickAsync();
            await page.Locator($"[data-renderer={renderer}]").WaitForAsync(new() { Timeout = 60000 });
            await VerifyEditorGuardAsync(page, details);
            await VerifyLocalDatesAsync(page, details, renderer);
            await VerifyRecipeCreationAndSettingsGuardsAsync(page, details, renderer);
            await VerifyBatchCorrectionRecoveryAsync(page, details, renderer);
        }
        finally
        {
            await BrowserArtifacts.CaptureAsync(page, context, artifacts, output.WriteLine);
            releaseDownload.TrySetResult();
            await context.UnrouteAllAsync(new() { Behavior = UnrouteBehavior.Wait });
        }
    }

    private static async Task VerifyEditorGuardAsync(IPage page, string details)
    {
        // Leave a real entry on each side of the editor, using enhanced navigation.
        await page.GetByRole(AriaRole.Link, new() { Name = "← Version details", Exact = true }).ClickAsync();
        await page.WaitForURLAsync(details);
        await page.GetByRole(AriaRole.Link, new() { Name = "Edit draft", Exact = true }).WaitForAsync();
        await page.GoBackAsync();
        await InteractiveButton(page, "Save draft").WaitForAsync();
        var editor = page.Url;
        var origin = await page.EvaluateAsync<double>("performance.timeOrigin");
        const string Unsaved = "Do not lose these kitchen notes.";
        await FillFieldAsync(page, "Recipe notes", Unsaved);
        await AttemptNavigationAsync(page, () => page.GetByRole(AriaRole.Link, new() { Name = "← Version details", Exact = true }).ClickAsync(), accept: false);
        page.Url.ShouldBe(editor);
        await AttemptNavigationAsync(page, () => page.GetByRole(AriaRole.Link, new() { Name = "Recipes", Exact = true }).ClickAsync(), accept: false);
        page.Url.ShouldBe(editor);
        await AttemptNavigationAsync(page, () => page.EvaluateAsync("location.reload()"), accept: false, dialogType: "beforeunload");
        (await ReadFieldAsync(page, "Recipe notes")).ShouldBe(Unsaved);
        await AttemptNavigationAsync(page, () => page.EvaluateAsync("history.back()"), accept: false);
        await page.WaitForFunctionAsync("url => location.href === url", editor);
        (await ReadFieldAsync(page, "Recipe notes")).ShouldBe(Unsaved);
        await AttemptNavigationAsync(page, () => page.EvaluateAsync("history.forward()"), accept: false);
        await page.WaitForFunctionAsync("url => location.href === url", editor);
        (await ReadFieldAsync(page, "Recipe notes")).ShouldBe(Unsaved);
        (await page.EvaluateAsync<double>("performance.timeOrigin")).ShouldBe(origin);

        await AttemptNavigationAsync(page, () => page.EvaluateAsync("history.forward()"), accept: true);
        await page.WaitForURLAsync(details);
        await page.GetByRole(AriaRole.Link, new() { Name = "Edit draft", Exact = true }).WaitForAsync();
        await page.GoBackAsync();
        await InteractiveButton(page, "Save draft").WaitForAsync();
        (await ReadFieldAsync(page, "Recipe notes")).ShouldBeEmpty();
        await FillFieldAsync(page, "Recipe notes", "Kept after saving.");
        await SaveDraftAsync(page);
        // With no Dialog handler Playwright dismisses prompts. A stale guard would
        // therefore prevent this navigation and fail the URL assertion.
        await page.GetByRole(AriaRole.Link, new() { Name = "← Version details", Exact = true }).ClickAsync();
        await page.WaitForURLAsync(details);
        (await page.EvaluateAsync<double>("performance.timeOrigin")).ShouldBe(origin);
        await page.GetByRole(AriaRole.Link, new() { Name = "Edit draft", Exact = true }).ClickAsync();
        await InteractiveButton(page, "Save draft").WaitForAsync();
        (await ReadFieldAsync(page, "Recipe notes")).ShouldBe("Kept after saving.");
        await FillFieldAsync(page, "Recipe notes", "Discard this edit.");
        await AttemptNavigationAsync(page, () => page.GetByRole(AriaRole.Link, new() { Name = "← Version details", Exact = true }).ClickAsync(), accept: true);
        await page.WaitForURLAsync(details);
    }

    private static async Task AttemptNavigationAsync(IPage page, Func<Task> navigate, bool accept, string dialogType = "confirm", string message = "Leave without saving your recipe changes?")
    {
        var received = new TaskCompletionSource<IDialog>(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnDialog(object? sender, IDialog dialog) => received.TrySetResult(dialog);
        page.Dialog += OnDialog;
        try
        {
            var navigation = navigate();
            var dialog = await received.Task.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);
            dialog.Type.ShouldBe(dialogType);
            if (string.Equals(dialogType, "confirm", StringComparison.Ordinal))
            {
                dialog.Message.ShouldBe(message);
            }
            if (accept) { await dialog.AcceptAsync(); }
            else { await dialog.DismissAsync(); }
            await navigation;
        }
        finally
        {
            page.Dialog -= OnDialog;
        }
    }

    private static async Task VerifyLocalDatesAsync(IPage page, string details, string renderer)
    {
        // The browser is Oct 2 in Chicago while the UTC date is Oct 3. Changing
        // only Date leaves timers/network waits running normally.
        await page.Clock.SetFixedTimeAsync(new DateTime(2001, 10, 3, 0, 30, 0, DateTimeKind.Utc));
        await page.GetByRole(AriaRole.Link, new() { Name = "Make a batch or add a tasting" }).ClickAsync();
        await page.Locator($"[data-renderer={renderer}]").WaitForAsync();
        await ClickButtonAsync(page, "Record a batch");
        await InteractiveButton(page, "Record batch").WaitForAsync();
        (await ReadFieldAsync(page, "Date made")).ShouldBe("2001-10-02");
        await ClickButtonAsync(page, "Record batch");
        await ClickButtonAsync(page, "Add a tasting");
        await InteractiveButton(page, "Save evaluation").WaitForAsync();
        (await ReadFieldAsync(page, "Date tasted")).ShouldBe("2001-10-02");
        await FillFieldAsync(page, "Overall observations", "Evening tasting");
        await ClickButtonAsync(page, "Save evaluation");
        await ClickButtonAsync(page, "Recorded tastings");
        await page.Locator(".evaluation-entry").Filter(new() { HasText = "Evening tasting" }).WaitForAsync();
        await VerifyJournalGuardAndCorrectionsAsync(page, details);
        await page.GotoAsync(details);
        (await page.Locator(".batch-history > summary").InnerTextAsync()).ShouldContain("Oct 2, 2001");
        await page.Locator(".batch-history > summary").ClickAsync();
        (await page.Locator(".batch-history .evaluation-entry h3").First.InnerTextAsync()).ShouldContain("Oct 2, 2001");
        await page.GetByRole(AriaRole.Link, new() { Name = "← Recipe history", Exact = true }).ClickAsync();
        var timestamp = page.Locator(".version-timeline time[data-local-date]").First;
        await timestamp.WaitForAsync();
        // Exercise a known UTC boundary through the same semantic timestamp used
        // by SSR history, standards, and interactive correction history.
        await timestamp.EvaluateAsync("element => element.setAttribute('datetime', '2001-10-03T00:30:00Z')");
        await page.WaitForFunctionAsync("document.querySelector('.version-timeline time[data-local-date]').textContent === 'Oct 2, 2001'");
    }
}
