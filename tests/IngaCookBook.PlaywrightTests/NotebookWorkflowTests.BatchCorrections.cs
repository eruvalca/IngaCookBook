using Microsoft.Playwright;
using Shouldly;

namespace IngaCookBook.PlaywrightTests;

public sealed partial class NotebookWorkflowTests
{
    private static async Task VerifyRecipeCreationAndSettingsGuardsAsync(IPage page, string details, string renderer)
    {
        await page.GetByRole(AriaRole.Link, new() { Name = "← Recipe library", Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Link, new() { Name = "+ New recipe", Exact = true }).ClickAsync();
        await page.Locator($"[data-renderer={renderer}]").WaitForAsync();
        await InteractiveButton(page, "Create recipe & first version").WaitForAsync();
        // Establish a forward history entry, then return to the clean form.
        await page.GetByRole(AriaRole.Link, new() { Name = "← Recipe library", Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Heading, new() { Name = "Your recipes", Exact = true }).WaitForAsync();
        await page.GoBackAsync();
        await InteractiveButton(page, "Create recipe & first version").WaitForAsync();
        var form = page.Url;
        await Field(page, "Recipe name").FillAsync("Protected recipe");
        await Field(page, "About this recipe").FillAsync("Keep this description");
        await VerifyFormNavigationGuardAsync(page, form, "← Recipe library");
        (await Field(page, "Recipe name").InputValueAsync()).ShouldBe("Protected recipe");
        (await Field(page, "About this recipe").InputValueAsync()).ShouldBe("Keep this description");
        await InteractiveButton(page, "Add metric").ClickAsync();
        await InteractiveButton(page, "Create recipe & first version").ClickAsync();
        await page.GetByRole(AriaRole.Alert).WaitForAsync();
        await AttemptNavigationAsync(page, () => page.GetByRole(AriaRole.Link, new() { Name = "← Recipe library", Exact = true }).ClickAsync(), false);
        await Field(page, "Evaluation metric").Nth(1).FillAsync("Freshness");
        await InteractiveButton(page, "Create recipe & first version").ClickAsync();
        // The create handler must clear the guard before programmatic navigation.
        await InteractiveButton(page, "Save draft").WaitForAsync();
        (await page.Locator("main").InnerTextAsync()).ShouldContain("Protected recipe");

        var recipe = details[..details.IndexOf("/versions/", StringComparison.Ordinal)];
        await page.GotoAsync(recipe);
        await page.GetByRole(AriaRole.Link, new() { Name = "Recipe & evaluation settings", Exact = true }).ClickAsync();
        await InteractiveButton(page, "Save settings").WaitForAsync();
        await page.GetByRole(AriaRole.Link, new() { Name = "← Recipe history", Exact = true }).ClickAsync();
        await page.WaitForURLAsync(recipe);
        await page.GoBackAsync();
        await InteractiveButton(page, "Save settings").WaitForAsync();
        form = page.Url;
        // A structural edit alone must mark the form dirty.
        await InteractiveButton(page, "Add metric").ClickAsync();
        await VerifyFormNavigationGuardAsync(page, form, "← Recipe history");
        (await Field(page, "Metric name").CountAsync()).ShouldBe(2);
        await InteractiveButton(page, "Save settings").ClickAsync();
        await page.GetByRole(AriaRole.Alert).WaitForAsync();
        await AttemptNavigationAsync(page, () => page.GetByRole(AriaRole.Link, new() { Name = "← Recipe history", Exact = true }).ClickAsync(), false);
        await Field(page, "Metric name").Nth(1).FillAsync("Freshness");
        await Field(page, "About this recipe").FillAsync("Saved description");
        await InteractiveButton(page, "Save settings").ClickAsync();
        await page.GetByRole(AriaRole.Status).Filter(new() { HasText = "Saved to your notebook" }).WaitForAsync();
        await page.GetByRole(AriaRole.Link, new() { Name = "← Recipe history", Exact = true }).ClickAsync();
        await page.WaitForURLAsync(recipe);
        await page.GetByRole(AriaRole.Link, new() { Name = "Recipe & evaluation settings", Exact = true }).WaitForAsync();
        (await page.Locator("main").InnerTextAsync()).ShouldContain("Saved description");
        await page.GetByRole(AriaRole.Link, new() { Name = "Recipe & evaluation settings", Exact = true }).ClickAsync();
        await InteractiveButton(page, "Save settings").WaitForAsync();
        (await Field(page, "Metric name").Nth(1).InputValueAsync()).ShouldBe("Freshness");
        await Field(page, "About this recipe").FillAsync("Discard this description");
        await AttemptNavigationAsync(page, () => page.GetByRole(AriaRole.Link, new() { Name = "← Recipe history", Exact = true }).ClickAsync(), true);
        await page.WaitForURLAsync(recipe);
        await page.GetByRole(AriaRole.Link, new() { Name = "Recipe & evaluation settings", Exact = true }).WaitForAsync();
        (await page.Locator("main").InnerTextAsync()).ShouldNotContain("Discard this description");
    }

    private static async Task VerifyFormNavigationGuardAsync(IPage page, string form, string backLink)
    {
        await AttemptNavigationAsync(page, () => page.GetByRole(AriaRole.Link, new() { Name = backLink, Exact = true }).ClickAsync(), false);
        page.Url.ShouldBe(form);
        await AttemptNavigationAsync(page, () => page.GetByRole(AriaRole.Link, new() { Name = "Recipes", Exact = true }).ClickAsync(), false);
        await AttemptNavigationAsync(page, () => page.EvaluateAsync("location.reload()"), false, dialogType: "beforeunload");
        await AttemptNavigationAsync(page, () => page.EvaluateAsync("history.back()"), false);
        await page.WaitForFunctionAsync("url => location.href === url", form);
        await AttemptNavigationAsync(page, () => page.EvaluateAsync("history.forward()"), false);
        await page.WaitForFunctionAsync("url => location.href === url", form);
    }

    private static async Task VerifyBatchCorrectionRecoveryAsync(IPage page, string details, string renderer)
    {
        const string Message = "Leave without saving your batch or tasting changes?";
        await page.GotoAsync(details);
        await page.GetByRole(AriaRole.Link, new() { Name = "Make a batch or add a tasting" }).ClickAsync();
        await page.Locator($"[data-renderer={renderer}]").WaitForAsync();
        await InteractiveButton(page, "Record batch").WaitForAsync();
        await Field(page, "Date made").FillAsync("2001-10-03");
        await Field(page, "What happened during preparation?").FillAsync("Wrong date, right batch");
        await InteractiveButton(page, "Record batch").ClickAsync();
        await page.GetByRole(AriaRole.Heading, new() { Name = "Batch 2 · Oct 3, 2001", Exact = true }).WaitForAsync();
        await Field(page, "Overall observations").FillAsync("Tasting retained during batch correction");
        await InteractiveButton(page, "Save evaluation").ClickAsync();
        await page.GetByRole(AriaRole.Alert).Filter(new() { HasText = "A tasting cannot be earlier" }).WaitForAsync();
        await InteractiveButton(page, "Correct batch").Nth(1).ClickAsync();
        await Field(page, "Date made").FillAsync("2001-10-02");
        await Field(page, "What happened during preparation?").FillAsync("Actually aged 18 hours");
        await InteractiveButton(page, "Save batch correction").ClickAsync();
        await page.GetByRole(AriaRole.Alert).Filter(new() { HasText = "Explain what" }).WaitForAsync();
        await page.GetByLabel("Why are you correcting this batch?", new() { Exact = false }).And(page.Locator("textarea:visible")).FillAsync("Mistyped batch date");
        await AttemptNavigationAsync(page, () => InteractiveButton(page, "Cancel batch correction").ClickAsync(), false, message: Message);
        (await Field(page, "Date made").InputValueAsync()).ShouldBe("2001-10-02");
        await InteractiveButton(page, "Save batch correction").ClickAsync();
        await page.GetByRole(AriaRole.Heading, new() { Name = "Batch 2 · Oct 2, 2001", Exact = true }).WaitForAsync();
        (await Field(page, "Overall observations").InputValueAsync()).ShouldBe("Tasting retained during batch correction");
        await AttemptNavigationAsync(page, () => page.GetByRole(AriaRole.Link, new() { Name = "← Version details", Exact = true }).ClickAsync(), false, message: Message);
        await InteractiveButton(page, "Save evaluation").ClickAsync();
        await page.Locator(".evaluation-entry").Filter(new() { HasText = "Tasting retained during batch correction" }).WaitForAsync();
        await page.GetByRole(AriaRole.Link, new() { Name = "← Version details", Exact = true }).ClickAsync();
        await page.WaitForURLAsync(details);
        await page.Locator(".batch-history").Nth(1).WaitForAsync();
        (await page.Locator(".batch-history").CountAsync()).ShouldBe(2);
        await page.Locator(".batch-history > summary").Nth(1).ClickAsync();
        await page.Locator(".batch-correction > summary").ClickAsync();
        var audit = await page.Locator(".batch-correction").InnerTextAsync();
        audit.ShouldContain("Mistyped batch date");
        audit.ShouldContain("Oct 3, 2001");
        audit.ShouldContain("Wrong date, right batch");
        (await page.Locator(".batch-history").Nth(1).InnerTextAsync()).ShouldContain("Actually aged 18 hours");
        await page.ReloadAsync();
        (await page.Locator(".batch-history").CountAsync()).ShouldBe(2);
    }
}
