using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace IngaCookBook.Features.Notebook.Services;

internal sealed class AzureRecipePhotoStore(BlobServiceClient client) : IRecipePhotoStore
{
    private const string ContainerName = "recipe-photos";

    public async Task SaveAsync(string key, Stream content, string contentType, CancellationToken cancellationToken)
    {
        var container = client.GetBlobContainerClient(ContainerName);
        // Avoid a routine ContainerAlreadyExists response on every subsequent photo.
        // CreateIfNotExists still handles two first uploads racing to provision it.
        if (!(await container.ExistsAsync(cancellationToken)).Value)
        {
            await container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);
        }
        await container.GetBlobClient(key).UploadAsync(content,
            new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = contentType } }, cancellationToken);
    }

    public async Task<Stream> OpenAsync(string key, CancellationToken cancellationToken) =>
        await client.GetBlobContainerClient(ContainerName).GetBlobClient(key).OpenReadAsync(cancellationToken: cancellationToken);

    public async Task DeleteAsync(string key, CancellationToken cancellationToken) =>
        await client.GetBlobContainerClient(ContainerName).GetBlobClient(key).DeleteIfExistsAsync(cancellationToken: cancellationToken);

    public async Task DeletePrefixAsync(string prefix, CancellationToken cancellationToken)
    {
        // Prefixes are created internally from GUID paths, never supplied by HTTP clients.
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        var container = client.GetBlobContainerClient(ContainerName);
        if (!(await container.ExistsAsync(cancellationToken)).Value)
        {
            return;
        }
        await foreach (var blob in container.GetBlobsAsync(BlobTraits.None, BlobStates.None, prefix, cancellationToken))
        {
            await container.GetBlobClient(blob.Name).DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: cancellationToken);
        }
    }
}
