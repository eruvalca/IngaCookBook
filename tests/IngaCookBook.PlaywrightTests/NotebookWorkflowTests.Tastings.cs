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
        await InteractiveButton(page, "Save evaluation").WaitForAsync();
        var journal = page.Url;
        await Field(page, "Texture").FillAsync("11");
        await Field(page, "Notes for Texture").FillAsync("Keep this observation");
        await InteractiveButton(page, "Save evaluation").ClickAsync();
        await page.GetByRole(AriaRole.Alert).Filter(new() { HasText = "Correct the highlighted scores" }).WaitForAsync();
        (await Field(page, "Texture").InputValueAsync()).ShouldBe("11");
        (await page.Locator(".evaluation-entry").CountAsync()).ShouldBe(1);
        await AttemptNavigationAsync(page, () => page.GetByRole(AriaRole.Link, new() { Name = "← Version details", Exact = true }).ClickAsync(), false, message: Message);
        page.Url.ShouldBe(journal);
        await AttemptNavigationAsync(page, () => page.GetByRole(AriaRole.Link, new() { Name = "Recipes", Exact = true }).ClickAsync(), false, message: Message);
        await AttemptNavigationAsync(page, () => page.EvaluateAsync("location.reload()"), false, dialogType: "beforeunload");
        await AttemptNavigationAsync(page, () => page.EvaluateAsync("history.back()"), false, message: Message);
        await page.WaitForFunctionAsync("url => location.href === url", journal);
        await AttemptNavigationAsync(page, () => page.EvaluateAsync("history.forward()"), false, message: Message);
        await page.WaitForFunctionAsync("url => location.href === url", journal);
        (await Field(page, "Notes for Texture").InputValueAsync()).ShouldBe("Keep this observation");
        await Field(page, "Texture").FillAsync("7");
        await InteractiveButton(page, "Save evaluation").ClickAsync();
        await page.Locator(".evaluation-entry").Filter(new() { HasText = "Keep this observation" }).WaitForAsync();
        await page.GetByRole(AriaRole.Link, new() { Name = "← Version details", Exact = true }).ClickAsync();
        await page.WaitForURLAsync(details);
        await page.GetByRole(AriaRole.Link, new() { Name = "Make a batch or add a tasting" }).ClickAsync();
        var entry = page.Locator(".evaluation-entry").Filter(new() { HasText = "Keep this observation" });
        await entry.Locator("fluent-button:not([disabled])").ClickAsync();
        (await Field(page, "Texture").InputValueAsync()).ShouldBe("7");
        await Field(page, "Texture").FillAsync("8");
        await InteractiveButton(page, "Save tasting correction").ClickAsync();
        await page.GetByRole(AriaRole.Alert).Filter(new() { HasText = "Explain what" }).WaitForAsync();
        await page.GetByLabel("What are you correcting, and why?", new() { Exact = false }).And(page.Locator("textarea:visible")).FillAsync("Misread my notebook");
        await InteractiveButton(page, "Save tasting correction").ClickAsync();
        await page.Locator(".evaluation-correction summary").Filter(new() { HasText = "Misread my notebook" }).WaitForAsync();
        await page.ReloadAsync();
        await InteractiveButton(page, "Save evaluation").WaitForAsync();
        (await page.Locator(".evaluation-entry").CountAsync()).ShouldBe(2);
        (await entry.Locator(":scope > .score-list").InnerTextAsync()).ShouldContain("Texture: 8");
        await entry.Locator(".evaluation-correction summary").ClickAsync();
        (await entry.Locator(".evaluation-correction .score-list").InnerTextAsync()).ShouldContain("Texture: 7");
        (await entry.Locator(".evaluation-correction").InnerTextAsync()).ShouldContain("Keep this observation");
        // Saving a tasting must not clear the guard for unsaved preparation notes.
        await Field(page, "What happened during preparation?").FillAsync("Unsaved preparation");
        await Field(page, "Overall observations").FillAsync("Another tasting");
        await InteractiveButton(page, "Save evaluation").ClickAsync();
        await page.Locator(".evaluation-entry").Filter(new() { HasText = "Another tasting" }).WaitForAsync();
        await AttemptNavigationAsync(page, () => page.GetByRole(AriaRole.Link, new() { Name = "← Version details", Exact = true }).ClickAsync(), false, message: Message);
        (await Field(page, "What happened during preparation?").InputValueAsync()).ShouldBe("Unsaved preparation");
        await AttemptNavigationAsync(page, () => page.GetByRole(AriaRole.Link, new() { Name = "← Version details", Exact = true }).ClickAsync(), true, message: Message);
        await page.WaitForURLAsync(details);
    }
}
