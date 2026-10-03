using IngaCookBook.Features.Notebook.Data;
using IngaCookBook.SharedKernel.Notebook;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace IngaCookBook.IntegrationTests;

public sealed partial class NotebookPersistenceTests
{
    [Fact]
    public async Task TastingCorrectionsPreserveHistoryAndOrderAndRemoveDeletedCriteriaFromAudit()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var store = await NotebookTestStore.CreateAsync(ct);
        var service = await store.OwnerAsync("owner", ct);
        var recipe = await CreateAsync(service, ct);
        var versionId = recipe.Versions[0].Id;
        var day = new DateTimeOffset(2026, 10, 2, 0, 0, 0, TimeSpan.Zero);
        var batchId = (await service.MakeBatchAsync(recipe.Id, versionId, new(recipe.Revision, day, "Batch"), ct)).ShouldBeOfType<ChangeSaved>().Id;
        recipe = (await service.GetRecipeAsync(recipe.Id, ct))!;
        var metricId = recipe.Metrics[0].Id;
        var evaluationId = (await service.EvaluateAsync(recipe.Id, versionId, batchId,
            new(recipe.Revision, day, "Original", "Less sugar", [new(metricId, 10, "Accidental ten")]), ct)).ShouldBeOfType<ChangeSaved>().Id;
        recipe = (await service.GetRecipeAsync(recipe.Id, ct))!;
        var recordedAt = recipe.Versions[0].Batches[0].Evaluations[0].RecordedAt;
        var laterId = (await service.EvaluateAsync(recipe.Id, versionId, batchId,
            new(recipe.Revision, day.AddDays(1), "Later tasting", "", [new(metricId, 8, "Smooth")]), ct)).ShouldBeOfType<ChangeSaved>().Id;
        recipe = (await service.GetRecipeAsync(recipe.Id, ct))!;
        var request = new EvaluationCorrectionRequest(new(recipe.Revision, day.AddDays(2), "Corrected", "More aging",
            [new(metricId, 7, "Seven intended")]), " Typing mistake ");
        (await service.CorrectEvaluationAsync(recipe.Id, versionId, batchId, evaluationId, request, ct)).ShouldBeOfType<ChangeSaved>().Id.ShouldBe(evaluationId);
        (await service.CorrectEvaluationAsync(recipe.Id, versionId, batchId, evaluationId, request, ct)).ShouldBeOfType<ChangeRejected>().Status.ShouldBe(409);
        recipe = (await store.Service("owner").GetRecipeAsync(recipe.Id, ct))!;
        var evaluations = recipe.Versions[0].Batches[0].Evaluations;
        evaluations.Select(e => e.Id).ShouldBe([evaluationId, laterId]);
        var corrected = evaluations[0];
        corrected.RecordedAt.ShouldBe(recordedAt);
        corrected.TastedAt.ShouldBe(day.AddDays(2));
        corrected.Notes.ShouldBe("Corrected");
        corrected.NextIdea.ShouldBe("More aging");
        corrected.Scores.Single().ShouldBe(new MetricScore(metricId, 7, "Seven intended"));
        var audit = corrected.Corrections.Single();
        audit.Reason.ShouldBe("Typing mistake");
        audit.PreviousContent.TastedAt.ShouldBe(day);
        audit.PreviousContent.Notes.ShouldBe("Original");
        audit.PreviousContent.NextIdea.ShouldBe("Less sugar");
        audit.PreviousContent.Scores.Single().ShouldBe(new MetricScore(metricId, 10, "Accidental ten"));
        evaluations[1].Notes.ShouldBe("Later tasting");
        (await service.CorrectEvaluationAsync(recipe.Id, versionId, batchId, evaluationId,
            new(request.Evaluation with { Revision = recipe.Revision, Notes = "Second correction" }, "Fix wording"), ct)).ShouldBeOfType<ChangeSaved>();
        recipe = (await service.GetRecipeAsync(recipe.Id, ct))!;
        recipe.Versions[0].Batches[0].Evaluations[0].Corrections.Select(c => c.PreviousContent.Notes).ShouldBe(["Original", "Corrected"]);
        (await service.SaveSettingsAsync(recipe.Id, new(recipe.Revision, recipe.Name, recipe.Description, []), ct)).ShouldBeOfType<ChangeSaved>();
        recipe = (await service.GetRecipeAsync(recipe.Id, ct))!;
        corrected = recipe.Versions[0].Batches[0].Evaluations[0];
        corrected.Scores.ShouldBeEmpty();
        corrected.Corrections.ShouldAllBe(c => c.PreviousContent.Scores.Count == 0);
        corrected.Corrections[0].PreviousContent.Notes.ShouldBe("Original");
        await using var db = await store.Factory.CreateDbContextAsync(ct);
        (await db.Set<EvaluationCorrectionEntity>().ToListAsync(ct)).ShouldAllBe(c => !c.PreviousContent.Contains("Accidental ten", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("reason", 400)]
    [InlineData("input", 400)]
    [InlineData("score", 400)]
    [InlineData("date", 400)]
    [InlineData("missing", 404)]
    [InlineData("owner", 404)]
    public async Task InvalidTastingCorrectionsLeaveOriginalAndAuditUntouched(string invalid, int status)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var store = await NotebookTestStore.CreateAsync(ct);
        var service = await store.OwnerAsync("owner", ct);
        var recipe = await CreateAsync(service, ct);
        var versionId = recipe.Versions[0].Id;
        var day = DateTimeOffset.UtcNow;
        var batchId = (await service.MakeBatchAsync(recipe.Id, versionId, new(recipe.Revision, day, ""), ct)).ShouldBeOfType<ChangeSaved>().Id;
        recipe = (await service.GetRecipeAsync(recipe.Id, ct))!;
        var id = (await service.EvaluateAsync(recipe.Id, versionId, batchId,
            new(recipe.Revision, day, "Original", "", [new(recipe.Metrics[0].Id, 6, "")]), ct)).ShouldBeOfType<ChangeSaved>().Id;
        recipe = (await service.GetRecipeAsync(recipe.Id, ct))!;
        var caller = service;
        if (string.Equals(invalid, "owner", StringComparison.Ordinal))
        {
            caller = await store.OwnerAsync("other", ct);
            (await caller.CreateWorkspaceAsync(new("Other", "USD"), ct)).ShouldBeOfType<ChangeSaved>();
        }
        var request = new EvaluationCorrectionRequest(new(recipe.Revision,
            string.Equals(invalid, "date", StringComparison.Ordinal) ? day.AddDays(-1) : day, "Changed", "",
            [new(recipe.Metrics[0].Id, string.Equals(invalid, "score", StringComparison.Ordinal) ? 11 : 8, "")]),
            string.Equals(invalid, "reason", StringComparison.Ordinal) ? " " : "Fix typo");
        if (string.Equals(invalid, "input", StringComparison.Ordinal))
        {
            request = request with { Evaluation = null! };
        }
        (await caller.CorrectEvaluationAsync(recipe.Id, versionId, batchId,
            string.Equals(invalid, "missing", StringComparison.Ordinal) ? Guid.NewGuid() : id, request, ct)).ShouldBeOfType<ChangeRejected>().Status.ShouldBe(status);
        var saved = (await service.GetRecipeAsync(recipe.Id, ct))!;
        saved.Revision.ShouldBe(recipe.Revision);
        saved.Versions[0].Batches[0].Evaluations.Single().Notes.ShouldBe("Original");
        saved.Versions[0].Batches[0].Evaluations[0].Scores.Single().Score.ShouldBe(6);
        saved.Versions[0].Batches[0].Evaluations[0].Corrections.ShouldBeEmpty();
    }

    [Fact]
    public async Task PreservedVariationCorrectionNeedsOnlyItsCorrectionReason()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var store = await NotebookTestStore.CreateAsync(ct);
        var service = await store.OwnerAsync("owner", ct);
        var recipe = await CreateAsync(service, ct);
        var id = (await service.VaryAsync(recipe.Id, recipe.Versions[0].Id, new(recipe.Revision), ct)).ShouldBeOfType<ChangeSaved>().Id;
        recipe = (await service.GetRecipeAsync(recipe.Id, ct))!;
        var content = recipe.Versions[1].Content with { Ingredients = [recipe.Versions[1].Content.Ingredients[0] with { Quantity = 120 }] };
        (await service.SaveVersionAsync(recipe.Id, id, new(recipe.Revision, content, null), ct)).ShouldBeOfType<ChangeSaved>();
        recipe = (await service.GetRecipeAsync(recipe.Id, ct))!;
        (await service.SetStandardAsync(recipe.Id, id, new(recipe.Revision), ct)).ShouldBeOfType<ChangeSaved>();
        recipe = (await service.GetRecipeAsync(recipe.Id, ct))!;
        (await service.SaveVersionAsync(recipe.Id, id, new(recipe.Revision, content with { Yield = 120 }, "Correct yield"), ct)).ShouldBeOfType<ChangeSaved>();
        recipe = (await service.GetRecipeAsync(recipe.Id, ct))!;
        recipe.Versions[1].Content.Yield.ShouldBe(120m);
        recipe.Versions[1].Content.RelatedChanges.ShouldBeEmpty();
        recipe.Versions[1].Corrections.Single().PreviousContent.Yield.ShouldBeNull();
        recipe.Versions[1].Corrections[0].Reason.ShouldBe("Correct yield");
    }
}
