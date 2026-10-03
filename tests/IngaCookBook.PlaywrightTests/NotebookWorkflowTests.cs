using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Aspire.Hosting.Testing;
using IngaCookBook.Testing;
using Microsoft.Playwright;
using Shouldly;
using Xunit;

namespace IngaCookBook.PlaywrightTests;

[SuppressMessage("Maintainability", "CA1515:Consider making public types internal", Justification = "xUnit requires public test classes for discovery.")]
public sealed partial class NotebookWorkflowTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(1280)]
    [InlineData(390)]
    public async Task CookCanRecordEvaluateCompareAndPrintAnExperiment(int width)
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
            ViewportSize = new() { Width = width, Height = 900 },
        });
        await context.Tracing.StartAsync(new() { Screenshots = true, Snapshots = true, Sources = true });
        var page = await context.NewPageAsync();
        var errors = new ConcurrentQueue<string>();
        page.PageError += (_, error) => errors.Enqueue(error);
        var artifacts = Path.Combine(AppContext.BaseDirectory, "TestResults", $"notebook-{width}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(artifacts);
        try
        {
            await RegisterAndCreateWorkspaceAsync(page);
            await CreateBaselineAsync(page);
            await page.ScreenshotAsync(new() { Path = Path.Combine(artifacts, "editor.png"), FullPage = true });
            var baseline = page.Url.Replace("/edit", "", StringComparison.Ordinal);
            await page.GetByRole(AriaRole.Link, new() { Name = "← Version details", Exact = true }).ClickAsync();
            await RecordPhotoAndTastingAsync(page);
            await InteractiveButton(page, "Set as standard").ClickAsync();
            await page.GetByText("Current standard", new() { Exact = true }).First.WaitForAsync();
            await InteractiveButton(page, "Try a variation").ClickAsync();
            await InteractiveButton(page, "Save draft").WaitForAsync();
            await Field(page, "Amount").FillAsync("120");
            await Field(page, "Step 1").FillAsync("Mix and chill for longer");
            await Field(page, "What are you testing?").FillAsync("Will more cream and mixing improve the texture?");
            await page.GetByText("Several things changed.", new() { Exact = false }).WaitForAsync();
            await InteractiveButton(page, "Save draft").ClickAsync();
            await page.GetByRole(AriaRole.Alert).Filter(new() { HasText = "Explain how" }).WaitForAsync();
            await Field(page, "How do these changes belong together?").FillAsync("The larger quantity needs longer mixing.");
            await SaveDraftAsync(page);
            var variation = page.Url.Replace("/edit", "", StringComparison.Ordinal);
            await page.GetByRole(AriaRole.Link, new() { Name = "← Version details", Exact = true }).ClickAsync();
            await page.GetByRole(AriaRole.Link, new() { Name = "Compare changes", Exact = true }).ClickAsync();
            var changes = page.GetByRole(AriaRole.Table).Filter(new() { HasText = "Changes from V1 to V2" });
            await changes.WaitForAsync();
            (await Field(page, "Starting version").Locator("option").First.TextContentAsync()).ShouldBe("V1 · First attempt");
            (await changes.InnerTextAsync()).ShouldContain("120");
            (await changes.InnerTextAsync()).ShouldContain("100.1256 g");
            (await page.GetByRole(AriaRole.Table).Filter(new() { HasText = "Quality scores" }).InnerTextAsync()).ShouldContain("8");
            (await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth")).ShouldBeTrue();
            (await page.Locator(".table-container").EvaluateAllAsync<bool>("elements => elements.every(element => element.scrollWidth <= element.clientWidth)")).ShouldBeTrue();
            await page.ScreenshotAsync(new() { Path = Path.Combine(artifacts, "comparison.png"), FullPage = true });
            await page.GetByRole(AriaRole.Link, new() { Name = "← Recipe history", Exact = true }).ClickAsync();
            await page.GetByRole(AriaRole.Link, new() { Name = "Branching view", Exact = true }).ClickAsync();
            (await page.Locator(".recipe-tree").InnerTextAsync()).ShouldContain("V2");
            await VerifyThemeAndPrintAsync(page, baseline, width);
            await VerifyWebAssemblyAndAntiforgeryAsync(page, variation);
            errors.ShouldBeEmpty();
        }
        finally
        {
            await BrowserArtifacts.CaptureAsync(page, context, artifacts, output.WriteLine);
            output.WriteLine($"Browser evidence: {artifacts}");
        }
    }

    private static ILocator Field(IPage page, string label) =>
        page.GetByLabel(label, new() { Exact = true }).And(page.Locator("input:visible, textarea:visible, select:visible"));

    private static ILocator InteractiveButton(IPage page, string label) =>
        page.Locator("fluent-button:not([disabled])").Filter(new() { HasText = label });
}
