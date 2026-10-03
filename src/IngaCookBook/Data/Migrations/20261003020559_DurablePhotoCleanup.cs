using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngaCookBook.Migrations;
/// <inheritdoc />
internal sealed partial class DurablePhotoCleanup : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "PhotoCleanupJobs",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Prefix = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                QueuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                NextAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PhotoCleanupJobs", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_PhotoCleanupJobs_NextAttemptAt",
            table: "PhotoCleanupJobs",
            column: "NextAttemptAt");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "PhotoCleanupJobs");
    }
}
