using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampaignManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class WeaponImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "weapon_images",
                schema: "cm",
                columns: table => new
                {
                    weapon_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ord = table.Column<int>(type: "integer", nullable: false),
                    file_id = table.Column<Guid>(type: "uuid", nullable: false),
                    caption = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_weapon_images", x => new { x.weapon_id, x.ord });
                    table.ForeignKey(
                        name: "fk_weapon_images_files_file_id",
                        column: x => x.file_id,
                        principalSchema: "cm",
                        principalTable: "files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_weapon_images_weapons_weapon_id",
                        column: x => x.weapon_id,
                        principalSchema: "cm",
                        principalTable: "weapons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_weapon_images_file_id",
                schema: "cm",
                table: "weapon_images",
                column: "file_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "weapon_images",
                schema: "cm");
        }
    }
}
