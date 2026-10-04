using Microsoft.Playwright;
using Shouldly;

namespace IngaCookBook.PlaywrightTests;

public sealed partial class NotebookWorkflowTests
{
    private static async Task VerifyJournalGuardAndCorrectionsAsync(IPage page, string details)
    {
        const string Message = "Leave without saving your batch or tasting changes?";
        // Put a real forward entry beside the journal for canceled Back/Forward.
        await page.GetByRole(AriaRole.Link, new() { Name = "← Version details", Exact = true }).ClickAsync();
        await page.WaitForURLAsync(details);
        await page.GetByRole(AriaRole.Link, new() { Name = "Make a batch or add a tasting" }).WaitForAsync();
        await page.GoBackAsync();
        await ClickButtonAsync(page, "Add a tasting");
        await InteractiveButton(page, "Save evaluation").WaitForAsync();
        var journal = page.Url;
        await FillFieldAsync(page, "Texture", "11");
        await FillFieldAsync(page, "Notes for Texture", "Keep this observation");
        await ClickButtonAsync(page, "Save evaluation");
        await page.GetByRole(AriaRole.Alert).Filter(new() { HasText = "Correct the highlighted scores" }).WaitForAsync();
        (await ReadFieldAsync(page, "Texture")).ShouldBe("11");
        (await page.Locator(".evaluation-entry").CountAsync()).ShouldBe(1);
        await AttemptNavigationAsync(page, () => page.GetByRole(AriaRole.Link, new() { Name = "← Version details", Exact = true }).ClickAsync(), false, message: Message);
        page.Url.ShouldBe(journal);
        await AttemptNavigationAsync(page, () => page.GetByRole(AriaRole.Link, new() { Name = "Recipes", Exact = true }).ClickAsync(), false, message: Message);
        await AttemptNavigationAsync(page, () => page.EvaluateAsync("location.reload()"), false, dialogType: "beforeunload");
        await AttemptNavigationAsync(page, () => page.EvaluateAsync("history.back()"), false, message: Message);
        await page.WaitForFunctionAsync("url => location.href === url", journal);
        await AttemptNavigationAsync(page, () => page.EvaluateAsync("history.forward()"), false, message: Message);
        await page.WaitForFunctionAsync("url => location.href === url", journal);
        (await ReadFieldAsync(page, "Notes for Texture")).ShouldBe("Keep this observation");
        await FillFieldAsync(page, "Texture", "7");
        await ClickButtonAsync(page, "Save evaluation");
        await ClickButtonAsync(page, "Recorded tastings");
        await page.Locator(".evaluation-entry").Filter(new() { HasText = "Keep this observation" }).WaitForAsync();
        await page.GetByRole(AriaRole.Link, new() { Name = "← Version details", Exact = true }).ClickAsync();
        await page.WaitForURLAsync(details);
        await page.GetByRole(AriaRole.Link, new() { Name = "Make a batch or add a tasting" }).ClickAsync();
        var entry = page.Locator(".evaluation-entry").Filter(new() { HasText = "Keep this observation" });
        await ClickButtonAsync(page, "Recorded tastings");
        await entry.Locator("fluent-button:not([disabled])").ClickAsync();
        (await ReadFieldAsync(page, "Texture")).ShouldBe("7");
        await FillFieldAsync(page, "Texture", "8");
        await ClickButtonAsync(page, "Save tasting correction");
        await page.GetByRole(AriaRole.Alert).Filter(new() { HasText = "Explain what" }).WaitForAsync();
        await page.GetByLabel("What are you correcting, and why?", new() { Exact = false }).And(page.Locator("textarea:visible")).FillAsync("Misread my notebook");
        await ClickButtonAsync(page, "Save tasting correction");
        await ClickButtonAsync(page, "Recorded tastings");
        await page.Locator(".evaluation-correction summary").Filter(new() { HasText = "Misread my notebook" }).WaitForAsync();
        await page.ReloadAsync();
        await InteractiveButton(page, "Save evaluation").WaitForAsync();
        await ClickButtonAsync(page, "Recorded tastings");
        (await page.Locator(".evaluation-entry").CountAsync()).ShouldBe(2);
        (await entry.Locator(":scope > .score-list").InnerTextAsync()).ShouldContain("Texture: 8");
        await entry.Locator(".evaluation-correction summary").ClickAsync();
        (await entry.Locator(".evaluation-correction .score-list").InnerTextAsync()).ShouldContain("Texture: 7");
        (await entry.Locator(".evaluation-correction").InnerTextAsync()).ShouldContain("Keep this observation");
        // Saving a tasting must not clear the guard for unsaved preparation notes.
        await FillFieldAsync(page, "What happened during preparation?", "Unsaved preparation");
        await FillFieldAsync(page, "Overall observations", "Another tasting");
        await ClickButtonAsync(page, "Save evaluation");
        await ClickButtonAsync(page, "Recorded tastings");
        await page.Locator(".evaluation-entry").Filter(new() { HasText = "Another tasting" }).WaitForAsync();
        await AttemptNavigationAsync(page, () => page.GetByRole(AriaRole.Link, new() { Name = "← Version details", Exact = true }).ClickAsync(), false, message: Message);
        (await ReadFieldAsync(page, "What happened during preparation?")).ShouldBe("Unsaved preparation");
        await AttemptNavigationAsync(page, () => page.GetByRole(AriaRole.Link, new() { Name = "← Version details", Exact = true }).ClickAsync(), true, message: Message);
        await page.WaitForURLAsync(details);
    }
}
