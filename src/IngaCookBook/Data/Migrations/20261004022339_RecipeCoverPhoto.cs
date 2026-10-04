using Microsoft.EntityFrameworkCore.Migrations;

namespace IngaCookBook.Migrations;

/// <inheritdoc />
internal sealed partial class RecipeCoverPhoto : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(name: "CoverPhotoId", table: "Recipes", type: "uuid", nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "CoverPhotoId", table: "Recipes");
    }
}
