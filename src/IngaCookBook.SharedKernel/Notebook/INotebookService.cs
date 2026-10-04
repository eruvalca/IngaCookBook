namespace IngaCookBook.SharedKernel.Notebook;

/// <summary>Authenticated notebook operations, implemented locally for SSR and over HTTP in WebAssembly.</summary>
public interface INotebookService
{
    Task<WorkspaceView?> GetWorkspaceAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<RecipeDocument>> GetRecipesAsync(CancellationToken cancellationToken);
    Task<RecipeDocument?> GetRecipeAsync(Guid id, CancellationToken cancellationToken);
    Task<NotebookChange> CreateWorkspaceAsync(WorkspaceRequest request, CancellationToken cancellationToken);
    Task<NotebookChange> CreateRecipeAsync(NewRecipeRequest request, CancellationToken cancellationToken);
    Task<NotebookChange> SaveVersionAsync(Guid recipeId, Guid versionId, VersionRequest request, CancellationToken cancellationToken);
    Task<NotebookChange> DeleteDraftAsync(Guid recipeId, Guid versionId, RevisionRequest request, CancellationToken cancellationToken);
    Task<NotebookChange> VaryAsync(Guid recipeId, Guid versionId, RevisionRequest request, CancellationToken cancellationToken);
    Task<NotebookChange> MakeBatchAsync(Guid recipeId, Guid versionId, BatchRequest request, CancellationToken cancellationToken);
    Task<NotebookChange> CorrectBatchAsync(Guid recipeId, Guid versionId, Guid batchId, BatchCorrectionRequest request, CancellationToken cancellationToken);
    Task<NotebookChange> EvaluateAsync(Guid recipeId, Guid versionId, Guid batchId, EvaluationRequest request, CancellationToken cancellationToken);
    Task<NotebookChange> CorrectEvaluationAsync(Guid recipeId, Guid versionId, Guid batchId, Guid evaluationId, EvaluationCorrectionRequest request, CancellationToken cancellationToken);
    Task<NotebookChange> SetStandardAsync(Guid recipeId, Guid versionId, RevisionRequest request, CancellationToken cancellationToken);
    Task<NotebookChange> SaveSettingsAsync(Guid recipeId, RecipeSettingsRequest request, CancellationToken cancellationToken);
    Task<NotebookChange> PromoteAsync(Guid recipeId, Guid versionId, PromotionRequest request, CancellationToken cancellationToken);
    Task<NotebookChange> UploadPhotoAsync(Guid recipeId, Guid versionId, Stream content, string fileName, string contentType, CancellationToken cancellationToken);
}
