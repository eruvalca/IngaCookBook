using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngaCookBook.Migrations;
/// <inheritdoc />
internal sealed partial class AddBatchCorrections : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "Position",
            table: "RecipeBatches",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        // Retain the old chronological order for existing batches, then keep
        // their positions stable when a recorded date is corrected.
        migrationBuilder.Sql("""
            WITH ordered AS (
                SELECT "Id", CAST(ROW_NUMBER() OVER (
                    PARTITION BY "VersionId" ORDER BY "MadeAt", "Id") - 1 AS integer) AS "Position"
                FROM "RecipeBatches"
            )
            UPDATE "RecipeBatches" AS batch
            SET "Position" = ordered."Position"
            FROM ordered WHERE batch."Id" = ordered."Id";
            """);

        migrationBuilder.CreateTable(
            name: "BatchCorrections",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                CorrectedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                Reason = table.Column<string>(type: "text", nullable: false),
                PreviousMadeAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                PreviousNotes = table.Column<string>(type: "text", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_BatchCorrections", x => x.Id);
                table.ForeignKey(
                    name: "FK_BatchCorrections_RecipeBatches_BatchId",
                    column: x => x.BatchId,
                    principalTable: "RecipeBatches",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_BatchCorrections_BatchId",
            table: "BatchCorrections",
            column: "BatchId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "BatchCorrections");

        migrationBuilder.DropColumn(
            name: "Position",
            table: "RecipeBatches");
    }
}
