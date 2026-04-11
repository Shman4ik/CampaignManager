using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampaignManager.Web.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddScenarioAnnouncementFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AnnouncementText",
                schema: "games",
                table: "Scenarios",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPublished",
                schema: "games",
                table: "Scenarios",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ScheduledDate",
                schema: "games",
                table: "Scenarios",
                type: "timestamp without time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnnouncementText",
                schema: "games",
                table: "Scenarios");

            migrationBuilder.DropColumn(
                name: "IsPublished",
                schema: "games",
                table: "Scenarios");

            migrationBuilder.DropColumn(
                name: "ScheduledDate",
                schema: "games",
                table: "Scenarios");
        }
    }
}
