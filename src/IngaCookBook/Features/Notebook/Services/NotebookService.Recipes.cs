using IngaCookBook.Features.Notebook.Data;
using IngaCookBook.SharedKernel.Notebook;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace IngaCookBook.Features.Notebook.Services;

internal sealed partial class NotebookService
{
    public async Task<NotebookChange> CreateWorkspaceAsync(WorkspaceRequest request, CancellationToken cancellationToken = default)
    {
        var userId = await UserIdAsync();
        if (userId is null)
        {
            return new ChangeRejected("Sign in to create a workspace.", 401);
        }
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 120 ||
            request.Currency is not ("USD" or "CAD" or "GBP" or "EUR" or "AUD"))
        {
            return new ChangeRejected("Enter a workspace name (up to 120 characters) and select a currency.");
        }
        if (await GetWorkspaceAsync(cancellationToken) is not null)
        {
            return new ChangeRejected("You already have a workspace.", 409);
        }
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var workspace = new WorkspaceEntity { Id = Guid.NewGuid(), OwnerId = userId, Name = request.Name.Trim(), Currency = request.Currency };
        db.Add(workspace);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return new ChangeSaved(workspace.Id);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return new ChangeRejected("You already have a workspace.", 409);
        }
    }

    public Task<NotebookChange> CreateRecipeAsync(NewRecipeRequest request, CancellationToken cancellationToken = default)
    {
        var metrics = request.Metrics?.Select(name => new EvaluationMetric(Guid.NewGuid(), name)).ToArray();
        var error = NotebookValidation.Settings(request.Name, request.Description, metrics);
        if (error is not null)
        {
            return Task.FromResult<NotebookChange>(new ChangeRejected(error));
        }
        var recipe = new RecipeDocument
        {
            Name = request.Name.Trim(),
            Description = request.Description.Trim(),
            Metrics = metrics!,
            Versions = [new RecipeVersion { Number = 1 }],
        };
        return InsertAsync(recipe, cancellationToken);
    }

    public async Task<NotebookChange> VaryAsync(Guid recipeId, Guid versionId, RevisionRequest request, CancellationToken cancellationToken = default)
    {
        var newId = Guid.NewGuid();
        return await UpdateAsync(recipeId, request.Revision, recipe =>
        {
            var parent = recipe.Versions.FirstOrDefault(v => v.Id == versionId);
            if (parent is null)
            {
                return null;
            }
            var version = new RecipeVersion
            {
                Id = newId,
                Number = recipe.Versions.Max(v => v.Number) + 1,
                ParentId = versionId,
                Content = parent.Content with { Label = "New experiment", Hypothesis = "", RelatedChanges = "" },
            };
            // Preserve the baseline as soon as a variation depends on it.
            return recipe with
            {
                Versions = [.. recipe.Versions.Select(v => v.Id == versionId ? v with { IsLocked = true } : v), version],
            };
        }, newId, cancellationToken);
    }

    public async Task<NotebookChange> SetStandardAsync(Guid recipeId, Guid versionId, RevisionRequest request, CancellationToken cancellationToken = default)
    {
        var recipe = await GetRecipeAsync(recipeId, cancellationToken);
        var version = recipe?.Versions.FirstOrDefault(v => v.Id == versionId);
        if (version is null)
        {
            return new ChangeRejected("This version could not be found.", 404);
        }
        var error = NotebookValidation.Content(version.Content, complete: true);
        if (error is not null)
        {
            return new ChangeRejected(error);
        }
        return await UpdateAsync(recipeId, request.Revision, current =>
        {
            var updated = Replace(current, versionId, v => v with { IsLocked = true });
            return updated is null ? null : updated with
            {
                StandardVersionId = versionId,
                Standards = [.. current.Standards, new StandardSelection(versionId, DateTimeOffset.UtcNow)],
            };
        }, versionId, cancellationToken);
    }

    public async Task<NotebookChange> SaveSettingsAsync(Guid recipeId, RecipeSettingsRequest request, CancellationToken cancellationToken = default)
    {
        var error = NotebookValidation.Settings(request.Name, request.Description, request.Metrics);
        if (error is not null)
        {
            return new ChangeRejected(error);
        }
        var current = await GetRecipeAsync(recipeId, cancellationToken);
        if (current is null) { return new ChangeRejected("This recipe could not be found.", 404); }
        var photoIds = current.Versions.SelectMany(v => v.Photos).Select(p => p.Id).ToHashSet();
        if (request.CoverPhotoId is { } cover && !photoIds.Contains(cover) ||
            request.PhotoCaptions?.Any(p => !photoIds.Contains(p.Key) || string.IsNullOrWhiteSpace(p.Value) || p.Value.Length > 200) == true)
        {
            return new ChangeRejected("Choose photos from this recipe and enter captions of 1–200 characters.");
        }
        var ids = request.Metrics.Select(m => m.Id).ToHashSet();
        return await UpdateAsync(recipeId, request.Revision, recipe => recipe with
        {
            CoverPhotoId = request.CoverPhotoId,
            Name = request.Name.Trim(),
            Description = request.Description.Trim(),
            // Keep established identities; the server assigns identities to new criteria.
            Metrics = request.Metrics.Select(m => recipe.Metrics.Any(existing => existing.Id == m.Id)
                ? m : m with { Id = Guid.NewGuid() }).ToArray(),
            Versions = recipe.Versions.Select(v => v with
            {
                Photos = v.Photos.Select(p => request.PhotoCaptions is not null && request.PhotoCaptions.TryGetValue(p.Id, out var caption) ? p with { Caption = caption.Trim() } : p).ToArray(),
                Content = v.Content with { TargetMetricId = ids.Contains(v.Content.TargetMetricId ?? Guid.Empty) ? v.Content.TargetMetricId : null },
                Batches = v.Batches.Select(b => b with
                {
                    Evaluations = b.Evaluations.Select(e => e with
                    {
                        Scores = e.Scores.Where(s => ids.Contains(s.MetricId)).ToArray(),
                        Corrections = e.Corrections.Select(c => c with
                        {
                            PreviousContent = c.PreviousContent with
                            {
                                Scores = c.PreviousContent.Scores.Where(s => ids.Contains(s.MetricId)).ToArray(),
                            },
                        }).ToArray(),
                    }).ToArray(),
                }).ToArray(),
            }).ToArray(),
        }, recipeId, cancellationToken);
    }

    public async Task<NotebookChange> PromoteAsync(Guid recipeId, Guid versionId, PromotionRequest request, CancellationToken cancellationToken = default)
    {
        var original = await GetRecipeAsync(recipeId, cancellationToken);
        var source = original?.Versions.FirstOrDefault(v => v.Id == versionId);
        if (source is null || original is null)
        {
            return new ChangeRejected("This version could not be found.", 404);
        }
        if (original.Revision != request.Revision)
        {
            return Conflict();
        }
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 200)
        {
            return new ChangeRejected("Enter a recipe name of up to 200 characters.");
        }
        var metrics = original.Metrics.ToDictionary(m => m.Id, m => new EvaluationMetric(Guid.NewGuid(), m.Name));
        var target = source.Content.TargetMetricId is { } targetId && metrics.TryGetValue(targetId, out var mapped) ? mapped.Id : (Guid?)null;
        return await InsertPromotionAsync(new RecipeDocument
        {
            Name = request.Name.Trim(),
            Description = original.Description,
            OriginRecipeId = original.Id,
            OriginVersionId = source.Id,
            Metrics = metrics.Values.ToArray(),
            Versions = [new RecipeVersion { Number = 1, Content = source.Content with { Label = "Starting recipe", TargetMetricId = target } }],
        }, request.Revision, cancellationToken);
    }
}
