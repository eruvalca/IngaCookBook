namespace IngaCookBook.UI.Features.Notebook.Models;

internal static class NotebookText
{
    internal static string DifferenceValue(string value, string label) => value.StartsWith(label + ": ", StringComparison.Ordinal) ? value[(label.Length + 2)..] : value;
}
