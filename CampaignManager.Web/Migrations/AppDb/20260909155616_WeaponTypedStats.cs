using System;
using CampaignManager.Web.Components.Features.Weapons.Model;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampaignManager.Web.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class WeaponTypedStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<WeaponAmmoInfo>(
                name: "AmmoInfo",
                schema: "games",
                table: "Weapons",
                type: "jsonb",
                nullable: true,
                comment: "Структурированный боезапас (авто-парсинг поля Ammo)");

            migrationBuilder.AddColumn<WeaponAttacksInfo>(
                name: "AttacksInfo",
                schema: "games",
                table: "Weapons",
                type: "jsonb",
                nullable: true,
                comment: "Структурированное число атак (авто-парсинг поля Attacks)");

            migrationBuilder.AddColumn<Guid>(
                name: "CatalogWeaponId",
                schema: "games",
                table: "Weapons",
                type: "uuid",
                nullable: true,
                comment: "Заполнено только у копии в листе персонажа; у каталожной строки — null");

            migrationBuilder.AddColumn<WeaponCostInfo>(
                name: "CostInfo",
                schema: "games",
                table: "Weapons",
                type: "jsonb",
                nullable: true,
                comment: "Структурированная стоимость (авто-парсинг поля Cost)");

            migrationBuilder.AddColumn<int>(
                name: "MalfunctionThreshold",
                schema: "games",
                table: "Weapons",
                type: "integer",
                nullable: true,
                comment: "Порог осечки числом (авто-парсинг поля Malfunction)");

            migrationBuilder.AddColumn<WeaponRangeInfo>(
                name: "RangeInfo",
                schema: "games",
                table: "Weapons",
                type: "jsonb",
                nullable: true,
                comment: "Структурированная дальность (авто-парсинг поля Range)");

            migrationBuilder.AddColumn<Guid>(
                name: "SkillId",
                schema: "games",
                table: "Weapons",
                type: "uuid",
                nullable: true,
                comment: "Навык из справочника Skills; без FK — см. Weapon.SkillId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AmmoInfo",
                schema: "games",
                table: "Weapons");

            migrationBuilder.DropColumn(
                name: "AttacksInfo",
                schema: "games",
                table: "Weapons");

            migrationBuilder.DropColumn(
                name: "CatalogWeaponId",
                schema: "games",
                table: "Weapons");

            migrationBuilder.DropColumn(
                name: "CostInfo",
                schema: "games",
                table: "Weapons");

            migrationBuilder.DropColumn(
                name: "MalfunctionThreshold",
                schema: "games",
                table: "Weapons");

            migrationBuilder.DropColumn(
                name: "RangeInfo",
                schema: "games",
                table: "Weapons");

            migrationBuilder.DropColumn(
                name: "SkillId",
                schema: "games",
                table: "Weapons");
        }
    }
}
