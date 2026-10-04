namespace IngaCookBook.Features.Notebook.Data;

internal sealed class RecipeEntity
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid Revision { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public Guid? CoverPhotoId { get; set; }
    public Guid? StandardVersionId { get; set; }
    public Guid? OriginRecipeId { get; set; }
    public Guid? OriginVersionId { get; set; }
    public List<MetricEntity> Metrics { get; set; } = [];
    public List<VersionEntity> Versions { get; set; } = [];
    public List<StandardEntity> Standards { get; set; } = [];
    public DateTimeOffset UpdatedAt { get; set; }
}
