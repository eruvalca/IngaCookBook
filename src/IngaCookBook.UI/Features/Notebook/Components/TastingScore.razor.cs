using System.Globalization;
using Microsoft.AspNetCore.Components;

namespace IngaCookBook.UI.Features.Notebook.Components;

public sealed partial class TastingScore
{
    private static readonly string[] _values = Enumerable.Range(1, 10).Select(n => n.ToString(CultureInfo.InvariantCulture)).ToArray();
    [Parameter, EditorRequired] public string Name { get; set; } = "";
    [Parameter, EditorRequired] public string Value { get; set; } = "";
    [Parameter] public EventCallback<string> ValueChanged { get; set; }
    [Parameter] public string Notes { get; set; } = "";
    [Parameter] public EventCallback<string> NotesChanged { get; set; }
    [Parameter] public bool Disabled { get; set; }
    private bool Valid => string.IsNullOrWhiteSpace(Value) || int.TryParse(Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var score) && score is >= 1 and <= 10;
    private Task ClearAsync() => ValueChanged.InvokeAsync("");
}
