using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampaignManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class ChaseManyActive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "encounters_one_active",
                schema: "cm",
                table: "encounters");

            migrationBuilder.CreateIndex(
                name: "encounters_one_active_combat",
                schema: "cm",
                table: "encounters",
                columns: new[] { "keeper_id", "campaign_id" },
                unique: true,
                filter: "status = 'Active' AND kind = 'Combat'")
                .Annotation("Npgsql:NullsDistinct", false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "encounters_one_active_combat",
                schema: "cm",
                table: "encounters");

            migrationBuilder.CreateIndex(
                name: "encounters_one_active",
                schema: "cm",
                table: "encounters",
                columns: new[] { "keeper_id", "kind", "campaign_id" },
                unique: true,
                filter: "status = 'Active'")
                .Annotation("Npgsql:NullsDistinct", false);
        }
    }
}
