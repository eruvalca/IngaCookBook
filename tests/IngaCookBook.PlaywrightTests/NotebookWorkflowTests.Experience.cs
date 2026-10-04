using Microsoft.Playwright;
using Shouldly;

namespace IngaCookBook.PlaywrightTests;

public sealed partial class NotebookWorkflowTests
{
    private static async Task VerifyEditorFocusClearanceAsync(IPage page)
    {
        var viewport = page.ViewportSize!;
        await page.SetViewportSizeAsync(320, viewport.Height);
        await page.WaitForFunctionAsync("document.documentElement.scrollWidth <= innerWidth");
        (await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth")).ShouldBeTrue();
        await page.SetViewportSizeAsync(viewport.Width, viewport.Height);
        await Field(page, "Ingredient name").FocusAsync();
        for (var index = 0; index < 16; index++)
        {
            await page.Keyboard.PressAsync(index < 8 ? "Tab" : "Shift+Tab");
            await page.WaitForFunctionAsync("""
                () => {
                    let element = document.activeElement;
                    while (element.shadowRoot?.activeElement) element = element.shadowRoot.activeElement;
                    if (!element.matches('input, textarea, select')) return true;
                    const field = element.getBoundingClientRect();
                    const bar = document.querySelector('.save-bar').getBoundingClientRect();
                    return field.top >= 0 && field.bottom <= Math.min(innerHeight, bar.top);
                }
                """);
        }
    }

    private static async Task VerifyKitchenChecklistAsync(IPage page)
    {
        await page.GetByRole(AriaRole.Link, new() { Name = "Kitchen view", Exact = true }).ClickAsync();
        await InteractiveButton(page, "Reset checkmarks").WaitForAsync();
        var ingredient = page.Locator(".kitchen-check").First;
        await ingredient.Locator("fluent-checkbox:not([disabled])").ClickAsync();
        await page.Locator(".kitchen-check[data-complete=true]").WaitForAsync();
        (await page.GetByRole(AriaRole.Status).InnerTextAsync()).ShouldContain("1 of 2 checked");
        (await ingredient.InnerTextAsync()).ShouldContain("100.1256 g");
        await ClickButtonAsync(page, "Reset checkmarks");
        await page.Locator(".kitchen-check[data-complete=true]").WaitForAsync(new() { State = WaitForSelectorState.Hidden });
        (await page.Locator(".kitchen-check[data-complete=true]").CountAsync()).ShouldBe(0);
        await page.GetByRole(AriaRole.Link, new() { Name = "← Back to recipe", Exact = true }).ClickAsync();
    }
}
