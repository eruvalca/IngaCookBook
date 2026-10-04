using IngaCookBook.Features.Notebook.Services;
using IngaCookBook.SharedKernel.Notebook;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace IngaCookBook.Features.Notebook.Endpoints;

internal static class NotebookEndpoints
{
    extension(IEndpointRouteBuilder endpoints)
    {
        internal void MapNotebookEndpoints()
        {
            var group = endpoints.MapGroup("/api/notebook").RequireAuthorization().DisableCookieRedirect().WithTags("Recipe notebook");
            group.AddEndpointFilter(async (context, next) =>
            {
                context.HttpContext.Response.Headers.CacheControl = "no-cache, no-store";
                context.HttpContext.Response.Headers.Pragma = "no-cache";
                if (!HttpMethods.IsGet(context.HttpContext.Request.Method))
                {
                    try
                    {
                        await context.HttpContext.RequestServices.GetRequiredService<IAntiforgery>()
                            .ValidateRequestAsync(context.HttpContext);
                    }
                    catch (AntiforgeryValidationException)
                    {
                        return Reply(new ChangeRejected("Your session needs refreshing. Reload this page and try again.", 400));
                    }
                }
                return await next(context);
            });
            group.MapGet("/token", (HttpContext context, IAntiforgery antiforgery) =>
                TypedResults.Ok(antiforgery.GetAndStoreTokens(context).RequestToken)).WithSummary("Issue an authenticated form token");
            group.MapGet("/workspace", async (INotebookService service, CancellationToken ct) => TypedResults.Json(await service.GetWorkspaceAsync(ct)));
            group.MapPost("/workspace", async (WorkspaceRequest request, INotebookService service, CancellationToken ct) =>
                Reply(await service.CreateWorkspaceAsync(request, ct), "/api/notebook/workspace", appendId: false));
            group.MapGet("/recipes", (INotebookService service, CancellationToken ct) => service.GetRecipesAsync(ct));
            group.MapGet("/recipes/{recipeId:guid}", async Task<Results<Ok<RecipeDocument>, NotFound>> (Guid recipeId, INotebookService service, CancellationToken ct) =>
                await service.GetRecipeAsync(recipeId, ct) is { } recipe ? TypedResults.Ok(recipe) : TypedResults.NotFound());
            group.MapPost("/recipes", async (NewRecipeRequest request, INotebookService service, CancellationToken ct) =>
                Reply(await service.CreateRecipeAsync(request, ct), "/api/notebook/recipes"));
            group.MapPut("/recipes/{recipeId:guid}/settings", async (Guid recipeId, RecipeSettingsRequest request, INotebookService service, CancellationToken ct) =>
                Reply(await service.SaveSettingsAsync(recipeId, request, ct)));
            MapVersions(group);
            MapPhotos(group);
        }
    }

    private static void MapVersions(RouteGroupBuilder group)
    {
        var versions = group.MapGroup("/recipes/{recipeId:guid}/versions/{versionId:guid}");
        versions.MapDelete("/", async (Guid recipeId, Guid versionId, [FromBody] RevisionRequest request, INotebookService service, CancellationToken ct) =>
            Reply(await service.DeleteDraftAsync(recipeId, versionId, request, ct)));
        versions.MapPut("/batches/{batchId:guid}", async (Guid recipeId, Guid versionId, Guid batchId, BatchCorrectionRequest request, INotebookService service, CancellationToken ct) =>
            Reply(await service.CorrectBatchAsync(recipeId, versionId, batchId, request, ct)));
        versions.MapGet("/", async Task<Results<Ok<RecipeVersion>, NotFound>> (Guid recipeId, Guid versionId, INotebookService service, CancellationToken ct) =>
            (await service.GetRecipeAsync(recipeId, ct))?.Versions.FirstOrDefault(v => v.Id == versionId) is { } version
                ? TypedResults.Ok(version) : TypedResults.NotFound());
        versions.MapGet("/batches/{batchId:guid}", async Task<Results<Ok<RecipeBatch>, NotFound>> (Guid recipeId, Guid versionId, Guid batchId, INotebookService service, CancellationToken ct) =>
            (await service.GetRecipeAsync(recipeId, ct))?.Versions.FirstOrDefault(v => v.Id == versionId)?.Batches.FirstOrDefault(b => b.Id == batchId) is { } batch
                ? TypedResults.Ok(batch) : TypedResults.NotFound());
        versions.MapGet("/batches/{batchId:guid}/evaluations/{evaluationId:guid}", async Task<Results<Ok<BatchEvaluation>, NotFound>> (Guid recipeId, Guid versionId, Guid batchId, Guid evaluationId, INotebookService service, CancellationToken ct) =>
            (await service.GetRecipeAsync(recipeId, ct))?.Versions.FirstOrDefault(v => v.Id == versionId)?.Batches.FirstOrDefault(b => b.Id == batchId)?.Evaluations.FirstOrDefault(e => e.Id == evaluationId) is { } evaluation
                ? TypedResults.Ok(evaluation) : TypedResults.NotFound());
        versions.MapPut("/", async (Guid recipeId, Guid versionId, VersionRequest request, INotebookService service, CancellationToken ct) =>
            Reply(await service.SaveVersionAsync(recipeId, versionId, request, ct)));
        versions.MapPost("/variations", async (Guid recipeId, Guid versionId, RevisionRequest request, INotebookService service, CancellationToken ct) =>
            Reply(await service.VaryAsync(recipeId, versionId, request, ct), $"/api/notebook/recipes/{recipeId}/versions"));
        versions.MapPost("/standard", async (Guid recipeId, Guid versionId, RevisionRequest request, INotebookService service, CancellationToken ct) =>
            Reply(await service.SetStandardAsync(recipeId, versionId, request, ct)));
        versions.MapPost("/promote", async (Guid recipeId, Guid versionId, PromotionRequest request, INotebookService service, CancellationToken ct) =>
            Reply(await service.PromoteAsync(recipeId, versionId, request, ct), "/api/notebook/recipes"));
        versions.MapPost("/batches", async (Guid recipeId, Guid versionId, BatchRequest request, INotebookService service, CancellationToken ct) =>
            Reply(await service.MakeBatchAsync(recipeId, versionId, request, ct), $"/api/notebook/recipes/{recipeId}/versions/{versionId}/batches"));
        versions.MapPost("/batches/{batchId:guid}/evaluations", async (Guid recipeId, Guid versionId, Guid batchId, EvaluationRequest request, INotebookService service, CancellationToken ct) =>
            Reply(await service.EvaluateAsync(recipeId, versionId, batchId, request, ct),
                $"/api/notebook/recipes/{recipeId}/versions/{versionId}/batches/{batchId}/evaluations"));
        versions.MapPut("/batches/{batchId:guid}/evaluations/{evaluationId:guid}", async (Guid recipeId, Guid versionId, Guid batchId, Guid evaluationId, EvaluationCorrectionRequest request, INotebookService service, CancellationToken ct) =>
            Reply(await service.CorrectEvaluationAsync(recipeId, versionId, batchId, evaluationId, request, ct)));
    }

    private static void MapPhotos(RouteGroupBuilder group)
    {
        group.MapPost("/recipes/{recipeId:guid}/versions/{versionId:guid}/photos",
            async (Guid recipeId, Guid versionId, [FromForm] IFormFile file, INotebookService service, CancellationToken ct) =>
            {
                if (file.Length > NotebookService.MaximumPhotoBytes)
                {
                    return Reply(new ChangeRejected("Choose a photo smaller than 10 MB.", 413));
                }
                await using var stream = file.OpenReadStream();
                return Reply(await service.UploadPhotoAsync(recipeId, versionId, stream, file.FileName, file.ContentType, ct),
                    $"/api/notebook/recipes/{recipeId}/versions/{versionId}/photos");
            });
        group.MapGet("/recipes/{recipeId:guid}/versions/{versionId:guid}/photos/{photoId:guid}",
            async Task<Results<FileStreamHttpResult, NotFound>> (Guid recipeId, Guid versionId, Guid photoId, NotebookService service, CancellationToken ct) =>
            {
                var photo = await service.OpenPhotoAsync(recipeId, versionId, photoId, ct);
                return photo is { } value ? TypedResults.Stream(value.Content, value.ContentType) : TypedResults.NotFound();
            });
    }

    private static IResult Reply(NotebookChange result, string? createdPath = null, bool appendId = true) => result switch
    {
        ChangeRejected rejected => TypedResults.Json<NotebookChange>(rejected, statusCode: rejected.Status),
        ChangeSaved saved when createdPath is not null => TypedResults.Created<NotebookChange>(new Uri(appendId ? $"{createdPath}/{saved.Id}" : createdPath, UriKind.Relative), saved),
        _ => TypedResults.Ok(result),
    };
}
