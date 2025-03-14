using System;
using CampaignManager.Web.Model;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampaignManager.Web.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class InitialMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "games");

            migrationBuilder.CreateTable(
                name: "ApplicationUser",
                schema: "games",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    UserName = table.Column<string>(type: "text", nullable: true),
                    NormalizedUserName = table.Column<string>(type: "text", nullable: true),
                    Email = table.Column<string>(type: "text", nullable: true),
                    NormalizedEmail = table.Column<string>(type: "text", nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    SecurityStamp = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationUser", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Campaigns",
                schema: "games",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false, defaultValue: "Planning"),
                    KeeperEmail = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Campaigns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Campaigns_ApplicationUser_KeeperEmail",
                        column: x => x.KeeperEmail,
                        principalSchema: "games",
                        principalTable: "ApplicationUser",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CampaignPlayers",
                schema: "games",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerEmail = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignPlayers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampaignPlayers_ApplicationUser_PlayerEmail",
                        column: x => x.PlayerEmail,
                        principalSchema: "games",
                        principalTable: "ApplicationUser",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CampaignPlayers_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalSchema: "games",
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Characters",
                schema: "games",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Character = table.Column<Character>(type: "jsonb", nullable: false, comment: "JSON-представление персонажа со всеми характеристиками"),
                    CampaignPlayerId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Characters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Characters_CampaignPlayers_CampaignPlayerId",
                        column: x => x.CampaignPlayerId,
                        principalSchema: "games",
                        principalTable: "CampaignPlayers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignPlayers_CampaignId",
                schema: "games",
                table: "CampaignPlayers",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignPlayers_PlayerEmail_CampaignId",
                schema: "games",
                table: "CampaignPlayers",
                columns: new[] { "PlayerEmail", "CampaignId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Campaigns_KeeperEmail",
                schema: "games",
                table: "Campaigns",
                column: "KeeperEmail");

            migrationBuilder.CreateIndex(
                name: "IX_Characters_CampaignPlayerId",
                schema: "games",
                table: "Characters",
                column: "CampaignPlayerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Characters",
                schema: "games");

            migrationBuilder.DropTable(
                name: "CampaignPlayers",
                schema: "games");

            migrationBuilder.DropTable(
                name: "Campaigns",
                schema: "games");

            migrationBuilder.DropTable(
                name: "ApplicationUser",
                schema: "games");
        }
    }
}
