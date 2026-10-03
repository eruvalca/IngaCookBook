using System.Diagnostics.CodeAnalysis;
using IngaCookBook.SharedKernel.Notebook;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace IngaCookBook.UI.Features.Notebook.Components;

public sealed partial class PhotoUpload
{
    [Parameter, EditorRequired] public Guid RecipeId { get; set; }
    [Parameter, EditorRequired] public Guid VersionId { get; set; }
    private readonly string _inputId = $"photo-{Guid.NewGuid():N}";
    private int _uploaded;

    [SuppressMessage("Security", "S5693:Limit the content length of HTTP requests", Justification = "Recipe photos have an explicit 10 MiB cap in both the browser stream and authenticated server handler.")]
    private Task UploadAsync(InputFileChangeEventArgs args) => RunAsync(async () =>
    {
        _uploaded = 0;
        foreach (var file in args.GetMultipleFiles(int.MaxValue))
        {
            if (file.Size > 10 * 1024 * 1024)
            {
                Error = $"{file.Name} is larger than 10 MB. {_uploaded} photos have already been saved.";
                Status = null;
                return;
            }
            await using var stream = file.OpenReadStream(10 * 1024 * 1024);
            NotebookChange result;
            try
            {
                result = await Notebook.UploadPhotoAsync(RecipeId, VersionId, stream, file.Name, file.ContentType);
            }
            catch (HttpRequestException)
            {
                Status = null;
                Error = $"We couldn't confirm this upload. {_uploaded} earlier photos were saved. Reload to check the current photo before trying again.";
                return;
            }
            if (!Saved(result))
            {
                Error = $"{Error} {_uploaded} photos have already been saved.";
                return;
            }
            _uploaded++;
        }
        Navigation.Refresh();
    });
}
