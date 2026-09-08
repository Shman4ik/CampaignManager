using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampaignManager.Web.Migrations.AppDb
{
    /// <summary>
    ///     Единое написание текстовых полей каталога — по таблице XVII (стр. 399–402).
    ///     <para>
    ///         Разбор формул от этих правок не меняется (<c>DamageFormulaParser</c> и так
    ///         нечувствителен к регистру и пробелам), но одна и та же величина писалась
    ///         по-разному — «БкУ» и «Бку», «СИЛ / 5 метров» и «СИЛ/5м», — и это лишние
    ///         варианты для парсеров дальности и атак.
    ///     </para>
    ///     <para>
    ///         У ракетницы и гранатомёта в книге в колонке «Базов. дальн.» стоят голые «10»
    ///         и «20» без единиц — единственные две ячейки во всей таблице; дописываем «метров».
    ///     </para>
    /// </summary>
    /// <inheritdoc />
    public partial class WeaponCatalogTextNormalization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Бонус к урону: книга пишет «БкУ»
            migrationBuilder.Sql("""
                UPDATE games."Weapons"
                SET "Damage" = replace(replace(replace("Damage", 'БКУ', 'БкУ'), 'Бку', 'БкУ'), 'бку', 'БкУ'),
                    "LastUpdated" = now()
                WHERE "Damage" ~ 'БКУ|Бку|бку';
                """);

            // Пробелы вокруг «+»: «1d4+1/2 БкУ» и «1d6+ 1» → «1d4 + 1/2 БкУ», «1d6 + 1»
            migrationBuilder.Sql("""
                UPDATE games."Weapons"
                SET "Damage" = regexp_replace("Damage", '\s*\+\s*', ' + ', 'g'), "LastUpdated" = now()
                WHERE "Damage" ~ '\+' AND "Damage" <> regexp_replace("Damage", '\s*\+\s*', ' + ', 'g');
                """);

            // Дальность метательного оружия и две ячейки без единиц измерения
            migrationBuilder.Sql("""
                UPDATE games."Weapons"
                SET "Range" = 'СИЛ / 5 метров', "LastUpdated" = now()
                WHERE lower("Range") IN ('сил/5м', 'сил / 5м', 'сил/5 м');

                UPDATE games."Weapons"
                SET "Range" = "Range" || ' метров', "LastUpdated" = now()
                WHERE "Range" ~ '^\d+$';
                """);

            // У дробовиков разобранный урон лежит в RangeDamages, и у каждой дальности
            // свой RawText — его тоже надо привести к новому написанию.
            migrationBuilder.Sql("""
                UPDATE games."Weapons" w
                SET "DamageInfo" = jsonb_set(w."DamageInfo", '{RangeDamages}', (
                        SELECT jsonb_agg(
                                   jsonb_set(e, '{Damage,RawText}',
                                       to_jsonb(regexp_replace(e -> 'Damage' ->> 'RawText',
                                                               '\s*\+\s*', ' + ', 'g')))
                                   ORDER BY ord)
                        FROM jsonb_array_elements(w."DamageInfo" -> 'RangeDamages')
                             WITH ORDINALITY AS t(e, ord)))
                WHERE jsonb_typeof(w."DamageInfo" -> 'RangeDamages') = 'array';
                """);

            // RawText внутри DamageInfo — зеркало поля Damage
            migrationBuilder.Sql("""
                UPDATE games."Weapons"
                SET "DamageInfo" = jsonb_set(
                        jsonb_set("DamageInfo", '{RawText}', to_jsonb("Damage")),
                        '{Primary,RawText}', to_jsonb("Damage"))
                WHERE "DamageInfo" IS NOT NULL
                  AND "DamageInfo" -> 'Primary' IS NOT NULL
                  AND jsonb_typeof("DamageInfo" -> 'Primary') = 'object'
                  AND "DamageInfo" ->> 'RawText' IS DISTINCT FROM "Damage";

                UPDATE games."Weapons"
                SET "DamageInfo" = jsonb_set("DamageInfo", '{RawText}', to_jsonb("Damage"))
                WHERE "DamageInfo" IS NOT NULL
                  AND "DamageInfo" ->> 'RawText' IS DISTINCT FROM "Damage";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Правки только данных — откатывать разнобой в написании незачем.
        }
    }
}
