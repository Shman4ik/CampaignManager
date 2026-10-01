using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampaignManager.Web.Migrations.AppDb
{
    /// <summary>
    ///     CHECK <c>CK_Characters_Owner</c>: владелец листа обязан подходить к его виду. Лист игрока без
    ///     места игрока правило доступа принимает за общую библиотеку — такие строки годами появлялись
    ///     молча, и база их больше не примет.
    ///     <para>
    ///         <b>Сначала данные, потом миграция.</b> PostgreSQL проверяет ограничение на всех
    ///         существующих строках, и пока в <c>games."Characters"</c> остаются нарушители, миграция
    ///         падает с <c>23514: check constraint "CK_Characters_Owner" … is violated by some row</c>
    ///         (ничего при этом не меняя). Живую базу перед применением (2026-10-01) выправил разовый
    ///         <c>docs/fix-character-owners.sql</c> — он остался в истории PR #91 и удалён.
    ///     </para>
    ///     <para>
    ///         Старой сборке на проде ограничение не мешает: всё, что она сохраняет штатно, ему
    ///         удовлетворяет, а ничейный лист сыщика вместо тихой записи получит ошибку сохранения.
    ///     </para>
    /// </summary>
    public partial class AddCharacterOwnerCheck : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_Characters_Owner",
                schema: "games",
                table: "Characters",
                sql: "(\"Kind\" = 'PlayerCharacter' AND \"CampaignPlayerId\" IS NOT NULL AND \"CampaignId\" IS NULL AND \"ScenarioId\" IS NULL) OR (\"Kind\" = 'Pregen' AND \"CampaignId\" IS NULL) OR (\"Kind\" = 'Npc' AND \"CampaignPlayerId\" IS NULL AND \"ScenarioId\" IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Characters_Owner",
                schema: "games",
                table: "Characters");
        }
    }
}
