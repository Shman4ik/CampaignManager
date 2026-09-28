using System.Collections.Generic;
using CampaignManager.Web.Components.Features.Bestiary.Model;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampaignManager.Web.Migrations.AppDb
{
    /// <summary>
    ///     Одна картинка существа (<c>ImageUrl</c>) превращается в список <c>Images</c>.
    ///     Прежняя картинка переезжает первым элементом, поэтому остаётся обложкой карточки.
    ///     <para>
    ///         Колонку <c>ImageUrl</c> миграция намеренно <b>не удаляет</b>: модель её больше не знает,
    ///         а старая сборка, которая ещё читает её, продолжит работать: локальный запуск и прод
    ///         ходят в одну базу, так что миграция с локальной машины оказывается на проде раньше
    ///         деплоя. Снести колонку — отдельной миграцией с <c>DropColumn</c>, когда старых сборок
    ///         не останется.
    ///     </para>
    /// </summary>
    public partial class CreatureImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<CreatureImage>>(
                name: "Images",
                schema: "games",
                table: "Creatures",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            // Ключи в том же регистре, в каком их пишет сериализатор EF для jsonb («Url», «Caption»).
            migrationBuilder.Sql("""
                UPDATE games."Creatures"
                SET "Images" = jsonb_build_array(jsonb_build_object('Url', "ImageUrl", 'Caption', NULL))
                WHERE "ImageUrl" IS NOT NULL AND btrim("ImageUrl") <> '';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Обложку возвращаем в ImageUrl: картинку могли сменить уже после переноса.
            // Второй и дальше картинкам в старой схеме места нет.
            migrationBuilder.Sql("""
                UPDATE games."Creatures"
                SET "ImageUrl" = "Images" -> 0 ->> 'Url'
                WHERE jsonb_array_length("Images") > 0;
                """);

            migrationBuilder.DropColumn(
                name: "Images",
                schema: "games",
                table: "Creatures");
        }
    }
}
