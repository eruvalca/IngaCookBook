namespace IngaCookBook.Features.Notebook.Data;

internal sealed class IngredientEntity
{
    public Guid Id { get; set; }
    public Guid VersionId { get; set; }
    public Guid RowId { get; set; }
    public int Position { get; set; }
    public string Name { get; set; } = "";
    public decimal? Quantity { get; set; }
    public string Unit { get; set; } = "g";
    public decimal? PurchaseQuantity { get; set; }
    public string PurchaseUnit { get; set; } = "g";
    public decimal? PurchasePrice { get; set; }
    public Guid? LinkedRecipeId { get; set; }
    public Guid? LinkedVersionId { get; set; }
    public string? LinkedSnapshot { get; set; }
}
