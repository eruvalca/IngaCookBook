namespace IngaCookBook.SharedKernel.Notebook;

/// <summary>A versioned ingredient, including optional purchase cost and pinned subrecipe.</summary>
public sealed record Ingredient
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "";
    public decimal? Quantity { get; init; }
    public string Unit { get; init; } = "g";
    public decimal? PurchaseQuantity { get; init; }
    public string PurchaseUnit { get; init; } = "g";
    public decimal? PurchasePrice { get; init; }
    public Guid? RecipeId { get; init; }
    public Guid? VersionId { get; init; }
    public RecipeContent? LinkedContent { get; init; }
}
