using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampaignManager.Web.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class Npc2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScenarioNpcs",
                schema: "games");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ScenarioNpcs",
                schema: "games",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScenarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastUpdated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScenarioNpcs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScenarioNpcs_Characters_CharacterId",
                        column: x => x.CharacterId,
                        principalSchema: "games",
                        principalTable: "Characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ScenarioNpcs_Scenarios_ScenarioId",
                        column: x => x.ScenarioId,
                        principalSchema: "games",
                        principalTable: "Scenarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScenarioNpcs_CharacterId",
                schema: "games",
                table: "ScenarioNpcs",
                column: "CharacterId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScenarioNpcs_ScenarioId",
                schema: "games",
                table: "ScenarioNpcs",
                column: "ScenarioId");
        }
    }
}