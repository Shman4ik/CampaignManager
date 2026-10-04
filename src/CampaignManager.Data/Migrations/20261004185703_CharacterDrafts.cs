using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampaignManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class CharacterDrafts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "character_drafts",
                schema: "cm",
                columns: table => new
                {
                    campaign_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    draft = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    draft_version = table.Column<int>(type: "integer", nullable: false),
                    step = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_character_drafts", x => new { x.campaign_id, x.owner_id });
                    table.CheckConstraint("ck_character_drafts_step", "step BETWEEN 0 AND 6");
                    table.ForeignKey(
                        name: "fk_character_drafts_campaign_members_campaign_id_owner_id",
                        columns: x => new { x.campaign_id, x.owner_id },
                        principalSchema: "cm",
                        principalTable: "campaign_members",
                        principalColumns: new[] { "campaign_id", "user_id" },
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "character_drafts",
                schema: "cm");
        }
    }
}
