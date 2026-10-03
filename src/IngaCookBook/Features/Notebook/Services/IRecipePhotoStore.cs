namespace IngaCookBook.Features.Notebook.Services;

internal interface IRecipePhotoStore
{
    Task SaveAsync(string key, Stream content, string contentType, CancellationToken cancellationToken);
    Task<Stream> OpenAsync(string key, CancellationToken cancellationToken);
    Task DeleteAsync(string key, CancellationToken cancellationToken);
    Task DeletePrefixAsync(string prefix, CancellationToken cancellationToken);
}
