namespace IngaCookBook.SharedKernel.Notebook;

/// <summary>Authenticated notebook operations, implemented locally for SSR and over HTTP in WebAssembly.</summary>
public interface INotebookService
{
    Task<WorkspaceView?> GetWorkspaceAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RecipeDocument>> GetRecipesAsync(CancellationToken cancellationToken = default);
    Task<RecipeDocument?> GetRecipeAsync(Guid id, CancellationToken cancellationToken = default);
    Task<NotebookChange> CreateWorkspaceAsync(WorkspaceRequest request, CancellationToken cancellationToken = default);
    Task<NotebookChange> CreateRecipeAsync(NewRecipeRequest request, CancellationToken cancellationToken = default);
    Task<NotebookChange> SaveVersionAsync(Guid recipeId, Guid versionId, VersionRequest request, CancellationToken cancellationToken = default);
    Task<NotebookChange> VaryAsync(Guid recipeId, Guid versionId, RevisionRequest request, CancellationToken cancellationToken = default);
    Task<NotebookChange> MakeBatchAsync(Guid recipeId, Guid versionId, BatchRequest request, CancellationToken cancellationToken = default);
    Task<NotebookChange> CorrectBatchAsync(Guid recipeId, Guid versionId, Guid batchId, BatchCorrectionRequest request, CancellationToken cancellationToken = default);
    Task<NotebookChange> EvaluateAsync(Guid recipeId, Guid versionId, Guid batchId, EvaluationRequest request, CancellationToken cancellationToken = default);
    Task<NotebookChange> CorrectEvaluationAsync(Guid recipeId, Guid versionId, Guid batchId, Guid evaluationId, EvaluationCorrectionRequest request, CancellationToken cancellationToken = default);
    Task<NotebookChange> SetStandardAsync(Guid recipeId, Guid versionId, RevisionRequest request, CancellationToken cancellationToken = default);
    Task<NotebookChange> SaveSettingsAsync(Guid recipeId, RecipeSettingsRequest request, CancellationToken cancellationToken = default);
    Task<NotebookChange> PromoteAsync(Guid recipeId, Guid versionId, PromotionRequest request, CancellationToken cancellationToken = default);
    Task<NotebookChange> UploadPhotoAsync(Guid recipeId, Guid versionId, Stream content, string fileName, string contentType, CancellationToken cancellationToken = default);
}
