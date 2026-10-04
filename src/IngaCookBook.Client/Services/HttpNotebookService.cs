using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using IngaCookBook.SharedKernel.Notebook;

namespace IngaCookBook.Client.Services;

internal sealed class HttpNotebookService(HttpClient http) : INotebookService
{
    public Task<WorkspaceView?> GetWorkspaceAsync(CancellationToken cancellationToken = default) =>
        http.GetFromJsonAsync<WorkspaceView>("api/notebook/workspace", cancellationToken);

    public async Task<IReadOnlyList<RecipeDocument>> GetRecipesAsync(CancellationToken cancellationToken = default) =>
        await http.GetFromJsonAsync<RecipeDocument[]>("api/notebook/recipes", cancellationToken) ?? [];

    public async Task<RecipeDocument?> GetRecipeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync(new Uri($"api/notebook/recipes/{id}", UriKind.Relative), cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<RecipeDocument>(cancellationToken);
    }

    public Task<NotebookChange> CreateWorkspaceAsync(WorkspaceRequest request, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Post, "api/notebook/workspace", request, cancellationToken);

    public Task<NotebookChange> CreateRecipeAsync(NewRecipeRequest request, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Post, "api/notebook/recipes", request, cancellationToken);

    public Task<NotebookChange> SaveVersionAsync(Guid recipeId, Guid versionId, VersionRequest request, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Put, $"{VersionPath(recipeId, versionId)}/", request, cancellationToken);

    public Task<NotebookChange> VaryAsync(Guid recipeId, Guid versionId, RevisionRequest request, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Post, $"{VersionPath(recipeId, versionId)}/variations", request, cancellationToken);

    public Task<NotebookChange> DeleteDraftAsync(Guid recipeId, Guid versionId, RevisionRequest request, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, $"{VersionPath(recipeId, versionId)}/", request, cancellationToken);

    public Task<NotebookChange> MakeBatchAsync(Guid recipeId, Guid versionId, BatchRequest request, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Post, $"{VersionPath(recipeId, versionId)}/batches", request, cancellationToken);

    public Task<NotebookChange> CorrectBatchAsync(Guid recipeId, Guid versionId, Guid batchId, BatchCorrectionRequest request, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Put, $"{VersionPath(recipeId, versionId)}/batches/{batchId}", request, cancellationToken);

    public Task<NotebookChange> EvaluateAsync(Guid recipeId, Guid versionId, Guid batchId, EvaluationRequest request, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Post, $"{VersionPath(recipeId, versionId)}/batches/{batchId}/evaluations", request, cancellationToken);

    public Task<NotebookChange> SetStandardAsync(Guid recipeId, Guid versionId, RevisionRequest request, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Post, $"{VersionPath(recipeId, versionId)}/standard", request, cancellationToken);

    public Task<NotebookChange> CorrectEvaluationAsync(Guid recipeId, Guid versionId, Guid batchId, Guid evaluationId, EvaluationCorrectionRequest request, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Put, $"{VersionPath(recipeId, versionId)}/batches/{batchId}/evaluations/{evaluationId}", request, cancellationToken);

    public Task<NotebookChange> SaveSettingsAsync(Guid recipeId, RecipeSettingsRequest request, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Put, $"api/notebook/recipes/{recipeId}/settings", request, cancellationToken);

    public Task<NotebookChange> PromoteAsync(Guid recipeId, Guid versionId, PromotionRequest request, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Post, $"{VersionPath(recipeId, versionId)}/promote", request, cancellationToken);

    public async Task<NotebookChange> UploadPhotoAsync(Guid recipeId, Guid versionId, Stream content, string fileName, string contentType, CancellationToken cancellationToken = default)
    {
        using var form = new MultipartFormDataContent();
        using var file = new StreamContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "file", fileName);
        return await SendContentAsync(HttpMethod.Post, $"{VersionPath(recipeId, versionId)}/photos", form, cancellationToken);
    }

    private async Task<NotebookChange> SendAsync<T>(HttpMethod method, string path, T payload, CancellationToken cancellationToken)
    {
        using var content = JsonContent.Create(payload);
        return await SendContentAsync(method, path, content, cancellationToken);
    }

    private async Task<NotebookChange> SendContentAsync(HttpMethod method, string path, HttpContent content, CancellationToken cancellationToken)
    {
        var token = await http.GetFromJsonAsync<string>("api/notebook/token", cancellationToken);
        using var message = new HttpRequestMessage(method, path) { Content = content };
        message.Headers.Add("RequestVerificationToken", token);
        using var response = await http.SendAsync(message, cancellationToken);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return new ChangeRejected("Your session has expired. Sign in again.", (int)response.StatusCode);
        }
        if (response.StatusCode == HttpStatusCode.RequestEntityTooLarge)
        {
            return new ChangeRejected("This upload is too large. Choose a photo smaller than 10 MB.", 413);
        }
        if ((int)response.StatusCode >= 500)
        {
            return new ChangeRejected("The server could not save this change. Your inputs are still here; please try again.", 503);
        }
        return await response.Content.ReadFromJsonAsync<NotebookChange>(cancellationToken) ??
            new ChangeRejected("The server did not return a save confirmation. Reload before retrying.", 503);
    }

    private static string VersionPath(Guid recipeId, Guid versionId) => $"api/notebook/recipes/{recipeId}/versions/{versionId}";
}
