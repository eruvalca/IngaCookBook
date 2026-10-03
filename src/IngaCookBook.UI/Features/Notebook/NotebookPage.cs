using IngaCookBook.SharedKernel.Notebook;
using Microsoft.AspNetCore.Components;

namespace IngaCookBook.UI.Features.Notebook;

/// <summary>Shared loading and save-state handling for notebook pages in either renderer.</summary>
public abstract class NotebookPage : ComponentBase
{
    [Inject] protected INotebookService Notebook { get; set; } = default!;
    [Inject] protected NavigationManager Navigation { get; set; } = default!;
    protected string? Error { get; set; }
    protected string? Status { get; set; }
    protected bool Busy { get; set; }
    protected bool Disabled => Busy || !RendererInfo.IsInteractive;

    protected async Task RunAsync(Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        Busy = true;
        Error = null;
        Status = null;
        try
        {
            await action();
        }
        catch (HttpRequestException)
        {
            Error = "We couldn't reach your notebook. Your inputs are still here. Check your connection and try again.";
        }
        finally
        {
            Busy = false;
        }
    }

    protected bool Saved(NotebookChange result)
    {
        switch (result)
        {
            case ChangeSaved:
                Status = "Saved to your notebook.";
                return true;
            case ChangeRejected rejected:
                Error = rejected.Message;
                Status = null;
                return false;
            default:
                throw new InvalidOperationException("Unknown notebook change result.");
        }
    }
}
