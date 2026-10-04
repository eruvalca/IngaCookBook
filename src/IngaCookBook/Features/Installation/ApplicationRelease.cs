using System.Security.Cryptography;
using System.Text;
using IngaCookBook.SharedKernel.Notebook;
using IngaCookBook.UI;

namespace IngaCookBook.Features.Installation;

/// <summary>A stable identity for this build, including browser assets, shared UI and server code.</summary>
internal sealed class ApplicationRelease
{
    public string Id { get; } = CreateId();

    private static string CreateId()
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var assembly in new[] { typeof(ApplicationRelease).Assembly, typeof(UiAssemblyMarker).Assembly, typeof(INotebookService).Assembly })
        {
            hash.AppendData(Encoding.UTF8.GetBytes(assembly.ManifestModule.ModuleVersionId.ToString()));
        }

        // The SDK emits this manifest for build and publish. It includes content
        // fingerprints, so a CSS/JavaScript-only release also produces an update.
        var manifest = Path.Combine(AppContext.BaseDirectory, "IngaCookBook.staticwebassets.endpoints.json");
        if (File.Exists(manifest))
        {
            hash.AppendData(File.ReadAllBytes(manifest));
        }
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }
}
