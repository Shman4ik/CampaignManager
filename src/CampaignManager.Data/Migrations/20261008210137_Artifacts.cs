using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampaignManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class Artifacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "artifacts",
                schema: "cm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", nullable: true),
                    name = table.Column<string>(type: "text", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    used_by = table.Column<List<string>>(type: "text[]", nullable: false, defaultValueSql: "'{}'"),
                    rule = table.Column<string>(type: "text", nullable: true),
                    description = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    source = table.Column<string>(type: "text", nullable: true),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_artifacts", x => x.id);
                    table.CheckConstraint("ck_artifacts_kind", "kind IN ('Device', 'Weapon', 'Armor', 'Substance', 'Relic', 'Place', 'Other')");
                    table.ForeignKey(
                        name: "fk_artifacts_users_created_by_id",
                        column: x => x.created_by_id,
                        principalSchema: "cm",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "artifact_images",
                schema: "cm",
                columns: table => new
                {
                    artifact_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ord = table.Column<int>(type: "integer", nullable: false),
                    file_id = table.Column<Guid>(type: "uuid", nullable: false),
                    caption = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_artifact_images", x => new { x.artifact_id, x.ord });
                    table.ForeignKey(
                        name: "fk_artifact_images_artifacts_artifact_id",
                        column: x => x.artifact_id,
                        principalSchema: "cm",
                        principalTable: "artifacts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_artifact_images_files_file_id",
                        column: x => x.file_id,
                        principalSchema: "cm",
                        principalTable: "files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_artifact_images_file_id",
                schema: "cm",
                table: "artifact_images",
                column: "file_id");

            migrationBuilder.CreateIndex(
                name: "artifacts_name_trgm",
                schema: "cm",
                table: "artifacts",
                column: "name")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_artifacts_code",
                schema: "cm",
                table: "artifacts",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_artifacts_created_by_id",
                schema: "cm",
                table: "artifacts",
                column: "created_by_id");

            // Имя справочника уникально без учёта регистра — как у остальных (InitialCmSchema): EF этого не выражает.
            migrationBuilder.Sql("CREATE UNIQUE INDEX artifacts_name ON cm.artifacts (lower(name));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "artifact_images",
                schema: "cm");

            migrationBuilder.DropTable(
                name: "artifacts",
                schema: "cm");
        }
    }
}
