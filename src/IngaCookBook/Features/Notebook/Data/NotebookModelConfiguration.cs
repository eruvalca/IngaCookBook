using IngaCookBook.Data;
using Microsoft.EntityFrameworkCore;

namespace IngaCookBook.Features.Notebook.Data;

internal static class NotebookModelConfiguration
{
    internal static void Configure(ModelBuilder builder)
    {
        builder.Entity<PhotoCleanupEntity>(entity =>
        {
            entity.ToTable("PhotoCleanupJobs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Prefix).HasMaxLength(200);
            entity.HasIndex(e => e.NextAttemptAt);
        });
        builder.Entity<WorkspaceEntity>(entity =>
        {
            entity.ToTable("Workspaces");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Name).HasMaxLength(120);
            entity.Property(e => e.Currency).HasMaxLength(3);
            entity.HasIndex(e => e.OwnerId).IsUnique();
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(e => e.OwnerId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<RecipeEntity>(entity =>
        {
            entity.ToTable("Recipes");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(4000);
            entity.Property(e => e.Revision).IsConcurrencyToken();
            entity.HasIndex(e => new { e.WorkspaceId, e.UpdatedAt });
            entity.HasOne<WorkspaceEntity>().WithMany().HasForeignKey(e => e.WorkspaceId);
            entity.HasMany(e => e.Metrics).WithOne().HasForeignKey(e => e.RecipeId);
            entity.HasMany(e => e.Versions).WithOne().HasForeignKey(e => e.RecipeId);
            entity.HasMany(e => e.Standards).WithOne().HasForeignKey(e => e.RecipeId);
        });
        builder.Entity<MetricEntity>(entity =>
        {
            entity.ToTable("EvaluationMetrics");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Name).HasMaxLength(100);
        });
        builder.Entity<VersionEntity>(entity =>
        {
            entity.ToTable("RecipeVersions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.HasIndex(e => new { e.RecipeId, e.Number }).IsUnique();
            entity.Property(e => e.Label).HasMaxLength(200);
            entity.HasMany(e => e.Ingredients).WithOne().HasForeignKey(e => e.VersionId);
            entity.HasMany(e => e.Steps).WithOne().HasForeignKey(e => e.VersionId);
            entity.HasMany(e => e.Batches).WithOne().HasForeignKey(e => e.VersionId);
            entity.HasMany(e => e.Photos).WithOne().HasForeignKey(e => e.VersionId);
            entity.HasMany(e => e.Corrections).WithOne().HasForeignKey(e => e.VersionId);
            entity.HasOne<VersionEntity>().WithMany().HasForeignKey(e => e.ParentId).OnDelete(DeleteBehavior.Restrict);
        });
        ConfigureDetails(builder);
    }

    private static void ConfigureDetails(ModelBuilder builder)
    {
        builder.Entity<IngredientEntity>(entity =>
        {
            entity.ToTable("VersionIngredients", table => table.HasCheckConstraint("CK_Ingredient_Amounts",
                "\"Quantity\" > 0 AND \"PurchaseQuantity\" > 0 AND \"PurchasePrice\" >= 0"));
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.HasIndex(e => new { e.VersionId, e.RowId }).IsUnique();
            entity.Property(e => e.Name).HasMaxLength(300);
            entity.Property(e => e.LinkedSnapshot).HasColumnType("jsonb");
        });
        builder.Entity<StepEntity>(entity =>
        {
            entity.ToTable("PreparationSteps");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.HasIndex(e => new { e.VersionId, e.RowId }).IsUnique();
        });
        builder.Entity<BatchEntity>(entity =>
        {
            entity.ToTable("RecipeBatches");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.HasMany(e => e.Evaluations).WithOne().HasForeignKey(e => e.BatchId);
            entity.HasMany(e => e.Corrections).WithOne().HasForeignKey(e => e.BatchId);
        });
        builder.Entity<BatchCorrectionEntity>(entity =>
        {
            entity.ToTable("BatchCorrections");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();
        });
        builder.Entity<EvaluationEntity>(entity =>
        {
            entity.ToTable("BatchEvaluations");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.HasMany(e => e.Scores).WithOne().HasForeignKey(e => e.EvaluationId);
            entity.HasMany(e => e.Corrections).WithOne().HasForeignKey(e => e.EvaluationId);
        });
        builder.Entity<EvaluationCorrectionEntity>(entity =>
        {
            entity.ToTable("EvaluationCorrections");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.PreviousContent).HasColumnType("jsonb");
        });
        builder.Entity<ScoreEntity>(entity =>
        {
            entity.ToTable("EvaluationScores", table => table.HasCheckConstraint("CK_Score_Range", "\"Score\" BETWEEN 1 AND 10"));
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.HasIndex(e => new { e.EvaluationId, e.MetricId }).IsUnique();
            entity.HasOne<MetricEntity>().WithMany().HasForeignKey(e => e.MetricId);
        });
        builder.Entity<PhotoEntity>(entity =>
        {
            entity.ToTable("VersionPhotos");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Caption).HasMaxLength(200);
            entity.Property(e => e.ContentType).HasMaxLength(40);
        });
        builder.Entity<CorrectionEntity>(entity =>
        {
            entity.ToTable("VersionCorrections");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.PreviousContent).HasColumnType("jsonb");
        });
        builder.Entity<StandardEntity>(entity =>
        {
            entity.ToTable("RecipeStandards");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();
        });
    }
}
