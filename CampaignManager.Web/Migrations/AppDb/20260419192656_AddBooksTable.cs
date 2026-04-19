using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampaignManager.Web.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddBooksTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Books",
                schema: "games",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    BookType = table.Column<string>(type: "text", nullable: false),
                    AlternativeNames = table.Column<string>(type: "jsonb", nullable: false),
                    Language = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Year = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Author = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    SanityLoss = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CthulhuMythosInitial = table.Column<int>(type: "integer", nullable: true),
                    CthulhuMythosFull = table.Column<int>(type: "integer", nullable: true),
                    MythosRating = table.Column<int>(type: "integer", nullable: true),
                    StudyWeeks = table.Column<int>(type: "integer", nullable: true),
                    OccultismBonus = table.Column<int>(type: "integer", nullable: true),
                    Description = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    PossibleSpells = table.Column<string>(type: "jsonb", nullable: false),
                    ImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastUpdated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Books", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Books_Name",
                schema: "games",
                table: "Books",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Books",
                schema: "games");
        }
    }
}
