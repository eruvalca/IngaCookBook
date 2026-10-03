using IngaCookBook.SharedKernel.Notebook;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Shouldly;
using Xunit;

namespace IngaCookBook.IntegrationTests;

public sealed partial class NotebookPersistenceTests
{
    [Fact]
    public async Task BatchCorrectionsRetainIdentityOrderTastingsAndSuccessiveHistory()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var store = await NotebookTestStore.CreateAsync(ct);
        var service = await store.OwnerAsync("owner", ct);
        var recipe = await CreateAsync(service, ct);
        var versionId = recipe.Versions[0].Id;
        var day = new DateTimeOffset(2026, 10, 3, 0, 0, 0, TimeSpan.Zero);
        var first = (await service.MakeBatchAsync(recipe.Id, versionId, new(recipe.Revision, day, "Original process"), ct)).ShouldBeOfType<ChangeSaved>().Id;
        recipe = (await service.GetRecipeAsync(recipe.Id, ct))!;
        var second = (await service.MakeBatchAsync(recipe.Id, versionId, new(recipe.Revision, day.AddDays(2), "Repeat"), ct)).ShouldBeOfType<ChangeSaved>().Id;
        recipe = (await service.GetRecipeAsync(recipe.Id, ct))!;
        var tasting = (await service.EvaluateAsync(recipe.Id, versionId, first,
            new(recipe.Revision, day.AddDays(3), "Original tasting", "", [new(recipe.Metrics[0].Id, 7, "Smooth")]), ct)).ShouldBeOfType<ChangeSaved>().Id;
        recipe = (await service.GetRecipeAsync(recipe.Id, ct))!;
        var request = new BatchCorrectionRequest(new(recipe.Revision, day.AddDays(3), "Actually aged 12 hours"), " Fix date and timing ");
        (await service.CorrectBatchAsync(recipe.Id, versionId, first, request, ct)).ShouldBeOfType<ChangeSaved>().Id.ShouldBe(first);
        (await service.CorrectBatchAsync(recipe.Id, versionId, first, request, ct)).ShouldBeOfType<ChangeRejected>().Status.ShouldBe(409);
        recipe = (await store.Service("owner").GetRecipeAsync(recipe.Id, ct))!;
        var batches = recipe.Versions[0].Batches;
        batches.Select(b => b.Id).ShouldBe([first, second]);
        batches[0].MadeAt.ShouldBe(day.AddDays(3));
        batches[0].Notes.ShouldBe("Actually aged 12 hours");
        batches[0].Evaluations.Single().Id.ShouldBe(tasting);
        batches[0].Evaluations[0].Notes.ShouldBe("Original tasting");
        batches[0].Evaluations[0].Scores.Single().Score.ShouldBe(7);
        var audit = batches[0].Corrections.Single();
        audit.Id.ShouldNotBe(Guid.Empty);
        audit.Reason.ShouldBe("Fix date and timing");
        audit.PreviousMadeAt.ShouldBe(day);
        audit.PreviousNotes.ShouldBe("Original process");
        batches[1].Notes.ShouldBe("Repeat");
        (await service.CorrectBatchAsync(recipe.Id, versionId, first,
            new(new(recipe.Revision, day.AddDays(-1), "Second correction"), "Earlier date"), ct)).ShouldBeOfType<ChangeSaved>();
        recipe = (await service.GetRecipeAsync(recipe.Id, ct))!;
        batches = recipe.Versions[0].Batches;
        batches.Select(b => b.Id).ShouldBe([first, second]);
        batches[0].Corrections.Select(c => c.PreviousNotes).ShouldBe(["Original process", "Actually aged 12 hours"]);
        batches[0].Corrections[1].PreviousMadeAt.ShouldBe(day.AddDays(3));
        batches[0].Evaluations.Single().Id.ShouldBe(tasting);
        recipe.Versions[0].IsLocked.ShouldBeTrue();
    }

    [Theory]
    [InlineData("reason", 400)]
    [InlineData("input", 400)]
    [InlineData("notes", 400)]
    [InlineData("default-date", 400)]
    [InlineData("after-tasting", 400)]
    [InlineData("missing-batch", 404)]
    [InlineData("missing-version", 404)]
    [InlineData("owner", 404)]
    public async Task InvalidBatchCorrectionsLeaveDateNotesTastingsAndHistoryUntouched(string invalid, int status)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var store = await NotebookTestStore.CreateAsync(ct);
        var service = await store.OwnerAsync("owner", ct);
        var recipe = await CreateAsync(service, ct);
        var versionId = recipe.Versions[0].Id;
        var day = new DateTimeOffset(2026, 10, 3, 0, 0, 0, TimeSpan.Zero);
        var batch = (await service.MakeBatchAsync(recipe.Id, versionId, new(recipe.Revision, day, "Original"), ct)).ShouldBeOfType<ChangeSaved>().Id;
        recipe = (await service.GetRecipeAsync(recipe.Id, ct))!;
        (await service.EvaluateAsync(recipe.Id, versionId, batch, new(recipe.Revision, day, "Tasted", "", []), ct)).ShouldBeOfType<ChangeSaved>();
        recipe = (await service.GetRecipeAsync(recipe.Id, ct))!;
        var request = new BatchCorrectionRequest(new(recipe.Revision, day, "Changed"), "Fix entry");
        request = invalid switch
        {
            "reason" => request with { Reason = " " },
            "input" => request with { Batch = null! },
            "notes" => request with { Batch = request.Batch with { Notes = null! } },
            "default-date" => request with { Batch = request.Batch with { MadeAt = default } },
            "after-tasting" => request with { Batch = request.Batch with { MadeAt = day.AddTicks(1) } },
            _ => request,
        };
        var caller = service;
        if (string.Equals(invalid, "owner", StringComparison.Ordinal))
        {
            caller = await store.OwnerAsync("other", ct);
            (await caller.CreateWorkspaceAsync(new("Other", "USD"), ct)).ShouldBeOfType<ChangeSaved>();
        }
        (await caller.CorrectBatchAsync(recipe.Id,
            string.Equals(invalid, "missing-version", StringComparison.Ordinal) ? Guid.NewGuid() : versionId,
            string.Equals(invalid, "missing-batch", StringComparison.Ordinal) ? Guid.NewGuid() : batch, request, ct))
            .ShouldBeOfType<ChangeRejected>().Status.ShouldBe(status);
        var saved = (await service.GetRecipeAsync(recipe.Id, ct))!;
        saved.Revision.ShouldBe(recipe.Revision);
        var unchanged = saved.Versions[0].Batches.Single();
        unchanged.MadeAt.ShouldBe(day);
        unchanged.Notes.ShouldBe("Original");
        unchanged.Evaluations.Single().Notes.ShouldBe("Tasted");
        unchanged.Corrections.ShouldBeEmpty();
    }

    [Fact]
    public async Task BatchPositionMigrationPreservesExistingChronology()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var store = await NotebookTestStore.CreateAsync(ct);
        var service = await store.OwnerAsync("owner", ct);
        var recipe = await CreateAsync(service, ct);
        var version = recipe.Versions[0].Id;
        var later = Guid.NewGuid();
        var earlier = Guid.NewGuid();
        var day = new DateTimeOffset(2026, 10, 3, 0, 0, 0, TimeSpan.Zero);
        await using var db = await store.Factory.CreateDbContextAsync(ct);
        // Only this test's disposable database is moved to the previous schema.
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261003045706_AddEvaluationCorrections", ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"RecipeBatches\" (\"Id\", \"VersionId\", \"MadeAt\", \"Notes\") VALUES ({later}, {version}, {day}, {"Later"}), ({earlier}, {version}, {day.AddDays(-1)}, {"Earlier"})", ct);
        await migrator.MigrateAsync(cancellationToken: ct);
        var saved = (await service.GetRecipeAsync(recipe.Id, ct))!;
        saved.Versions[0].Batches.Select(b => b.Id).ShouldBe([earlier, later]);
        (await service.CorrectBatchAsync(recipe.Id, version, earlier,
            new(new(saved.Revision, day.AddDays(2), "Later after correction"), "Fix entry"), ct)).ShouldBeOfType<ChangeSaved>();
        saved = (await service.GetRecipeAsync(recipe.Id, ct))!;
        saved.Versions[0].Batches.Select(b => b.Id).ShouldBe([earlier, later]);
    }
}
