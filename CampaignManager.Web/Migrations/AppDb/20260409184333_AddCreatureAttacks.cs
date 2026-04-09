using System.Collections.Generic;
using CampaignManager.Web.Components.Features.Bestiary.Model;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampaignManager.Web.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddCreatureAttacks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<CreatureAttack>>(
                name: "Attacks",
                schema: "games",
                table: "Creatures",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Attacks",
                schema: "games",
                table: "Creatures");
        }
    }
}
