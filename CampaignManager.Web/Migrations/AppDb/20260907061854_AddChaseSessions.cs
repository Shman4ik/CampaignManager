using System;
using CampaignManager.Web.Components.Features.Chase.Model;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampaignManager.Web.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddChaseSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChaseSessions",
                schema: "games",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: true),
                    KeeperEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    State = table.Column<ChaseSnapshot>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastUpdated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChaseSessions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChaseSessions_KeeperEmail_CampaignId",
                schema: "games",
                table: "ChaseSessions",
                columns: new[] { "KeeperEmail", "CampaignId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChaseSessions",
                schema: "games");
        }
    }
}
