using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Playwright;
using Shouldly;
using Xunit;

namespace IngaCookBook.PlaywrightTests;

[SuppressMessage("Maintainability", "CA1515:Consider making public types internal", Justification = "xUnit requires public test classes for discovery.")]
public sealed partial class NotebookWorkflowTests(ITestOutputHelper output, BrowserAppFixture application)
{
    [Theory]
    [InlineData(1280)]
    [InlineData(390)]
    public async Task CookCanRecordEvaluateCompareAndPrintAnExperiment(int width)
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync();
        await using var context = await browser.NewContextAsync(new()
        {
            BaseURL = application.Endpoint.ToString(),
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
            await VerifyEditorFocusClearanceAsync(page);
            await page.ScreenshotAsync(new() { Path = Path.Combine(artifacts, "editor.png"), FullPage = true });
            var baseline = page.Url.Replace("/edit", "", StringComparison.Ordinal);
            await page.GetByRole(AriaRole.Link, new() { Name = "← Version details", Exact = true }).ClickAsync();
            await VerifyVersionActionsLayoutAsync(page);
            await VerifyKitchenChecklistAsync(page);
            await RecordPhotoAndTastingAsync(page);
            await ClickButtonAsync(page, "Set as standard");
            await page.GetByText("Current standard", new() { Exact = true }).First.WaitForAsync();
            await ClickButtonAsync(page, "Try a variation");
            await InteractiveButton(page, "Save draft").WaitForAsync();
            await FillFieldAsync(page, "Amount", "120");
            await FillFieldAsync(page, "Step 1", "Mix and chill for longer");
            await FillFieldAsync(page, "What are you testing?", "Will more cream and mixing improve the texture?");
            await page.GetByText("Several things changed.", new() { Exact = false }).WaitForAsync();
            await ClickButtonAsync(page, "Save draft");
            await page.GetByRole(AriaRole.Alert).Filter(new() { HasText = "Explain how" }).WaitForAsync();
            await FillFieldAsync(page, "How do these changes belong together?", "The larger quantity needs longer mixing.");
            await SaveDraftAsync(page);
            var variation = page.Url.Replace("/edit", "", StringComparison.Ordinal);
            await page.GetByRole(AriaRole.Link, new() { Name = "← Version details", Exact = true }).ClickAsync();
            await page.GetByRole(AriaRole.Link, new() { Name = "Compare changes", Exact = true }).ClickAsync();
            var changes = page.GetByRole(AriaRole.Table).Filter(new() { HasText = "Changes from V1 to V2" });
            await changes.WaitForAsync();
            await page.GetByText("Choose versions to compare", new() { Exact = true }).ClickAsync();
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
        page.GetByLabel(label, new() { Exact = true }).Or(page.GetByLabel(label + ", Required", new() { Exact = true })).And(page.Locator("input:visible, textarea:visible, select:visible"));

    private static ILocator InteractiveButton(IPage page, string label) =>
        page.Locator("fluent-button:not([disabled])").Filter(new() { HasText = label });

    private static async Task RevealAsync(IPage page, ILocator control)
    {
        await control.WaitForAsync(new() { State = WaitForSelectorState.Attached });
        var hiddenTask = page.Locator(".journal-form > div[hidden]").Filter(new() { Has = control });
        if (await hiddenTask.CountAsync() > 0)
        {
            var isBatch = await hiddenTask.Locator("#made-date").CountAsync() > 0;
            await InteractiveButton(page, isBatch ? "Record a batch" : "Add a tasting").ClickAsync();
        }
        var disclosures = page.Locator("details:not([open])").Filter(new() { Has = control });
        while (await disclosures.CountAsync() > 0)
        {
            await disclosures.First.Locator(":scope > summary").ClickAsync();
        }
    }

    private static async Task FillFieldAsync(IPage page, string label, string value)
    {
        var control = page.GetByLabel(label, new() { Exact = true }).Or(page.GetByLabel(label + ", Required", new() { Exact = true })).And(page.Locator("input, textarea, select"));
        await RevealAsync(page, control);
        await control.FillAsync(value);
    }

    private static async Task<string> ReadFieldAsync(IPage page, string label)
    {
        var control = page.GetByLabel(label, new() { Exact = true }).Or(page.GetByLabel(label + ", Required", new() { Exact = true })).And(page.Locator("input, textarea, select"));
        await RevealAsync(page, control);
        return await control.InputValueAsync();
    }

    private static async Task ClickButtonAsync(IPage page, string label)
    {
        var control = InteractiveButton(page, label);
        await RevealAsync(page, control);
        await control.ClickAsync();
    }
}
