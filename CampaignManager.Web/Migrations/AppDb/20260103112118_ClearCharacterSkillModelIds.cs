using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampaignManager.Web.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class ClearCharacterSkillModelIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Clear all SkillModelId values from character JSONB data since we changed from int to Guid
            // This is necessary because old integer IDs can't be converted to Guids
            // Users will need to run the skill migration again after importing skills from JSON
            migrationBuilder.Sql(@"
                UPDATE games.""Characters""
                SET ""Character"" = jsonb_set(
                    ""Character"",
                    '{Skills,SkillGroups}',
                    (
                        SELECT jsonb_agg(
                            jsonb_set(
                                skill_group,
                                '{Skills}',
                                (
                                    SELECT jsonb_agg(
                                        skill - 'SkillModelId'
                                    )
                                    FROM jsonb_array_elements(skill_group->'Skills') AS skill
                                )
                            )
                        )
                        FROM jsonb_array_elements(""Character""->'Skills'->'SkillGroups') AS skill_group
                    )
                )
                WHERE ""Character"" ? 'Skills'
                AND ""Character""->'Skills' ? 'SkillGroups';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
