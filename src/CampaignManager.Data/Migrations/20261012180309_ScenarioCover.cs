using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampaignManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class ScenarioCover : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "cover_file_id",
                schema: "cm",
                table: "scenarios",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_scenarios_cover_file_id",
                schema: "cm",
                table: "scenarios",
                column: "cover_file_id");

            migrationBuilder.AddForeignKey(
                name: "fk_scenarios_files_cover_file_id",
                schema: "cm",
                table: "scenarios",
                column: "cover_file_id",
                principalSchema: "cm",
                principalTable: "files",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_scenarios_files_cover_file_id",
                schema: "cm",
                table: "scenarios");

            migrationBuilder.DropIndex(
                name: "ix_scenarios_cover_file_id",
                schema: "cm",
                table: "scenarios");

            migrationBuilder.DropColumn(
                name: "cover_file_id",
                schema: "cm",
                table: "scenarios");
        }
    }
}
