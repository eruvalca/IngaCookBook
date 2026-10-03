using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngaCookBook.Migrations;

/// <inheritdoc />
internal sealed partial class RecipeNotebook : Migration
{
    private static readonly string[] _evaluationMetricColumns = ["EvaluationId", "MetricId"];
    private static readonly string[] _versionRowColumns = ["VersionId", "RowId"];
    private static readonly string[] _workspaceUpdatedColumns = ["WorkspaceId", "UpdatedAt"];
    private static readonly string[] _recipeNumberColumns = ["RecipeId", "Number"];
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Workspaces",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OwnerId = table.Column<string>(type: "text", nullable: false),
                Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Workspaces", x => x.Id);
                table.ForeignKey(
                    name: "FK_Workspaces_AspNetUsers_OwnerId",
                    column: x => x.OwnerId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "Recipes",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                Revision = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                StandardVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                OriginRecipeId = table.Column<Guid>(type: "uuid", nullable: true),
                OriginVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Recipes", x => x.Id);
                table.ForeignKey(
                    name: "FK_Recipes_Workspaces_WorkspaceId",
                    column: x => x.WorkspaceId,
                    principalTable: "Workspaces",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "EvaluationMetrics",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                RecipeId = table.Column<Guid>(type: "uuid", nullable: false),
                Position = table.Column<int>(type: "integer", nullable: false),
                Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EvaluationMetrics", x => x.Id);
                table.ForeignKey(
                    name: "FK_EvaluationMetrics_Recipes_RecipeId",
                    column: x => x.RecipeId,
                    principalTable: "Recipes",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "RecipeStandards",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                RecipeId = table.Column<Guid>(type: "uuid", nullable: false),
                VersionId = table.Column<Guid>(type: "uuid", nullable: false),
                SelectedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RecipeStandards", x => x.Id);
                table.ForeignKey(
                    name: "FK_RecipeStandards_Recipes_RecipeId",
                    column: x => x.RecipeId,
                    principalTable: "Recipes",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "RecipeVersions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                RecipeId = table.Column<Guid>(type: "uuid", nullable: false),
                ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                Number = table.Column<int>(type: "integer", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                IsLocked = table.Column<bool>(type: "boolean", nullable: false),
                Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Notes = table.Column<string>(type: "text", nullable: false),
                TargetMetricId = table.Column<Guid>(type: "uuid", nullable: true),
                Hypothesis = table.Column<string>(type: "text", nullable: false),
                RelatedChanges = table.Column<string>(type: "text", nullable: false),
                Yield = table.Column<decimal>(type: "numeric", nullable: true),
                YieldUnit = table.Column<string>(type: "text", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RecipeVersions", x => x.Id);
                table.ForeignKey(
                    name: "FK_RecipeVersions_RecipeVersions_ParentId",
                    column: x => x.ParentId,
                    principalTable: "RecipeVersions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_RecipeVersions_Recipes_RecipeId",
                    column: x => x.RecipeId,
                    principalTable: "Recipes",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "PreparationSteps",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                VersionId = table.Column<Guid>(type: "uuid", nullable: false),
                RowId = table.Column<Guid>(type: "uuid", nullable: false),
                Position = table.Column<int>(type: "integer", nullable: false),
                Instruction = table.Column<string>(type: "text", nullable: false),
                Notes = table.Column<string>(type: "text", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PreparationSteps", x => x.Id);
                table.ForeignKey(
                    name: "FK_PreparationSteps_RecipeVersions_VersionId",
                    column: x => x.VersionId,
                    principalTable: "RecipeVersions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "RecipeBatches",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                VersionId = table.Column<Guid>(type: "uuid", nullable: false),
                MadeAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                Notes = table.Column<string>(type: "text", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RecipeBatches", x => x.Id);
                table.ForeignKey(
                    name: "FK_RecipeBatches_RecipeVersions_VersionId",
                    column: x => x.VersionId,
                    principalTable: "RecipeVersions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "VersionCorrections",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                VersionId = table.Column<Guid>(type: "uuid", nullable: false),
                CorrectedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                Reason = table.Column<string>(type: "text", nullable: false),
                PreviousContent = table.Column<string>(type: "jsonb", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_VersionCorrections", x => x.Id);
                table.ForeignKey(
                    name: "FK_VersionCorrections_RecipeVersions_VersionId",
                    column: x => x.VersionId,
                    principalTable: "RecipeVersions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "VersionIngredients",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                VersionId = table.Column<Guid>(type: "uuid", nullable: false),
                RowId = table.Column<Guid>(type: "uuid", nullable: false),
                Position = table.Column<int>(type: "integer", nullable: false),
                Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                Quantity = table.Column<decimal>(type: "numeric", nullable: true),
                Unit = table.Column<string>(type: "text", nullable: false),
                PurchaseQuantity = table.Column<decimal>(type: "numeric", nullable: true),
                PurchaseUnit = table.Column<string>(type: "text", nullable: false),
                PurchasePrice = table.Column<decimal>(type: "numeric", nullable: true),
                LinkedRecipeId = table.Column<Guid>(type: "uuid", nullable: true),
                LinkedVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                LinkedSnapshot = table.Column<string>(type: "jsonb", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_VersionIngredients", x => x.Id);
                table.CheckConstraint("CK_Ingredient_Amounts", "\"Quantity\" > 0 AND \"PurchaseQuantity\" > 0 AND \"PurchasePrice\" >= 0");
                table.ForeignKey(
                    name: "FK_VersionIngredients_RecipeVersions_VersionId",
                    column: x => x.VersionId,
                    principalTable: "RecipeVersions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "VersionPhotos",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                VersionId = table.Column<Guid>(type: "uuid", nullable: false),
                Caption = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                ContentType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_VersionPhotos", x => x.Id);
                table.ForeignKey(
                    name: "FK_VersionPhotos_RecipeVersions_VersionId",
                    column: x => x.VersionId,
                    principalTable: "RecipeVersions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "BatchEvaluations",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                TastedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                Notes = table.Column<string>(type: "text", nullable: false),
                NextIdea = table.Column<string>(type: "text", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_BatchEvaluations", x => x.Id);
                table.ForeignKey(
                    name: "FK_BatchEvaluations_RecipeBatches_BatchId",
                    column: x => x.BatchId,
                    principalTable: "RecipeBatches",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "EvaluationScores",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                EvaluationId = table.Column<Guid>(type: "uuid", nullable: false),
                MetricId = table.Column<Guid>(type: "uuid", nullable: false),
                Score = table.Column<int>(type: "integer", nullable: true),
                Notes = table.Column<string>(type: "text", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EvaluationScores", x => x.Id);
                table.CheckConstraint("CK_Score_Range", "\"Score\" BETWEEN 1 AND 10");
                table.ForeignKey(
                    name: "FK_EvaluationScores_BatchEvaluations_EvaluationId",
                    column: x => x.EvaluationId,
                    principalTable: "BatchEvaluations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_EvaluationScores_EvaluationMetrics_MetricId",
                    column: x => x.MetricId,
                    principalTable: "EvaluationMetrics",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_BatchEvaluations_BatchId",
            table: "BatchEvaluations",
            column: "BatchId");

        migrationBuilder.CreateIndex(
            name: "IX_EvaluationMetrics_RecipeId",
            table: "EvaluationMetrics",
            column: "RecipeId");

        migrationBuilder.CreateIndex(
            name: "IX_EvaluationScores_EvaluationId_MetricId",
            table: "EvaluationScores",
            columns: _evaluationMetricColumns,
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_EvaluationScores_MetricId",
            table: "EvaluationScores",
            column: "MetricId");

        migrationBuilder.CreateIndex(
            name: "IX_PreparationSteps_VersionId_RowId",
            table: "PreparationSteps",
            columns: _versionRowColumns,
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_RecipeBatches_VersionId",
            table: "RecipeBatches",
            column: "VersionId");

        migrationBuilder.CreateIndex(
            name: "IX_Recipes_WorkspaceId_UpdatedAt",
            table: "Recipes",
            columns: _workspaceUpdatedColumns);

        migrationBuilder.CreateIndex(
            name: "IX_RecipeStandards_RecipeId",
            table: "RecipeStandards",
            column: "RecipeId");

        migrationBuilder.CreateIndex(
            name: "IX_RecipeVersions_ParentId",
            table: "RecipeVersions",
            column: "ParentId");

        migrationBuilder.CreateIndex(
            name: "IX_RecipeVersions_RecipeId_Number",
            table: "RecipeVersions",
            columns: _recipeNumberColumns,
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_VersionCorrections_VersionId",
            table: "VersionCorrections",
            column: "VersionId");

        migrationBuilder.CreateIndex(
            name: "IX_VersionIngredients_VersionId_RowId",
            table: "VersionIngredients",
            columns: _versionRowColumns,
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_VersionPhotos_VersionId",
            table: "VersionPhotos",
            column: "VersionId");

        migrationBuilder.CreateIndex(
            name: "IX_Workspaces_OwnerId",
            table: "Workspaces",
            column: "OwnerId",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "EvaluationScores");

        migrationBuilder.DropTable(
            name: "PreparationSteps");

        migrationBuilder.DropTable(
            name: "RecipeStandards");

        migrationBuilder.DropTable(
            name: "VersionCorrections");

        migrationBuilder.DropTable(
            name: "VersionIngredients");

        migrationBuilder.DropTable(
            name: "VersionPhotos");

        migrationBuilder.DropTable(
            name: "BatchEvaluations");

        migrationBuilder.DropTable(
            name: "EvaluationMetrics");

        migrationBuilder.DropTable(
            name: "RecipeBatches");

        migrationBuilder.DropTable(
            name: "RecipeVersions");

        migrationBuilder.DropTable(
            name: "Recipes");

        migrationBuilder.DropTable(
            name: "Workspaces");
    }
}
