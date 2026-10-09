using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampaignManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class SkillImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "image_file_id",
                schema: "cm",
                table: "skills",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_skills_image_file_id",
                schema: "cm",
                table: "skills",
                column: "image_file_id");

            migrationBuilder.AddForeignKey(
                name: "fk_skills_files_image_file_id",
                schema: "cm",
                table: "skills",
                column: "image_file_id",
                principalSchema: "cm",
                principalTable: "files",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_skills_files_image_file_id",
                schema: "cm",
                table: "skills");

            migrationBuilder.DropIndex(
                name: "ix_skills_image_file_id",
                schema: "cm",
                table: "skills");

            migrationBuilder.DropColumn(
                name: "image_file_id",
                schema: "cm",
                table: "skills");
        }
    }
}
