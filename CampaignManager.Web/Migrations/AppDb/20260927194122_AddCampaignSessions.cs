using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampaignManager.Web.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddCampaignSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CampaignSessions",
                schema: "games",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Summary = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: true),
                    KeeperNotes = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: true),
                    ScenarioId = table.Column<Guid>(type: "uuid", nullable: true),
                    ScenarioCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastUpdated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampaignSessions_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalSchema: "games",
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CampaignSessions_Scenarios_ScenarioId",
                        column: x => x.ScenarioId,
                        principalSchema: "games",
                        principalTable: "Scenarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignSessions_CampaignId_SessionDate",
                schema: "games",
                table: "CampaignSessions",
                columns: new[] { "CampaignId", "SessionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignSessions_ScenarioId",
                schema: "games",
                table: "CampaignSessions",
                column: "ScenarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CampaignSessions",
                schema: "games");
        }
    }
}
