using System.Text.Json;
using IngaCookBook.SharedKernel.Notebook;
using Microsoft.Playwright;
using Shouldly;
using Xunit;

namespace IngaCookBook.PlaywrightTests;

public sealed partial class NotebookWorkflowTests
{
    [Fact]
    public async Task ApiCreatedLocationsResolveAndOtherAccountsCannotReadTheNotebook()
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync();
        await using var owner = await browser.NewContextAsync(new() { BaseURL = application.Endpoint.ToString(), IgnoreHTTPSErrors = true });
        var page = await owner.NewPageAsync();
        await owner.Tracing.StartAsync(new() { Screenshots = true, Snapshots = true, Sources = true });
        var artifacts = Path.Combine(AppContext.BaseDirectory, "TestResults", $"notebook-api-{Guid.NewGuid():N}");
        Directory.CreateDirectory(artifacts);
        try
        {
            await RegisterAndCreateWorkspaceAsync(page);
            await using var tokenResponse = await owner.APIRequest.GetAsync("/api/notebook/token");
            tokenResponse.Status.ShouldBe(200);
            var token = JsonSerializer.Deserialize<string>(await tokenResponse.TextAsync()).ShouldNotBeNull();
            var headers = new Dictionary<string, string>(StringComparer.Ordinal) { ["RequestVerificationToken"] = token };
            await using var created = await owner.APIRequest.PostAsync("/api/notebook/recipes", new()
            {
                Headers = headers,
                DataObject = new NewRecipeRequest("API vanilla", "Owner only", ["Texture"]),
            });
            created.Status.ShouldBe(201);
            var recipePath = created.Headers["location"];
            var recipe = await ReadRecipeAsync(owner.APIRequest, recipePath);
            recipe.Name.ShouldBe("API vanilla");
            var version = recipe.Versions.Single();
            var versionPath = $"{recipePath}/versions/{version.Id}";
            await using var saved = await owner.APIRequest.PutAsync(versionPath, new()
            {
                Headers = headers,
                DataObject = new VersionRequest(recipe.Revision, new()
                {
                    Ingredients = [new Ingredient { Name = "Cream", Quantity = 100 }],
                    Steps = [new(Guid.NewGuid(), "Mix and chill", "")],
                }, null),
            });
            saved.Status.ShouldBe(200);
            recipe = await ReadRecipeAsync(owner.APIRequest, recipePath);
            var day = DateTimeOffset.UtcNow;
            await using var made = await owner.APIRequest.PostAsync(versionPath + "/batches", new()
            {
                Headers = headers,
                DataObject = new BatchRequest(recipe.Revision, day, "Chilled overnight"),
            });
            made.Status.ShouldBe(201);
            var batchPath = made.Headers["location"];
            recipe = await ReadRecipeAsync(owner.APIRequest, recipePath);
            await using var tasted = await owner.APIRequest.PostAsync(batchPath + "/evaluations", new()
            {
                Headers = headers,
                DataObject = new EvaluationRequest(recipe.Revision, day, "Smooth", "Age longer", [new(recipe.Metrics[0].Id, 8, "Good texture")]),
            });
            tasted.Status.ShouldBe(201);
            var evaluationPath = tasted.Headers["location"];
            await using var evaluation = await owner.APIRequest.GetAsync(evaluationPath);
            evaluation.Status.ShouldBe(200);
            var recorded = JsonSerializer.Deserialize<BatchEvaluation>(await evaluation.TextAsync(), JsonSerializerOptions.Web).ShouldNotBeNull();
            recorded.Notes.ShouldBe("Smooth");
            recorded.Scores.Single().Score.ShouldBe(8);
            await using var batch = await owner.APIRequest.GetAsync(batchPath);
            batch.Status.ShouldBe(200);
            JsonSerializer.Deserialize<RecipeBatch>(await batch.TextAsync(), JsonSerializerOptions.Web).ShouldNotBeNull().Notes.ShouldBe("Chilled overnight");
            await using var formulation = await owner.APIRequest.GetAsync(versionPath);
            formulation.Status.ShouldBe(200);
            JsonSerializer.Deserialize<RecipeVersion>(await formulation.TextAsync(), JsonSerializerOptions.Web).ShouldNotBeNull().IsLocked.ShouldBeTrue();

            await page.GotoAsync(versionPath.Replace("/api/notebook", "", StringComparison.Ordinal));
            await page.Locator("input[type=file]:not([disabled])").SetInputFilesAsync(new FilePayload
            {
                Name = "private-texture.png",
                MimeType = "image/png",
                Buffer = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a0S8AAAAASUVORK5CYII="),
            });
            await page.GetByRole(AriaRole.Img, new() { Name = "private-texture", Exact = true }).WaitForAsync();
            recipe = await ReadRecipeAsync(owner.APIRequest, recipePath);
            var photoPath = $"{versionPath}/photos/{recipe.Versions[0].Photos.Single().Id}";
            await using var photo = await owner.APIRequest.GetAsync(photoPath);
            photo.Status.ShouldBe(200);
            photo.Headers["content-type"].ShouldBe("image/png");

            await using var other = await browser.NewContextAsync(new() { BaseURL = application.Endpoint.ToString(), IgnoreHTTPSErrors = true });
            await using var anonymous = await browser.NewContextAsync(new() { BaseURL = application.Endpoint.ToString(), IgnoreHTTPSErrors = true });
            await RegisterAndCreateWorkspaceAsync(await other.NewPageAsync());
            foreach (var path in new[] { recipePath, versionPath, batchPath, evaluationPath, photoPath })
            {
                await using var forbidden = await other.APIRequest.GetAsync(path);
                forbidden.Status.ShouldBe(404, path);
                forbidden.Headers["cache-control"].ShouldBe("no-cache, no-store");
                await using var unauthenticated = await anonymous.APIRequest.GetAsync(path, new() { MaxRedirects = 0 });
                unauthenticated.Status.ShouldBe(401, path);
            }
            await using var accountPage = await anonymous.APIRequest.GetAsync("/Account/Manage", new() { MaxRedirects = 0 });
            accountPage.Status.ShouldBe(302);
            accountPage.Headers["location"].ShouldContain("/Account/Login");
            await using var otherRecipes = await other.APIRequest.GetAsync("/api/notebook/recipes");
            otherRecipes.Status.ShouldBe(200);
            JsonSerializer.Deserialize<RecipeDocument[]>(await otherRecipes.TextAsync(), JsonSerializerOptions.Web).ShouldNotBeNull().ShouldBeEmpty();
        }
        finally
        {
            await BrowserArtifacts.CaptureAsync(page, owner, artifacts, output.WriteLine);
            output.WriteLine($"Browser evidence: {artifacts}");
        }
    }

    private static async Task<RecipeDocument> ReadRecipeAsync(IAPIRequestContext http, string path)
    {
        await using var response = await http.GetAsync(path);
        response.Status.ShouldBe(200);
        response.Headers["cache-control"].ShouldBe("no-cache, no-store");
        return JsonSerializer.Deserialize<RecipeDocument>(await response.TextAsync(), JsonSerializerOptions.Web).ShouldNotBeNull();
    }
}
