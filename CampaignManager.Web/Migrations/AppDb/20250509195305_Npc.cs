using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampaignManager.Web.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class Npc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ScenarioNpcs_Characters_CharacterId",
                schema: "games",
                table: "ScenarioNpcs");

            migrationBuilder.DropColumn(
                name: "Description",
                schema: "games",
                table: "ScenarioNpcs");

            migrationBuilder.DropColumn(
                name: "Location",
                schema: "games",
                table: "ScenarioNpcs");

            migrationBuilder.DropColumn(
                name: "Notes",
                schema: "games",
                table: "ScenarioNpcs");

            migrationBuilder.DropColumn(
                name: "Role",
                schema: "games",
                table: "ScenarioNpcs");

            migrationBuilder.AlterColumn<Guid>(
                name: "ScenarioId",
                schema: "games",
                table: "ScenarioNpcs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "CharacterId",
                schema: "games",
                table: "ScenarioNpcs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ScenarioNpcs_Characters_CharacterId",
                schema: "games",
                table: "ScenarioNpcs",
                column: "CharacterId",
                principalSchema: "games",
                principalTable: "Characters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ScenarioNpcs_Characters_CharacterId",
                schema: "games",
                table: "ScenarioNpcs");

            migrationBuilder.AlterColumn<Guid>(
                name: "ScenarioId",
                schema: "games",
                table: "ScenarioNpcs",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "CharacterId",
                schema: "games",
                table: "ScenarioNpcs",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                schema: "games",
                table: "ScenarioNpcs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Location",
                schema: "games",
                table: "ScenarioNpcs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                schema: "games",
                table: "ScenarioNpcs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Role",
                schema: "games",
                table: "ScenarioNpcs",
                type: "text",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ScenarioNpcs_Characters_CharacterId",
                schema: "games",
                table: "ScenarioNpcs",
                column: "CharacterId",
                principalSchema: "games",
                principalTable: "Characters",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
