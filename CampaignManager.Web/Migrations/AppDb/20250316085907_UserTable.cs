#nullable disable

using Microsoft.EntityFrameworkCore.Migrations;

namespace CampaignManager.Web.Migrations.AppDb;

/// <inheritdoc />
public partial class UserTable : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            "FK_CampaignPlayers_ApplicationUser_PlayerEmail",
            schema: "games",
            table: "CampaignPlayers");

        migrationBuilder.DropForeignKey(
            "FK_Campaigns_ApplicationUser_KeeperEmail",
            schema: "games",
            table: "Campaigns");

        migrationBuilder.DropTable(
            "ApplicationUser",
            "games");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            "ApplicationUser",
            schema: "games",
            columns: table => new
            {
                Id = table.Column<string>("text", nullable: false),
                AccessFailedCount = table.Column<int>("integer", nullable: false),
                ConcurrencyStamp = table.Column<string>("text", nullable: true),
                Email = table.Column<string>("text", nullable: true),
                EmailConfirmed = table.Column<bool>("boolean", nullable: false),
                LockoutEnabled = table.Column<bool>("boolean", nullable: false),
                LockoutEnd = table.Column<DateTimeOffset>("timestamp with time zone", nullable: true),
                NormalizedEmail = table.Column<string>("text", nullable: true),
                NormalizedUserName = table.Column<string>("text", nullable: true),
                PasswordHash = table.Column<string>("text", nullable: true),
                PhoneNumber = table.Column<string>("text", nullable: true),
                PhoneNumberConfirmed = table.Column<bool>("boolean", nullable: false),
                Role = table.Column<int>("integer", nullable: false),
                SecurityStamp = table.Column<string>("text", nullable: true),
                TwoFactorEnabled = table.Column<bool>("boolean", nullable: false),
                UserName = table.Column<string>("text", nullable: true)
            },
            constraints: table => { table.PrimaryKey("PK_ApplicationUser", x => x.Id); });

        migrationBuilder.AddForeignKey(
            "FK_CampaignPlayers_ApplicationUser_PlayerEmail",
            schema: "games",
            table: "CampaignPlayers",
            column: "PlayerEmail",
            principalSchema: "games",
            principalTable: "ApplicationUser",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            "FK_Campaigns_ApplicationUser_KeeperEmail",
            schema: "games",
            table: "Campaigns",
            column: "KeeperEmail",
            principalSchema: "games",
            principalTable: "ApplicationUser",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }
}