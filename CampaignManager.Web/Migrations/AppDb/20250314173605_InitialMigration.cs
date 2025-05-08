#nullable disable

using CampaignManager.Web.Components.Features.Characters.Model;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CampaignManager.Web.Migrations.AppDb;

/// <inheritdoc />
public partial class InitialMigration : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            "games");

        migrationBuilder.CreateTable(
            "ApplicationUser",
            schema: "games",
            columns: table => new
            {
                Id = table.Column<string>("text", nullable: false),
                Role = table.Column<int>("integer", nullable: false),
                UserName = table.Column<string>("text", nullable: true),
                NormalizedUserName = table.Column<string>("text", nullable: true),
                Email = table.Column<string>("text", nullable: true),
                NormalizedEmail = table.Column<string>("text", nullable: true),
                EmailConfirmed = table.Column<bool>("boolean", nullable: false),
                PasswordHash = table.Column<string>("text", nullable: true),
                SecurityStamp = table.Column<string>("text", nullable: true),
                ConcurrencyStamp = table.Column<string>("text", nullable: true),
                PhoneNumber = table.Column<string>("text", nullable: true),
                PhoneNumberConfirmed = table.Column<bool>("boolean", nullable: false),
                TwoFactorEnabled = table.Column<bool>("boolean", nullable: false),
                LockoutEnd = table.Column<DateTimeOffset>("timestamp with time zone", nullable: true),
                LockoutEnabled = table.Column<bool>("boolean", nullable: false),
                AccessFailedCount = table.Column<int>("integer", nullable: false)
            },
            constraints: table => { table.PrimaryKey("PK_ApplicationUser", x => x.Id); });

        migrationBuilder.CreateTable(
            "Campaigns",
            schema: "games",
            columns: table => new
            {
                Id = table.Column<Guid>("uuid", nullable: false),
                Name = table.Column<string>("text", nullable: false),
                Status = table.Column<string>("text", nullable: false, defaultValue: "Planning"),
                KeeperEmail = table.Column<string>("text", nullable: true),
                CreatedAt = table.Column<DateTime>("timestamp with time zone", nullable: false),
                LastUpdated = table.Column<DateTime>("timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Campaigns", x => x.Id);
                table.ForeignKey(
                    "FK_Campaigns_ApplicationUser_KeeperEmail",
                    x => x.KeeperEmail,
                    principalSchema: "games",
                    principalTable: "ApplicationUser",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            "CampaignPlayers",
            schema: "games",
            columns: table => new
            {
                Id = table.Column<Guid>("uuid", nullable: false),
                CampaignId = table.Column<Guid>("uuid", nullable: false),
                PlayerEmail = table.Column<string>("text", nullable: false),
                CreatedAt = table.Column<DateTime>("timestamp with time zone", nullable: false),
                LastUpdated = table.Column<DateTime>("timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CampaignPlayers", x => x.Id);
                table.ForeignKey(
                    "FK_CampaignPlayers_ApplicationUser_PlayerEmail",
                    x => x.PlayerEmail,
                    principalSchema: "games",
                    principalTable: "ApplicationUser",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    "FK_CampaignPlayers_Campaigns_CampaignId",
                    x => x.CampaignId,
                    principalSchema: "games",
                    principalTable: "Campaigns",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            "Characters",
            schema: "games",
            columns: table => new
            {
                Id = table.Column<Guid>("uuid", nullable: false),
                CharacterName = table.Column<string>("character varying(100)", maxLength: 100, nullable: false),
                Character = table.Column<Character>("jsonb", nullable: false, comment: "JSON-представление персонажа со всеми характеристиками"),
                CampaignPlayerId = table.Column<Guid>("uuid", nullable: true),
                CreatedAt = table.Column<DateTime>("timestamp with time zone", nullable: false),
                LastUpdated = table.Column<DateTime>("timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Characters", x => x.Id);
                table.ForeignKey(
                    "FK_Characters_CampaignPlayers_CampaignPlayerId",
                    x => x.CampaignPlayerId,
                    principalSchema: "games",
                    principalTable: "CampaignPlayers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            "IX_CampaignPlayers_CampaignId",
            schema: "games",
            table: "CampaignPlayers",
            column: "CampaignId");

        migrationBuilder.CreateIndex(
            "IX_CampaignPlayers_PlayerEmail_CampaignId",
            schema: "games",
            table: "CampaignPlayers",
            columns: new[] { "PlayerEmail", "CampaignId" },
            unique: true);

        migrationBuilder.CreateIndex(
            "IX_Campaigns_KeeperEmail",
            schema: "games",
            table: "Campaigns",
            column: "KeeperEmail");

        migrationBuilder.CreateIndex(
            "IX_Characters_CampaignPlayerId",
            schema: "games",
            table: "Characters",
            column: "CampaignPlayerId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            "Characters",
            "games");

        migrationBuilder.DropTable(
            "CampaignPlayers",
            "games");

        migrationBuilder.DropTable(
            "Campaigns",
            "games");

        migrationBuilder.DropTable(
            "ApplicationUser",
            "games");
    }
}