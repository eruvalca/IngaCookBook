using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace IngaCookBook.UI.Features.Notebook;

internal sealed class EditorNavigationInterop(IJSRuntime runtime) : IAsyncDisposable
{
    internal const string ModulePath = "./_content/IngaCookBook.UI/Features/Notebook/Pages/VersionEditor.razor.js";
    private IJSObjectReference? _module;
    private ElementReference _editor;

    internal async Task UpdateAsync(ElementReference editor, bool dirty, int savedRevision, CancellationToken cancellationToken)
    {
        _editor = editor;
        _module ??= await runtime.InvokeAsync<IJSObjectReference>("import", cancellationToken, ModulePath);
        await _module.InvokeVoidAsync("updateGuard", cancellationToken, editor, dirty, savedRevision);
        cancellationToken.ThrowIfCancellationRequested();
    }

    internal async Task<bool> ConfirmDiscardAsync(CancellationToken cancellationToken)
    {
        _module ??= await runtime.InvokeAsync<IJSObjectReference>("import", cancellationToken, ModulePath);
        var discard = await _module.InvokeAsync<bool>("confirmDiscard", cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return discard;
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try
            {
                // Cleanup must not inherit the already-canceled component lifetime.
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await _module.InvokeVoidAsync("disposeGuard", cleanup.Token, _editor);
            }
            catch (JSDisconnectedException) { /* The circuit already ended. */ }
            catch (OperationCanceledException) { /* Bound cleanup when the browser no longer replies. */ }
            try { await _module.DisposeAsync(); }
            catch (JSDisconnectedException) { /* The circuit already ended. */ }
            catch (OperationCanceledException) { /* The JS runtime bounded its disposal call. */ }
        }
    }
}
