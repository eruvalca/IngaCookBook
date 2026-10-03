using Microsoft.Playwright;
using Shouldly;

namespace IngaCookBook.PlaywrightTests;

public sealed partial class NotebookWorkflowTests
{
    private static async Task VerifyLongFormLayoutAsync(IPage page)
    {
        for (var index = 0; index < 8; index++)
        {
            await InteractiveButton(page, "Add metric").ClickAsync();
            await page.WaitForFunctionAsync("count => document.querySelectorAll('.control-row').length === count", index + 2);
        }
        await page.EvaluateAsync("window.scrollTo(0, 0)");
        await page.Mouse.MoveAsync(page.ViewportSize!.Width - 25, 400);
        await page.Mouse.WheelAsync(0, 700);
        await page.WaitForFunctionAsync("window.scrollY > 100");
        (await page.EvaluateAsync<double>("window.scrollY")).ShouldBeGreaterThan(100);
        (await page.Locator(".site-header").EvaluateAsync<double>("el => el.getBoundingClientRect().height")).ShouldBeGreaterThanOrEqualTo(60);
        (await page.Locator(".control-row").EvaluateAllAsync<bool>("""
            rows => rows.every(row => {
                const input = row.querySelector('fluent-text-input').getBoundingClientRect();
                const button = row.querySelector('fluent-button').getBoundingClientRect();
                return Math.abs(input.bottom - button.bottom) <= 1;
            })
            """)).ShouldBeTrue("Metric inputs and their remove buttons should align.");
        for (var index = 0; index < 8; index++)
        {
            await InteractiveButton(page, "Remove").Last.ClickAsync();
            await page.WaitForFunctionAsync("count => document.querySelectorAll('.control-row').length === count", 8 - index);
        }
        (await Field(page, "Evaluation metric").CountAsync()).ShouldBe(1);
    }

    private static async Task VerifyDropdownLayoutAsync(IPage page)
    {
        var focus = page.GetByRole(AriaRole.Combobox, new() { Name = "Metric to improve", Exact = true });
        (await focus.EvaluateAsync<string>("el => getComputedStyle(el).borderTopWidth")).ShouldBe("0px",
            "The Fluent dropdown's inner button must not gain a second application border.");
        (await focus.EvaluateAsync<string>("el => getComputedStyle(el).paddingTop")).ShouldBe("0px");
        await focus.ClickAsync();
        await page.Locator("fluent-option:visible").Filter(new() { HasText = "Texture" }).ClickAsync();
        await page.WaitForFunctionAsync("() => document.querySelector('button[aria-label=\"Metric to improve\"]').textContent.trim() === 'Texture'");
        (await focus.InnerTextAsync()).Trim().ShouldBe("Texture");
    }

    private static async Task VerifyVersionActionsLayoutAsync(IPage page)
    {
        await InteractiveButton(page, "Try a variation").WaitForAsync();
        var edit = await page.GetByRole(AriaRole.Link, new() { Name = "Edit draft", Exact = true }).BoundingBoxAsync();
        var vary = await InteractiveButton(page, "Try a variation").BoundingBoxAsync();
        edit.ShouldNotBeNull();
        vary.ShouldNotBeNull();
        Math.Abs(edit.Height - vary.Height).ShouldBeLessThanOrEqualTo(1);
    }
}
