using System.Text.Json;
using IngaCookBook.SharedKernel.Notebook;

namespace IngaCookBook.UI.Features.Notebook.Pages;

public sealed partial class NewRecipe
{
    private static readonly string[] _starterMetrics = ["Flavor", "Creaminess", "Scoopability", "Sweetness satisfaction", "Overall satisfaction"];
    private string _name = "";
    private string _description = "";
    private bool _hasWorkspace;
    private bool _loaded;
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
        _metrics.Clear();
        _metrics.AddRange(_starterMetrics
            .Select(name => new MetricInput { Name = name }));
    }

    private Task CreateAsync() => RunAsync(async () =>
    {
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
