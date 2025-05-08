#nullable disable

using Microsoft.EntityFrameworkCore.Migrations;

namespace CampaignManager.Web.Migrations.AppDb;

/// <inheritdoc />
public partial class CharacterStatus : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<Guid>(
            "CampaignPlayerId",
            schema: "games",
            table: "Characters",
            type: "uuid",
            nullable: false,
            defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
            oldClrType: typeof(Guid),
            oldType: "uuid",
            oldNullable: true);

        migrationBuilder.AddColumn<string>(
            "Status",
            schema: "games",
            table: "Characters",
            type: "text",
            nullable: false,
            defaultValue: "Active");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            "Status",
            schema: "games",
            table: "Characters");

        migrationBuilder.AlterColumn<Guid>(
            "CampaignPlayerId",
            schema: "games",
            table: "Characters",
            type: "uuid",
            nullable: true,
            oldClrType: typeof(Guid),
            oldType: "uuid");
    }
}