namespace IngaCookBook.Features.Notebook.Data;

internal sealed class StepEntity
{
    public Guid Id { get; set; }
    public Guid VersionId { get; set; }
    public Guid RowId { get; set; }
    public int Position { get; set; }
    public string Instruction { get; set; } = "";
    public string Notes { get; set; } = "";
}
