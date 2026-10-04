using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using IngaCookBook.Client.Services;
using IngaCookBook.SharedKernel.Notebook;
using Shouldly;
using Xunit;

namespace IngaCookBook.UnitTests.Features.Notebook;

[SuppressMessage("Maintainability", "CA1515:Consider making public types internal", Justification = "xUnit requires public test classes for discovery.")]
public sealed class HttpNotebookResponseTests
{
    [Theory]
    [InlineData(401, 401, "Sign in again")]
    [InlineData(403, 403, "Sign in again")]
    [InlineData(413, 413, "smaller than 10 MB")]
    [InlineData(500, 503, "Your inputs are still here")]
    [InlineData(502, 503, "Your inputs are still here")]
    public async Task NonJsonServerFailuresBecomeActionableRejections(int status, int expectedStatus, string message)
    {
        using var handler = new ResponseHandler((_, _) => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status)
        {
            Content = new StringContent("<html>Server error</html>"),
        }));
        using var http = Client(handler);

        var result = await new HttpNotebookService(http).CreateRecipeAsync(new("Vanilla", "", []), TestContext.Current.CancellationToken);

        var rejected = result.ShouldBeOfType<ChangeRejected>();
        rejected.Status.ShouldBe(expectedStatus);
        rejected.Message.ShouldContain(message);
    }

    [Theory]
    [InlineData(400)]
    [InlineData(404)]
    [InlineData(409)]
    public async Task ExpectedRejectionsPreserveTheServersMessageAndStatus(int status)
    {
        var rejection = new ChangeRejected("Reload the current version before editing.", status);
        using var handler = new ResponseHandler((_, _) => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status)
        {
            Content = JsonContent.Create<NotebookChange>(rejection),
        }));
        using var http = Client(handler);

        var result = await new HttpNotebookService(http).CreateRecipeAsync(new("Vanilla", "", []), TestContext.Current.CancellationToken);

        result.ShouldBe(rejection);
    }

    [Fact]
    public async Task SuccessfulWriteUsesAntiforgeryAndPreservesPayloadAndCreatedId()
    {
        var created = Guid.NewGuid();
        var payload = new NewRecipeRequest("Vanilla & honey", "First attempt", ["Texture", "Flavor"]);
        var writes = 0;
        using var handler = new ResponseHandler(async (request, ct) =>
        {
            writes++;
            request.Method.ShouldBe(HttpMethod.Post);
            request.RequestUri!.AbsolutePath.ShouldBe("/api/notebook/recipes");
            request.Headers.GetValues("RequestVerificationToken").ShouldBe(["test-antiforgery"]);
            var sent = await request.Content!.ReadFromJsonAsync<NewRecipeRequest>(ct);
            sent.ShouldNotBeNull();
            sent.Name.ShouldBe(payload.Name);
            sent.Description.ShouldBe(payload.Description);
            sent.Metrics.ShouldBe(payload.Metrics);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create<NotebookChange>(new ChangeSaved(created)) };
        });
        using var http = Client(handler);

        var result = await new HttpNotebookService(http).CreateRecipeAsync(payload, TestContext.Current.CancellationToken);

        result.ShouldBe(new ChangeSaved(created));
        writes.ShouldBe(1);
    }

    [Fact]
    public async Task MissingConfirmationDoesNotReportASuccessfulSave()
    {
        using var handler = new ResponseHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create<NotebookChange?>(null),
        }));
        using var http = Client(handler);

        var result = await new HttpNotebookService(http).CreateRecipeAsync(new("Vanilla", "", []), TestContext.Current.CancellationToken);

        var rejected = result.ShouldBeOfType<ChangeRejected>();
        rejected.Status.ShouldBe(503);
        rejected.Message.ShouldContain("Reload before retrying");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RecipeLookupDistinguishesMissingFromUnavailable(bool missing)
    {
        var recipeId = Guid.NewGuid();
        using var handler = new ResponseHandler((request, _) =>
        {
            request.Method.ShouldBe(HttpMethod.Get);
            request.RequestUri!.AbsolutePath.ShouldBe($"/api/notebook/recipes/{recipeId}");
            return Task.FromResult(new HttpResponseMessage(missing ? HttpStatusCode.NotFound : HttpStatusCode.ServiceUnavailable));
        });
        using var http = Client(handler);
        var service = new HttpNotebookService(http);

        if (missing)
        {
            (await service.GetRecipeAsync(recipeId, TestContext.Current.CancellationToken)).ShouldBeNull();
        }
        else
        {
            var error = await Should.ThrowAsync<HttpRequestException>(() => service.GetRecipeAsync(recipeId, TestContext.Current.CancellationToken));
            error.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        }
    }

    [Fact]
    public async Task RecipeLookupDeserializesSavedVersionsAndBlankScores()
    {
        var metric = new EvaluationMetric(Guid.NewGuid(), "Texture");
        var version = new RecipeVersion
        {
            Number = 2,
            Content = new() { Label = "Vanilla & honey", Notes = "Chill\nThen churn" },
            Batches = [new(Guid.NewGuid(), DateTimeOffset.UtcNow, "", [new(Guid.NewGuid(), DateTimeOffset.UtcNow, "", "", [new(metric.Id, null, "Not tasted yet")])])],
        };
        var recipe = new RecipeDocument { Metrics = [metric], Versions = [version], StandardVersionId = version.Id };
        using var handler = new ResponseHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(recipe),
        }));
        using var http = Client(handler);

        var result = (await new HttpNotebookService(http).GetRecipeAsync(recipe.Id, TestContext.Current.CancellationToken)).ShouldNotBeNull();

        result.Id.ShouldBe(recipe.Id);
        result.Revision.ShouldBe(recipe.Revision);
        result.StandardVersionId.ShouldBe(version.Id);
        result.Metrics.Single().ShouldBe(metric);
        result.Versions.Single().Id.ShouldBe(version.Id);
        result.Versions[0].Content.Label.ShouldBe("Vanilla & honey");
        result.Versions[0].Content.Notes.ShouldBe("Chill\nThen churn");
        result.Versions[0].Batches.Single().Evaluations.Single().Scores.Single().ShouldBe(new MetricScore(metric.Id, null, "Not tasted yet"));
    }

    [Fact]
    public async Task NullRecipeListIsAnEmptyNotebook()
    {
        using var handler = new ResponseHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create<RecipeDocument[]?>(null),
        }));
        using var http = Client(handler);

        (await new HttpNotebookService(http).GetRecipesAsync(TestContext.Current.CancellationToken)).ShouldBeEmpty();
    }

    [Fact]
    public async Task UploadSendsOriginalPhotoBytesAndMetadataToTheSelectedVersion()
    {
        var recipe = Guid.NewGuid();
        var version = Guid.NewGuid();
        var photo = Guid.NewGuid();
        byte[] bytes = [137, 80, 78, 71, 13, 10, 26, 10];
        using var handler = new ResponseHandler(async (request, ct) =>
        {
            request.Method.ShouldBe(HttpMethod.Post);
            request.RequestUri!.AbsolutePath.ShouldBe($"/api/notebook/recipes/{recipe}/versions/{version}/photos");
            request.Headers.GetValues("RequestVerificationToken").ShouldBe(["test-antiforgery"]);
            var form = request.Content.ShouldBeOfType<MultipartFormDataContent>();
            var file = form.Single();
            file.Headers.ContentDisposition!.Name.ShouldBe("file");
            file.Headers.ContentDisposition.FileName.ShouldBe("vanilla.png");
            file.Headers.ContentType!.MediaType.ShouldBe("image/png");
            (await file.ReadAsByteArrayAsync(ct)).ShouldBe(bytes);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create<NotebookChange>(new ChangeSaved(photo)) };
        });
        using var http = Client(handler);
        await using var body = new MemoryStream(bytes);

        var result = await new HttpNotebookService(http).UploadPhotoAsync(recipe, version, body, "vanilla.png", "image/png", TestContext.Current.CancellationToken);

        result.ShouldBe(new ChangeSaved(photo));
    }

    private static HttpClient Client(HttpMessageHandler handler) => new(handler) { BaseAddress = new Uri("https://notebook.test/") };

    private sealed class ResponseHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/token", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create("test-antiforgery") };
            }
            return await respond(request, cancellationToken);
        }
    }
}
