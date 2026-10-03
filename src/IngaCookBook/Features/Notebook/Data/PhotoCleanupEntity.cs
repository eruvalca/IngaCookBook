namespace IngaCookBook.Features.Notebook.Data;

// Deliberately has no workspace FK: cleanup must survive deletion of its owner.
internal sealed class PhotoCleanupEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Prefix { get; set; } = "";
    public DateTimeOffset QueuedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset NextAttemptAt { get; set; } = DateTimeOffset.UtcNow;
}
