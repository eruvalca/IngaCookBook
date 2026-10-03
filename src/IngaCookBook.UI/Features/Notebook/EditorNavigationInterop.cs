using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace IngaCookBook.UI.Features.Notebook;

internal sealed class EditorNavigationInterop(IJSRuntime runtime) : IAsyncDisposable
{
    internal const string ModulePath = "./_content/IngaCookBook.UI/Features/Notebook/Pages/VersionEditor.razor.js";
    private IJSObjectReference? _module;
    private ElementReference _editor;

    internal async Task UpdateAsync(ElementReference editor, bool dirty, int savedRevision)
    {
        _editor = editor;
        _module ??= await runtime.InvokeAsync<IJSObjectReference>("import", ModulePath);
        await _module.InvokeVoidAsync("updateGuard", editor, dirty, savedRevision);
    }

    internal async Task<bool> ConfirmDiscardAsync()
    {
        _module ??= await runtime.InvokeAsync<IJSObjectReference>("import", ModulePath);
        return await _module.InvokeAsync<bool>("confirmDiscard");
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try
            {
                await _module.InvokeVoidAsync("disposeGuard", _editor);
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException) { /* The circuit already ended. */ }
        }
    }
}
