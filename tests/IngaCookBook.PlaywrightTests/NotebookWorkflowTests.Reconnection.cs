using Microsoft.Playwright;
using Shouldly;

namespace IngaCookBook.PlaywrightTests;

public sealed partial class NotebookWorkflowTests
{
    private static async Task VerifyRejectedSessionAsync(IPage page, string artifacts)
    {
        var savedNotes = await ReadFieldAsync(page, "Recipe notes");
        var recovery = page.GetByRole(AriaRole.Region, new() { Name = "Session recovery", Exact = true });
        (await recovery.IsVisibleAsync()).ShouldBeFalse();
        (await page.Locator("[data-app-update]").IsVisibleAsync()).ShouldBeFalse();
        const string Notes = "Copy these notes before reconnecting to a new session.";
        await FillFieldAsync(page, "Recipe notes", Notes);
        var documentId = await page.EvaluateAsync<string>("window.recoveryDocumentId = crypto.randomUUID()");
        await RejectSessionAsync(page);
        var dialog = page.GetByRole(AriaRole.Dialog, new() { Name = "Server connection", Exact = true });
        await dialog.WaitForAsync();
        (await dialog.InnerTextAsync()).ShouldContain("review and copy");
        (await page.EvaluateAsync<string>("window.recoveryDocumentId")).ShouldBe(documentId);
        await page.ScreenshotAsync(new() { Path = Path.Combine(artifacts, "session-ended.png") });
        await AttemptNavigationAsync(page, () => dialog.GetByRole(AriaRole.Button, new() { Name = "Refresh app", Exact = true }).ClickAsync(), accept: false, dialogType: "beforeunload");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Review my inputs", Exact = true }).ClickAsync();
        await dialog.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
        await recovery.WaitForAsync();
        (await ReadFieldAsync(page, "Recipe notes")).ShouldBe(Notes);
        (await page.EvaluateAsync<string>("window.recoveryDocumentId")).ShouldBe(documentId);
        (await page.Locator("[data-app-update]").IsVisibleAsync()).ShouldBeFalse();
        await page.ScreenshotAsync(new() { Path = Path.Combine(artifacts, "review-inputs-with-refresh.png") });
        var refresh = recovery.GetByRole(AriaRole.Button, new() { Name = "Refresh app", Exact = true });
        await AttemptNavigationAsync(page, () => refresh.PressAsync("Enter"), accept: false, dialogType: "beforeunload");
        (await recovery.IsVisibleAsync()).ShouldBeTrue();
        (await ReadFieldAsync(page, "Recipe notes")).ShouldBe(Notes);
        (await page.EvaluateAsync<string>("window.recoveryDocumentId")).ShouldBe(documentId);
        await RejectSessionAsync(page);
        await dialog.WaitForAsync();
        await dialog.PressAsync("Escape");
        await recovery.WaitForAsync();
        await AttemptNavigationAsync(page, () => refresh.ClickAsync(), accept: true, dialogType: "beforeunload");
        await page.WaitForFunctionAsync("previous => window.recoveryDocumentId !== previous", documentId);
        await InteractiveButton(page, "Save draft").WaitForAsync();
        (await recovery.IsVisibleAsync()).ShouldBeFalse();
        (await ReadFieldAsync(page, "Recipe notes")).ShouldBe(savedNotes);
    }

    private static async Task RejectSessionAsync(IPage page) => await page.EvaluateAsync("""
        () => {
            const dialog = document.querySelector('#components-reconnect-modal');
            dialog.className = 'components-reconnect-rejected';
            dialog.dispatchEvent(new CustomEvent('components-reconnect-state-changed', { detail: { state: 'rejected' } }));
        }
        """);
}
