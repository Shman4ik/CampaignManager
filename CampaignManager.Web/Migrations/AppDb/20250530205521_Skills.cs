using CampaignManager.Web.Components.Features.Scenarios.Model;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CampaignManager.Web.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class Skills : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<ICollection<ScenarioItem>>(
                name: "ScenarioItems",
                schema: "games",
                table: "Scenarios",
                type: "jsonb",
                nullable: false,
                defaultValue: new List<ScenarioItem>(),
                oldClrType: typeof(ICollection<ScenarioItem>),
                oldType: "jsonb",
                oldDefaultValue: new List<ScenarioItem>());

            migrationBuilder.AlterColumn<ICollection<ScenarioCreature>>(
                name: "ScenarioCreatures",
                schema: "games",
                table: "Scenarios",
                type: "jsonb",
                nullable: false,
                defaultValue: new List<ScenarioCreature>(),
                oldClrType: typeof(ICollection<ScenarioCreature>),
                oldType: "jsonb",
                oldDefaultValue: new List<ScenarioCreature>());

            migrationBuilder.CreateTable(
                name: "Skills",
                schema: "games",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    BaseValue = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    IsUncommon = table.Column<bool>(type: "boolean", nullable: false),
                    UsageExamples = table.Column<List<string>>(type: "jsonb", nullable: false),
                    FailureConsequences = table.Column<List<string>>(type: "jsonb", nullable: false),
                    TimeRequired = table.Column<string>(type: "text", nullable: false),
                    CanRetry = table.Column<bool>(type: "boolean", nullable: false),
                    OpposingSkills = table.Column<List<string>>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastUpdated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table => { table.PrimaryKey("PK_Skills", x => x.Id); });

            migrationBuilder.CreateIndex(
                name: "IX_Skills_Name",
                schema: "games",
                table: "Skills",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Skills",
                schema: "games");

            migrationBuilder.AlterColumn<ICollection<ScenarioItem>>(
                name: "ScenarioItems",
                schema: "games",
                table: "Scenarios",
                type: "jsonb",
                nullable: false,
                defaultValue: new List<ScenarioItem>(),
                oldClrType: typeof(ICollection<ScenarioItem>),
                oldType: "jsonb",
                oldDefaultValue: new List<ScenarioItem>());

            migrationBuilder.AlterColumn<ICollection<ScenarioCreature>>(
                name: "ScenarioCreatures",
                schema: "games",
                table: "Scenarios",
                type: "jsonb",
                nullable: false,
                defaultValue: new List<ScenarioCreature>(),
                oldClrType: typeof(ICollection<ScenarioCreature>),
                oldType: "jsonb",
                oldDefaultValue: new List<ScenarioCreature>());
        }
    }
}