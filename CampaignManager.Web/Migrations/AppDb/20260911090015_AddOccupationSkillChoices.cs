using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampaignManager.Web.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddOccupationSkillChoices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Слоты «выбрать N навыков из перечисленных» (стр. 38–39). Колонка NOT NULL,
            // поэтому у существующих строк значение берётся из умолчания — пустой список.
            migrationBuilder.AddColumn<string>(
                name: "SkillChoices",
                schema: "games",
                table: "Occupations",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SkillChoices",
                schema: "games",
                table: "Occupations");
        }
    }
}
