using CampaignManager.Web.Components.Features.Weapons.Model;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampaignManager.Web.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddWeaponDamageInfo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<WeaponDamageInfo>(
                name: "DamageInfo",
                schema: "games",
                table: "Weapons",
                type: "jsonb",
                nullable: true,
                comment: "Структурированная информация об уроне (авто-парсинг поля Damage)");

            migrationBuilder.AddColumn<bool>(
                name: "IsImpaling",
                schema: "games",
                table: "Weapons",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DamageInfo",
                schema: "games",
                table: "Weapons");

            migrationBuilder.DropColumn(
                name: "IsImpaling",
                schema: "games",
                table: "Weapons");
        }
    }
}
