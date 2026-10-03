using IngaCookBook.SharedKernel.Notebook;

namespace IngaCookBook.UI.Features.Notebook.Models;

internal sealed class IngredientInput
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public decimal? Quantity { get; set; }
    public string Unit { get; set; } = "g";
    public decimal? PurchaseQuantity { get; set; }
    public decimal? PurchasePrice { get; set; }
    public string PurchaseUnit { get; set; } = "g";
    public string Link { get; set; } = "";
    public RecipeContent? LinkedContent { get; set; }

    public Ingredient ToIngredient()
    {
        var parts = Link.Split('/');
        var recipeId = parts.Length == 2 && Guid.TryParse(parts[0], out var recipe) ? recipe : (Guid?)null;
        var versionId = parts.Length == 2 && Guid.TryParse(parts[1], out var version) ? version : (Guid?)null;
        return new Ingredient
        {
            Id = Id,
            Name = Name,
            Quantity = Quantity,
            Unit = Unit,
            PurchaseQuantity = PurchaseQuantity,
            PurchasePrice = PurchasePrice,
            PurchaseUnit = PurchaseUnit,
            RecipeId = recipeId,
            VersionId = versionId,
            LinkedContent = LinkedContent,
        };
    }

    public static IngredientInput From(Ingredient ingredient) => new()
    {
        Id = ingredient.Id,
        Name = ingredient.Name,
        Quantity = ingredient.Quantity,
        Unit = ingredient.Unit,
        PurchaseQuantity = ingredient.PurchaseQuantity,
        PurchasePrice = ingredient.PurchasePrice,
        PurchaseUnit = ingredient.PurchaseUnit,
        Link = ingredient.RecipeId is null ? "" : $"{ingredient.RecipeId}/{ingredient.VersionId}",
        LinkedContent = ingredient.LinkedContent,
    };
}
