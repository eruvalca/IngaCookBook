namespace IngaCookBook.UI.Features.Notebook.Models;

internal sealed class StepInput
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Instruction { get; set; } = "";
    public string Notes { get; set; } = "";
}
