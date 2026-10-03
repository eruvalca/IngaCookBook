using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace IngaCookBook.Migrations;
/// <inheritdoc />
internal sealed partial class AddEvaluationCorrections : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "EvaluationCorrections",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                EvaluationId = table.Column<Guid>(type: "uuid", nullable: false),
                CorrectedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                Reason = table.Column<string>(type: "text", nullable: false),
                PreviousContent = table.Column<string>(type: "jsonb", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EvaluationCorrections", x => x.Id);
                table.ForeignKey(
                    name: "FK_EvaluationCorrections_BatchEvaluations_EvaluationId",
                    column: x => x.EvaluationId,
                    principalTable: "BatchEvaluations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_EvaluationCorrections_EvaluationId",
            table: "EvaluationCorrections",
            column: "EvaluationId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "EvaluationCorrections");
    }
}
