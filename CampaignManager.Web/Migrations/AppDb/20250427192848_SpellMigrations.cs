#nullable disable

using Microsoft.EntityFrameworkCore.Migrations;

namespace CampaignManager.Web.Migrations.AppDb;

/// <inheritdoc />
public partial class SpellMigrations : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            "Spells",
            schema: "games",
            columns: table => new
            {
                Id = table.Column<Guid>("uuid", nullable: false),
                Name = table.Column<string>("text", nullable: false),
                Cost = table.Column<string>("text", nullable: true),
                CastingTime = table.Column<string>("text", nullable: true),
                Description = table.Column<string>("text", nullable: false),
                AlternativeNames = table.Column<List<string>>("jsonb", nullable: false),
                SpellType = table.Column<string>("text", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>("timestamp with time zone", nullable: false),
                LastUpdated = table.Column<DateTimeOffset>("timestamp with time zone", nullable: false)
            },
            constraints: table => { table.PrimaryKey("PK_Spells", x => x.Id); });

        migrationBuilder.CreateIndex(
            "IX_Spells_Name",
            schema: "games",
            table: "Spells",
            column: "Name",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            "Spells",
            "games");
    }
}