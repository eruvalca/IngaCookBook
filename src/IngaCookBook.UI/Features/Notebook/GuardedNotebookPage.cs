using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;

namespace IngaCookBook.UI.Features.Notebook;

/// <summary>Shared unsaved-input protection for interactive notebook forms.</summary>
public abstract class GuardedNotebookPage : NotebookPage
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

    protected async Task AcceptChangesAsync(CancellationToken cancellationToken)
    {
        CaptureSavedState();
        if (_navigation is not null)
        {
            // Programmatic navigation can run before the next render.
            await _navigation.UpdateAsync(Editor, false, _savedRevision, cancellationToken);
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        _navigation ??= new EditorNavigationInterop(JavaScript);
        await _navigation.UpdateAsync(Editor, Dirty, _savedRevision, LifetimeToken);
        if (!_guardReady)
        {
            _guardReady = true;
            StateHasChanged();
        }
    }

    protected async Task BeforeNavigateAsync(LocationChangingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (_navigation is not null && !await _navigation.ConfirmDiscardAsync(LifetimeToken))
        {
            context.PreventNavigation();
        }
    }

    protected override async ValueTask DisposeCoreAsync()
    {
        await base.DisposeCoreAsync();
        if (_navigation is not null)
        {
            await _navigation.DisposeAsync();
        }
    }
}
