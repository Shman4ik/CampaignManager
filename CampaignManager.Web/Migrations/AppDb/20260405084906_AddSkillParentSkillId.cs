using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampaignManager.Web.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddSkillParentSkillId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ParentSkillId",
                schema: "games",
                table: "Skills",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Skills_ParentSkillId",
                schema: "games",
                table: "Skills",
                column: "ParentSkillId");

            migrationBuilder.AddForeignKey(
                name: "FK_Skills_Skills_ParentSkillId",
                schema: "games",
                table: "Skills",
                column: "ParentSkillId",
                principalSchema: "games",
                principalTable: "Skills",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // Data migration: link specialization skills to their parent skill records.
            // Specializations are detected by the " (…)" suffix pattern.
            // If a parent record does not exist, a new abstract parent is inserted.
            migrationBuilder.Sql(@"
DO $$
DECLARE
    base_name  text;
    parent_id  uuid;
    spec_cat   text;
    now_ts     timestamptz := NOW();
BEGIN
    FOR base_name IN
        SELECT DISTINCT regexp_replace(""Name"", ' \(.*$', '')
        FROM games.""Skills""
        WHERE ""Name"" ~ ' \(.+\)$'
    LOOP
        SELECT ""Id"" INTO parent_id
        FROM games.""Skills""
        WHERE ""Name"" = base_name
        LIMIT 1;

        IF parent_id IS NULL THEN
            parent_id := gen_random_uuid();

            SELECT ""Category"" INTO spec_cat
            FROM games.""Skills""
            WHERE ""Name"" ~ ('^' || regexp_replace(base_name, '([.*+?^${}()|[\]\\])', '\\\1', 'g') || ' \(')
            LIMIT 1;

            INSERT INTO games.""Skills""
                (""Id"", ""Name"", ""BaseValue"", ""Description"", ""Category"",
                 ""IsUncommon"", ""UsageExamples"", ""FailureConsequences"",
                 ""TimeRequired"", ""CanRetry"", ""OpposingSkills"",
                 ""ParentSkillId"", ""CreatedAt"", ""LastUpdated"")
            VALUES (
                parent_id, base_name, 0, '',
                COALESCE(spec_cat, 'Knowledge'),
                false, '[]'::jsonb, '[]'::jsonb, '', true, '[]'::jsonb,
                NULL, now_ts, now_ts
            );
        END IF;

        UPDATE games.""Skills""
        SET ""ParentSkillId"" = parent_id
        WHERE regexp_replace(""Name"", ' \(.*$', '') = base_name
          AND ""Name"" ~ ' \(.+\)$';
    END LOOP;
END;
$$;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Skills_Skills_ParentSkillId",
                schema: "games",
                table: "Skills");

            migrationBuilder.DropIndex(
                name: "IX_Skills_ParentSkillId",
                schema: "games",
                table: "Skills");

            migrationBuilder.DropColumn(
                name: "ParentSkillId",
                schema: "games",
                table: "Skills");
        }
    }
}
