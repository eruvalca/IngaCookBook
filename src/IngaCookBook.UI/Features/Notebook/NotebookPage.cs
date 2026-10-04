using System.Diagnostics.CodeAnalysis;
using IngaCookBook.SharedKernel.Notebook;
using Microsoft.AspNetCore.Components;

namespace IngaCookBook.UI.Features.Notebook;

/// <summary>Shared loading and save-state handling for notebook pages in either renderer.</summary>
public abstract class NotebookPage : CancellableComponentBase
{
    [SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "ExecuteAsync owns and disposes each operation source after its action completes; disposal cancels it through LifetimeToken.")]
    private CancellationTokenSource? _operation;
    [CascadingParameter(Name = "RequestAborted")] public CancellationToken RequestAborted { get; set; }
    [Inject] protected INotebookService Notebook { get; set; } = default!;
    [Inject] protected NavigationManager Navigation { get; set; } = default!;
    protected string? Error { get; set; }
    protected string? Status { get; set; }
    protected bool Busy { get; set; }
    protected bool Disabled => Busy || !RendererInfo.IsInteractive;

    protected virtual TimeSpan LoadTimeout => TimeSpan.FromSeconds(30);
    protected virtual TimeSpan SaveTimeout => TimeSpan.FromMinutes(2);

    protected Task LoadAsync(Func<CancellationToken, Task> action) => ExecuteAsync(action, LoadTimeout, saving: false);
    protected Task RunAsync(Func<CancellationToken, Task> action) => ExecuteAsync(action, SaveTimeout, saving: true);

    // A collaborator may finish despite cancellation. Check before assigning its result
    // or navigating so an old route/load cannot overwrite the current page.
    protected static async Task<T> ReceiveAsync<T>(Task<T> pending, CancellationToken cancellationToken)
    {
        var result = await pending;
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }

    private async Task ExecuteAsync(Func<CancellationToken, Task> action, TimeSpan timeout, bool saving)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (IsDisposed) { return; }
        // The server supplies this cascade for static SSR/prerendering. A circuit's
        // original HTTP request is NOT the lifetime of its interactive components.
        var requestToken = RendererInfo.IsInteractive ? CancellationToken.None : RequestAborted;
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(LifetimeToken, requestToken);
        var previous = _operation;
        _operation = operation;
        operation.CancelAfter(timeout);
        Busy = true;
        Error = null;
        Status = null;
        try
        {
            if (previous is not null) { await previous.CancelAsync(); }
            operation.Token.ThrowIfCancellationRequested();
            await action(operation.Token);
        }
        catch (OperationCanceledException) when (IsDisposed || requestToken.IsCancellationRequested || _operation != operation)
        {
            // Leaving this page or replacing a load is expected, not a save failure.
        }
        catch (OperationCanceledException exception) when (operation.IsCancellationRequested || exception.InnerException is TimeoutException)
        {
            Status = null;
            Error = saving
                ? "Saving or refreshing took too long. Your inputs are still here. Check the notebook before trying again; the save may have completed."
                : "Loading took too long. Check your connection and reload this page.";
        }
        catch (HttpRequestException)
        {
            if (!IsDisposed && _operation == operation)
            {
                Status = null;
                Error = saving
                    ? "We couldn't confirm the save. Your inputs are still here. Check the notebook before trying again; the save may have completed."
                    : "We couldn't reach your notebook. Check your connection and reload this page.";
            }
        }
        finally
        {
            if (_operation == operation)
            {
                _operation = null;
                Busy = false;
            }
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
