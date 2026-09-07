using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampaignManager.Web.Migrations.AppDb
{
    /// <summary>
    ///     Упрощение модели НПС: вид персонажа переезжает из JSONB в колонку <c>Kind</c>,
    ///     участие НПС в сценарии — из копий листов в связь <c>ScenarioNpcs</c>,
    ///     а владелец НПС описывается колонкой <c>CampaignId</c>.
    /// </summary>
    /// <inheritdoc />
    public partial class SimplifyNpcModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Kind",
                schema: "games",
                table: "Characters",
                type: "text",
                nullable: false,
                defaultValue: "PlayerCharacter");

            migrationBuilder.AddColumn<Guid>(
                name: "CampaignId",
                schema: "games",
                table: "Characters",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ScenarioNpcs",
                schema: "games",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScenarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false),
                    Count = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastUpdated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScenarioNpcs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScenarioNpcs_Characters_CharacterId",
                        column: x => x.CharacterId,
                        principalSchema: "games",
                        principalTable: "Characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ScenarioNpcs_Scenarios_ScenarioId",
                        column: x => x.ScenarioId,
                        principalSchema: "games",
                        principalTable: "Scenarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // ── Перенос данных ────────────────────────────────────────────────
            // Вид персонажа раньше жил внутри JSONB (CharacterType: 1 — НПС) вместе со
            // статусом «Template» и набором внешних ключей. Читаем оба возможных
            // написания ключа и оба представления перечисления: сериализация JSONB за
            // время жизни базы могла отличаться.
            migrationBuilder.Sql("""
                UPDATE games."Characters"
                SET "Kind" = CASE
                    WHEN COALESCE("Character" ->> 'CharacterType', "Character" ->> 'characterType')
                         IN ('1', 'NonPlayerCharacter') THEN 'Npc'
                    WHEN "ScenarioId" IS NOT NULL OR "Status" = 'Template' THEN 'Pregen'
                    ELSE 'PlayerCharacter'
                END;
                """);

            // Каждая копия НПС, привязанная к сценарию, становится строкой состава.
            migrationBuilder.Sql("""
                INSERT INTO games."ScenarioNpcs" ("Id", "ScenarioId", "CharacterId", "Role", "Count", "CreatedAt", "LastUpdated")
                SELECT gen_random_uuid(), c."ScenarioId", c."Id", COALESCE(c."NpcRole", 'Neutral'), 1, now(), now()
                FROM games."Characters" c
                WHERE c."Kind" = 'Npc' AND c."ScenarioId" IS NOT NULL;
                """);

            // Копия, сделанная прежним «привязать к сценарию», хранит идентификатор
            // исходного листа внутри JSONB. Если копия не расходилась с оригиналом,
            // сценарий связываем с оригиналом, а копию удаляем — иначе оставляем как
            // самостоятельного НПС, чтобы ничего не потерять.
            migrationBuilder.Sql("""
                WITH dup AS (
                    SELECT copy."Id" AS copy_id, src."Id" AS src_id
                    FROM games."Characters" copy
                    JOIN games."Characters" src
                      ON src."Id"::text = COALESCE(copy."Character" ->> 'Id', copy."Character" ->> 'id')
                     AND src."Id" <> copy."Id"
                    WHERE copy."Kind" = 'Npc'
                      AND copy."ScenarioId" IS NOT NULL
                      AND src."Kind" = 'Npc'
                      AND src."ScenarioId" IS NULL
                      AND src."Character" = copy."Character"
                )
                UPDATE games."ScenarioNpcs" sn
                SET "CharacterId" = dup.src_id
                FROM dup
                WHERE sn."CharacterId" = dup.copy_id
                  AND NOT EXISTS (
                      SELECT 1 FROM games."ScenarioNpcs" other
                      WHERE other."ScenarioId" = sn."ScenarioId" AND other."CharacterId" = dup.src_id);
                """);

            migrationBuilder.Sql("""
                DELETE FROM games."Characters" copy
                USING games."Characters" src
                WHERE copy."Kind" = 'Npc'
                  AND copy."ScenarioId" IS NOT NULL
                  AND src."Id"::text = COALESCE(copy."Character" ->> 'Id', copy."Character" ->> 'id')
                  AND src."Id" <> copy."Id"
                  AND src."Kind" = 'Npc'
                  AND src."ScenarioId" IS NULL
                  AND src."Character" = copy."Character";
                """);

            // Участие НПС в сценарии теперь описывает только связь.
            migrationBuilder.Sql("""
                UPDATE games."Characters" SET "ScenarioId" = NULL WHERE "Kind" = 'Npc';
                """);

            // Статус больше не кодирует принадлежность: «шаблон» — это вид, а не статус.
            migrationBuilder.Sql("""
                UPDATE games."Characters" SET "Status" = 'Active' WHERE "Status" = 'Template';
                """);

            migrationBuilder.DropColumn(
                name: "NpcRole",
                schema: "games",
                table: "Characters");

            migrationBuilder.CreateIndex(
                name: "IX_Characters_CampaignId",
                schema: "games",
                table: "Characters",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_Characters_Kind",
                schema: "games",
                table: "Characters",
                column: "Kind");

            migrationBuilder.CreateIndex(
                name: "IX_ScenarioNpcs_CharacterId",
                schema: "games",
                table: "ScenarioNpcs",
                column: "CharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_ScenarioNpcs_ScenarioId_CharacterId",
                schema: "games",
                table: "ScenarioNpcs",
                columns: new[] { "ScenarioId", "CharacterId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Characters_Campaigns_CampaignId",
                schema: "games",
                table: "Characters",
                column: "CampaignId",
                principalSchema: "games",
                principalTable: "Campaigns",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NpcRole",
                schema: "games",
                table: "Characters",
                type: "text",
                nullable: false,
                defaultValue: "Neutral");

            // Возвращаем привязку к сценарию в лист: если НПС занят в нескольких
            // сценариях, остаётся первый — прежняя модель большего не умела.
            migrationBuilder.Sql("""
                UPDATE games."Characters" c
                SET "ScenarioId" = first_cast."ScenarioId",
                    "NpcRole" = first_cast."Role"
                FROM (
                    SELECT DISTINCT ON ("CharacterId") "CharacterId", "ScenarioId", "Role"
                    FROM games."ScenarioNpcs"
                    ORDER BY "CharacterId", "CreatedAt"
                ) AS first_cast
                WHERE c."Id" = first_cast."CharacterId";
                """);

            migrationBuilder.Sql("""
                UPDATE games."Characters" SET "Status" = 'Template'
                WHERE "Kind" <> 'PlayerCharacter' AND "ScenarioId" IS NULL AND "CampaignPlayerId" IS NULL;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_Characters_Campaigns_CampaignId",
                schema: "games",
                table: "Characters");

            migrationBuilder.DropTable(
                name: "ScenarioNpcs",
                schema: "games");

            migrationBuilder.DropIndex(
                name: "IX_Characters_CampaignId",
                schema: "games",
                table: "Characters");

            migrationBuilder.DropIndex(
                name: "IX_Characters_Kind",
                schema: "games",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "CampaignId",
                schema: "games",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "Kind",
                schema: "games",
                table: "Characters");
        }
    }
}
