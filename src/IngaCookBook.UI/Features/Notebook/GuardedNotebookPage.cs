using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;

namespace IngaCookBook.UI.Features.Notebook;

/// <summary>Shared unsaved-input protection for interactive notebook forms.</summary>
public abstract class GuardedNotebookPage : NotebookPage, IAsyncDisposable
{
    [Inject] private IJSRuntime JavaScript { get; set; } = default!;
    private EditorNavigationInterop? _navigation;
    private string? _savedState;
    private int _savedRevision;
    private bool _guardReady;
    protected ElementReference Editor { get; set; }
    protected abstract string FormState { get; }
    protected bool FormDisabled => Disabled || !_guardReady;
    private bool Dirty => _savedState is not null && !string.Equals(_savedState, FormState, StringComparison.Ordinal);

    protected void CaptureSavedState()
    {
        _savedState = FormState;
        _savedRevision++;
    }

    protected async Task AcceptChangesAsync()
    {
        CaptureSavedState();
        if (_navigation is not null)
        {
            // Programmatic navigation can run before the next render.
            await _navigation.UpdateAsync(Editor, false, _savedRevision);
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        _navigation ??= new EditorNavigationInterop(JavaScript);
        await _navigation.UpdateAsync(Editor, Dirty, _savedRevision);
        if (!_guardReady)
        {
            _guardReady = true;
            StateHasChanged();
        }
    }

    protected async Task BeforeNavigateAsync(LocationChangingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (_navigation is not null && !await _navigation.ConfirmDiscardAsync())
        {
            context.PreventNavigation();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_navigation is not null)
        {
            await _navigation.DisposeAsync();
        }
        GC.SuppressFinalize(this);
    }
}
