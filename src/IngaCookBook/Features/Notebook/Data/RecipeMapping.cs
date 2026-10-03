using System.Text.Json;
using IngaCookBook.SharedKernel.Notebook;
using Microsoft.EntityFrameworkCore;

namespace IngaCookBook.Features.Notebook.Data;

internal static class RecipeMapping
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    internal static IQueryable<RecipeEntity> Complete(IQueryable<RecipeEntity> query) => query.AsSplitQuery()
        .Include(r => r.Metrics).Include(r => r.Standards)
        .Include(r => r.Versions).ThenInclude(v => v.Ingredients)
        .Include(r => r.Versions).ThenInclude(v => v.Steps)
        .Include(r => r.Versions).ThenInclude(v => v.Photos)
        .Include(r => r.Versions).ThenInclude(v => v.Corrections)
        .Include(r => r.Versions).ThenInclude(v => v.Batches).ThenInclude(b => b.Evaluations).ThenInclude(e => e.Scores);

    internal static RecipeDocument Read(RecipeEntity entity) => new()
    {
        Id = entity.Id,
        Revision = entity.Revision,
        Name = entity.Name,
        Description = entity.Description,
        StandardVersionId = entity.StandardVersionId,
        OriginRecipeId = entity.OriginRecipeId,
        OriginVersionId = entity.OriginVersionId,
        UpdatedAt = entity.UpdatedAt,
        Metrics = entity.Metrics.OrderBy(m => m.Position).Select(m => new EvaluationMetric(m.Id, m.Name)).ToArray(),
        Standards = entity.Standards.OrderBy(s => s.SelectedAt).Select(s => new StandardSelection(s.VersionId, s.SelectedAt)).ToArray(),
        Versions = entity.Versions.OrderBy(v => v.Number).Select(ReadVersion).ToArray(),
    };

    private static RecipeVersion ReadVersion(VersionEntity entity) => new()
    {
        Id = entity.Id,
        Number = entity.Number,
        ParentId = entity.ParentId,
        CreatedAt = entity.CreatedAt,
        IsLocked = entity.IsLocked,
        Content = new RecipeContent
        {
            Label = entity.Label,
            Notes = entity.Notes,
            TargetMetricId = entity.TargetMetricId,
            Hypothesis = entity.Hypothesis,
            RelatedChanges = entity.RelatedChanges,
            Yield = entity.Yield,
            YieldUnit = entity.YieldUnit,
            Ingredients = entity.Ingredients.OrderBy(i => i.Position).Select(ReadIngredient).ToArray(),
            Steps = entity.Steps.OrderBy(s => s.Position).Select(s => new PreparationStep(s.RowId, s.Instruction, s.Notes)).ToArray(),
        },
        Batches = entity.Batches.OrderBy(b => b.MadeAt).Select(b => new RecipeBatch(b.Id, b.MadeAt, b.Notes,
            b.Evaluations.OrderBy(e => e.RecordedAt).Select(e => new BatchEvaluation(e.Id, e.TastedAt, e.Notes, e.NextIdea,
                e.Scores.Select(s => new MetricScore(s.MetricId, s.Score, s.Notes)).ToArray())
            { RecordedAt = e.RecordedAt }).ToArray())).ToArray(),
        Photos = entity.Photos.OrderBy(p => p.CreatedAt).Select(p => new RecipePhoto(p.Id, p.Caption, p.ContentType, p.CreatedAt)).ToArray(),
        Corrections = entity.Corrections.OrderBy(c => c.CorrectedAt).Select(c => new VersionCorrection(c.CorrectedAt, c.Reason, Deserialize(c.PreviousContent)!)).ToArray(),
    };

    private static Ingredient ReadIngredient(IngredientEntity entity) => new()
    {
        Id = entity.RowId,
        Name = entity.Name,
        Quantity = entity.Quantity,
        Unit = entity.Unit,
        PurchaseQuantity = entity.PurchaseQuantity,
        PurchaseUnit = entity.PurchaseUnit,
        PurchasePrice = entity.PurchasePrice,
        RecipeId = entity.LinkedRecipeId,
        VersionId = entity.LinkedVersionId,
        LinkedContent = Deserialize(entity.LinkedSnapshot),
    };

    internal static void Apply(RecipeEntity entity, RecipeDocument recipe)
    {
        entity.Id = recipe.Id;
        entity.Revision = recipe.Revision;
        entity.Name = recipe.Name;
        entity.Description = recipe.Description;
        entity.StandardVersionId = recipe.StandardVersionId;
        entity.OriginRecipeId = recipe.OriginRecipeId;
        entity.OriginVersionId = recipe.OriginVersionId;
        entity.UpdatedAt = recipe.UpdatedAt;
        Sync(entity.Metrics, recipe.Metrics, e => e.Id, m => m.Id, () => new(), (e, m, i) => { e.Id = m.Id; e.Name = m.Name; e.Position = i; });
        Sync(entity.Versions, recipe.Versions, e => e.Id, v => v.Id, () => new(), (e, v, _) => ApplyVersion(e, v));
        foreach (var selection in recipe.Standards.Where(s => !entity.Standards.Any(e => e.SelectedAt == s.SelectedAt && e.VersionId == s.VersionId)))
        {
            entity.Standards.Add(new() { Id = Guid.NewGuid(), VersionId = selection.VersionId, SelectedAt = selection.SelectedAt });
        }
    }

    private static void ApplyVersion(VersionEntity entity, RecipeVersion version)
    {
        entity.Id = version.Id;
        entity.Number = version.Number;
        entity.ParentId = version.ParentId;
        entity.CreatedAt = version.CreatedAt;
        entity.IsLocked = version.IsLocked;
        entity.Label = version.Content.Label;
        entity.Notes = version.Content.Notes;
        entity.TargetMetricId = version.Content.TargetMetricId;
        entity.Hypothesis = version.Content.Hypothesis;
        entity.RelatedChanges = version.Content.RelatedChanges;
        entity.Yield = version.Content.Yield;
        entity.YieldUnit = version.Content.YieldUnit;
        Sync(entity.Ingredients, version.Content.Ingredients, e => e.RowId, i => i.Id,
            () => new() { Id = Guid.NewGuid() }, ApplyIngredient);
        Sync(entity.Steps, version.Content.Steps, e => e.RowId, s => s.Id, () => new() { Id = Guid.NewGuid() },
            (e, s, i) => { e.RowId = s.Id; e.Position = i; e.Instruction = s.Instruction; e.Notes = s.Notes; });
        Sync(entity.Batches, version.Batches, e => e.Id, b => b.Id, () => new(), (e, b, _) => ApplyBatch(e, b));
        Sync(entity.Photos, version.Photos, e => e.Id, p => p.Id, () => new(),
            (e, p, _) => { e.Id = p.Id; e.Caption = p.Caption; e.ContentType = p.ContentType; e.CreatedAt = p.CreatedAt; });
        foreach (var correction in version.Corrections.Where(c => !entity.Corrections.Any(e => e.CorrectedAt == c.CorrectedAt)))
        {
            entity.Corrections.Add(new()
            {
                Id = Guid.NewGuid(),
                CorrectedAt = correction.CorrectedAt,
                Reason = correction.Reason,
                PreviousContent = JsonSerializer.Serialize(correction.PreviousContent, _json),
            });
        }
    }

    private static void ApplyIngredient(IngredientEntity entity, Ingredient ingredient, int index)
    {
        entity.RowId = ingredient.Id;
        entity.Position = index;
        entity.Name = ingredient.Name;
        entity.Quantity = ingredient.Quantity;
        entity.Unit = ingredient.Unit;
        entity.PurchaseQuantity = ingredient.PurchaseQuantity;
        entity.PurchaseUnit = ingredient.PurchaseUnit;
        entity.PurchasePrice = ingredient.PurchasePrice;
        entity.LinkedRecipeId = ingredient.RecipeId;
        entity.LinkedVersionId = ingredient.VersionId;
        entity.LinkedSnapshot = ingredient.LinkedContent is null ? null : JsonSerializer.Serialize(ingredient.LinkedContent, _json);
    }

    private static void ApplyBatch(BatchEntity entity, RecipeBatch batch)
    {
        entity.Id = batch.Id;
        entity.MadeAt = batch.MadeAt;
        entity.Notes = batch.Notes;
        Sync(entity.Evaluations, batch.Evaluations, e => e.Id, e => e.Id, () => new(), (e, source, _) =>
        {
            e.Id = source.Id;
            e.TastedAt = source.TastedAt;
            e.RecordedAt = source.RecordedAt;
            e.Notes = source.Notes;
            e.NextIdea = source.NextIdea;
            Sync(e.Scores, source.Scores, s => s.MetricId, s => s.MetricId, () => new() { Id = Guid.NewGuid() },
                (s, score, _) => { s.MetricId = score.MetricId; s.Score = score.Score; s.Notes = score.Notes; });
        });
    }

    private static void Sync<TEntity, TSource>(List<TEntity> entities, IReadOnlyList<TSource> sources,
        Func<TEntity, Guid> entityKey, Func<TSource, Guid> sourceKey, Func<TEntity> create, Action<TEntity, TSource, int> apply)
    {
        var keys = sources.Select(sourceKey).ToHashSet();
        entities.RemoveAll(e => !keys.Contains(entityKey(e)));
        for (var i = 0; i < sources.Count; i++)
        {
            var source = sources[i];
            var entity = entities.FirstOrDefault(e => entityKey(e) == sourceKey(source));
            if (entity is null)
            {
                entity = create();
                entities.Add(entity);
            }
            apply(entity, source, i);
        }
    }

    private static RecipeContent? Deserialize(string? json) =>
        json is null ? null : JsonSerializer.Deserialize<RecipeContent>(json, _json);
}
