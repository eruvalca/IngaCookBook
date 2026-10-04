using System.Text.Json;
using IngaCookBook.SharedKernel.Notebook;
using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components;

namespace IngaCookBook.UI.Features.Notebook.Pages;

public sealed partial class NewRecipe
{
    private static readonly string[] _starterMetrics = ["Flavor", "Creaminess", "Scoopability", "Sweetness satisfaction", "Overall satisfaction"];
    private string _name = "";
    private string _description = "";
    private bool _hasWorkspace;
    private bool _loaded;
    private FluentTextInput? _nameInput;
    private string? _nameError;
    private bool _focusName;
    private readonly List<MetricInput> _metrics = [new() { Name = "Overall satisfaction" }];
    protected override string FormState => JsonSerializer.Serialize(new { _name, _description, _metrics });

    protected override async Task OnInitializedAsync()
    {
        await RunAsync(async () => { _hasWorkspace = await Notebook.GetWorkspaceAsync() is not null; _loaded = true; });
        CaptureSavedState();
    }

    private void AddMetric() => _metrics.Add(new());

    private void UseIceCreamMetrics()
    {
        _metrics.AddRange(_starterMetrics
            .Where(name => !_metrics.Any(metric => string.Equals(metric.Name.Trim(), name, StringComparison.OrdinalIgnoreCase)))
            .Select(name => new MetricInput { Name = name }));
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);
        if (_focusName && _nameInput is not null)
        {
            _focusName = false;
            await _nameInput.Element.FocusAsync();
        }
    }

    private Task CreateAsync() => RunAsync(async () =>
    {
        _nameError = string.IsNullOrWhiteSpace(_name) ? "Enter a recipe name." : null;
        if (_nameError is not null)
        {
            _focusName = true;
            return;
        }
        var result = await Notebook.CreateRecipeAsync(new(_name, _description, _metrics.Select(m => m.Name).ToArray()));
        if (Saved(result) && result is ChangeSaved created)
        {
            var recipe = await Notebook.GetRecipeAsync(created.Id);
            await AcceptChangesAsync();
            Navigation.NavigateTo($"/recipes/{created.Id}/versions/{recipe!.Versions[0].Id}/edit");
        }
    });

    private sealed class MetricInput
    {
        public Guid Id { get; } = Guid.NewGuid();
        public string Name { get; set; } = "";
    }
}
