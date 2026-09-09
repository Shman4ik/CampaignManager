using System.Collections.Generic;
using CampaignManager.Web.Components.Features.Bestiary.Model;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampaignManager.Web.Migrations.AppDb
{
    /// <summary>
    ///     Остальные строки статблока из книги, под которые в модели не было полей:
    ///     навыки, скорость по способам передвижения, число атак за раунд, броня словами,
    ///     вид каждой атаки и то, как она обращается с бонусом к урону (гл. 14, стр. 277–280).
    ///     <para>
    ///         Скорость и число атак книга печатает у каждой твари, но при заливке бестиария
    ///         они не сохранились: составные скорости «8 / плавание 10» свелись к одному числу,
    ///         а «Атак за раунд» уцелело лишь у 18 существ из 56. Эти два набора взяты из книги
    ///         поимённо; всё остальное разбирается из уже имеющегося текста CombatDescriptions.
    ///     </para>
    ///     <para>Шаги идемпотентны: заполняется только пустое.</para>
    /// </summary>
    /// <inheritdoc />
    public partial class BestiaryStatblockModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<CreatureSkill>>(
                name: "Skills",
                schema: "games",
                table: "Creatures",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            // ── Переименование: это бонус к урону, а не к попаданию ───────────────
            migrationBuilder.Sql("""
                UPDATE games."Creatures"
                SET "CreatureCharacteristics" =
                        ("CreatureCharacteristics" - 'AverageBonusToHit')
                        || jsonb_build_object('AverageDamageBonus',
                                              "CreatureCharacteristics"->'AverageBonusToHit')
                WHERE "CreatureCharacteristics" ? 'AverageBonusToHit';
                """);

            // ── Навыки ────────────────────────────────────────────────────────────
            // «Скрытность 60% (в воде 80%), Слух 50%.» → две записи. Разделитель —
            // запятая перед заглавной буквой: скобки внутри названия («Внимание
            // (эхолокация) 75%») запятых не содержат, поэтому не разрываются.
            migrationBuilder.Sql("""
                UPDATE games."Creatures" c
                SET "Skills" = s.parsed
                FROM (
                    SELECT c2."Id", jsonb_agg(jsonb_build_object(
                               'Name',  trim(substring(item from '^(.*?)\s*[0-9]+\s*%')),
                               'Value', (substring(item from '([0-9]+)\s*%'))::int,
                               'Note',  nullif(trim(both ' .()' from
                                            regexp_replace(item, '^.*?[0-9]+\s*%\s*', '')), '')
                           ) ORDER BY item) AS parsed
                    FROM games."Creatures" c2,
                    LATERAL regexp_split_to_table(c2."CombatDescriptions"->>'Навыки',
                                                  ',\s*(?=[А-ЯЁ])') AS item
                    WHERE item ~ '[0-9]+\s*%'
                    GROUP BY c2."Id"
                ) s
                WHERE c."Id" = s."Id" AND jsonb_array_length(c."Skills") = 0;
                """);

            // ── Броня словами ─────────────────────────────────────────────────────
            // Из строки убирается только то, что уже разобрано в число: ведущее
            // «9 пунктов (кожа).» или «нет.». Всё, что осталось, и есть механика,
            // которую числом не выразить: «регенерирует 2 ПЗ за раунд», «физическое
            // оружие наносит только по 1 пункту урона за попадание» (стр. 306).
            migrationBuilder.Sql("""
                UPDATE games."Creatures"
                SET "CreatureCharacteristics" = jsonb_set(
                        "CreatureCharacteristics", '{ArmorNote}', to_jsonb(m.note))
                FROM (
                    SELECT "Id", nullif(trim(regexp_replace(
                        "CombatDescriptions"->>'Броня',
                        '^\s*(?:[0-9]+(?=\s*(?:пункт|\(|[.,]|$))\s*(?:пункт[а-я]*)?|нет)\s*(?:\([^)]*\))?\s*[.,]?\s*',
                        '')), '') AS note
                    FROM games."Creatures"
                    WHERE coalesce("CreatureCharacteristics"->>'ArmorNote', '') = ''
                ) AS m
                WHERE games."Creatures"."Id" = m."Id" AND m.note IS NOT NULL;
                """);

            // ── Скорость по способам передвижения ─────────────────────────────────
            // Книга печатает «8 / плавание 10», «6 / полёт 20», «перекатывание 10»
            // (стр. 280). При заливке осталось одно число, поэтому вторая скорость
            // берётся из книги поимённо. Без неё погоня считала полёт как половину
            // наземной скорости (стр. 141): бьякхи вместо 16 получал 2.
            migrationBuilder.Sql("""
                UPDATE games."Creatures" c
                SET "CreatureCharacteristics" = c."CreatureCharacteristics"
                        || jsonb_strip_nulls(jsonb_build_object(
                               'SwimSpeed', m.swim, 'FlySpeed', m.fly, 'SpeedNote', m.note))
                FROM (VALUES
                    ('Акула', 12, NULL, 'плавание 12'),
                    ('Бьякхи', NULL, 16, NULL),
                    ('Вампир', NULL, NULL, 'как у его вида +2 (у вампира-человека СКО 10)'),
                    ('Глубоководный', 10, NULL, NULL),
                    ('Гончая Тиндала', NULL, 20, NULL),
                    ('Гриб с Юггота', NULL, 13, NULL),
                    ('Дагон и Гидра', NULL, 15, NULL),
                    ('Дхоул', NULL, NULL, 'ползание 15 / рытьё 10'),
                    ('Звёздное отродье Ктулху', 15, NULL, NULL),
                    ('Звёздный вампир', NULL, 9, NULL),
                    ('Зомби', NULL, NULL, '6 (медленный зомби) / 8 (быстрый зомби)'),
                    ('Кальмар, гигантский', 10, NULL, NULL),
                    ('Крокодил', 8, NULL, NULL),
                    ('Летучая мышь, крупная', NULL, 12, NULL),
                    ('Летучий полип', NULL, 12, NULL),
                    ('Ллойгор', NULL, NULL, '7 / 3 сквозь камень, когда нематериален'),
                    ('Насекомое с Шатгая', NULL, 20, NULL),
                    ('Огненный вампир', NULL, 11, 'полёт 11'),
                    ('Охотящийся ужас', NULL, 11, NULL),
                    ('Потомок Глубоководных', 8, NULL, NULL),
                    ('Птица', NULL, 12, NULL),
                    ('Старец', NULL, 10, NULL),
                    ('Хтониец', NULL, NULL, '6 / рытьё 1'),
                    ('Цвет из иных миров', NULL, 20, 'перетекание 12 / полёт 20'),
                    ('Шантак', NULL, 18, NULL),
                    ('Шоггот', NULL, NULL, 'перекатывание 10'),
                    ('Шоггот-владыка', NULL, NULL, 'ходьба 8 / перекатывание 10')
                ) AS m(name, swim, fly, note)
                WHERE c."Name" = m.name
                  AND c."CreatureCharacteristics"->'SwimSpeed' IS NULL
                  AND c."CreatureCharacteristics"->'FlySpeed' IS NULL
                  AND coalesce(c."CreatureCharacteristics"->>'SpeedNote', '') = '';
                """);

            // ── Атак за раунд ─────────────────────────────────────────────────────
            // Строка статблока, одна на всё существо (стр. 279). Столько же раз тварь
            // может уклониться или контратаковать, прежде чем противники получат
            // бонусную кость за численное превосходство. Там, где книга задаёт число
            // броском («2d6», «1d8»), в поле остаётся 1, а формула уходит в оговорку.
            migrationBuilder.Sql("""
                UPDATE games."Creatures" c
                SET "CreatureCharacteristics" = c."CreatureCharacteristics"
                        || jsonb_strip_nulls(jsonb_build_object(
                               'AttacksPerRound', m.count, 'AttacksPerRoundNote', m.note))
                FROM (VALUES
                    ('Акула', 2, 'может схватить и удерживать только одну жертву'),
                    ('Бесформенное отродье', 2, 'максимум 1 укус за раунд'),
                    ('Бродящий меж миров', 2, NULL),
                    ('Бьякхи', 2, NULL),
                    ('Великая Раса с планеты Йит', 2, 'только один выстрел из молниемёта за раунд'),
                    ('Волк', 1, NULL),
                    ('Глубоководный', 1, NULL),
                    ('Гноф-кех', 5, 'максимум 1 бодание за раунд'),
                    ('Гончая Тиндала', 1, NULL),
                    ('Гриб с Юггота', 2, NULL),
                    ('Дагон и Гидра', 2, NULL),
                    ('Дхоул', 1, NULL),
                    ('Звёздное отродье Ктулху', 4, NULL),
                    ('Звёздный вампир', 3, NULL),
                    ('Змеиный народ', 1, NULL),
                    ('Змея, Удав', 1, NULL),
                    ('Змея, Ядовитая', 1, 'может контратаковать укусом несколько раз за раунд'),
                    ('Кальмар, гигантский', 4, NULL),
                    ('Крокодил', 1, NULL),
                    ('Крысиная тварь', 1, NULL),
                    ('Лев', 2, NULL),
                    ('Летучая мышь, крупная', 1, NULL),
                    ('Летучий полип', 1, '2d6 — число щупалец бросается каждый раунд'),
                    ('Лошадь', 1, NULL),
                    ('Медведь', 2, NULL),
                    ('Мумия', 2, NULL),
                    ('Насекомое с Шатгая', 1, NULL),
                    ('Обитатель песков', 2, NULL),
                    ('Оборотень', 2, NULL),
                    ('Огненный вампир', 1, NULL),
                    ('Охотящийся ужас', 2, NULL),
                    ('Ползучая тварь', 1, NULL),
                    ('Потомок Глубоководных', 1, NULL),
                    ('Птица', 1, NULL),
                    ('Собака', 1, NULL),
                    ('Старец', 5, NULL),
                    ('Тёмная молодь', 5, 'затаптывание не чаще раза за раунд'),
                    ('Упырь', 3, NULL),
                    ('Хтониец', 1, '1d8; раздавливание не чаще раза за раунд'),
                    ('Цвет из иных миров', 1, NULL),
                    ('Чо-чо', 1, NULL),
                    ('Шантак', 1, NULL),
                    ('Шоггот', 2, NULL)
                ) AS m(name, count, note)
                WHERE c."Name" = m.name
                  AND coalesce((c."CreatureCharacteristics"->>'AttacksPerRound')::int, 0) = 0;
                """);

            // Остальным — одна атака за раунд: это и самый частый случай в книге
            // (26 записей из 59), и прежнее поведение боёвки.
            migrationBuilder.Sql("""
                UPDATE games."Creatures"
                SET "CreatureCharacteristics" =
                        jsonb_set("CreatureCharacteristics", '{AttacksPerRound}', to_jsonb(1))
                WHERE coalesce(("CreatureCharacteristics"->>'AttacksPerRound')::int, 0) = 0;
                """);

            // ── Вид атаки и обращение с бонусом к урону ───────────────────────────
            // Манёвром считается только то, что книга сама пометила «(манёвр)»;
            // особой — атака, для которой книга не дала костей урона (поглощение,
            // взгляд, слияние): её исход решает встречная проверка, а не бросок.
            // Режим бонуса читается из той же строки урона: «равен БкУ», «½ БкУ», «+ БкУ».
            migrationBuilder.Sql("""
                WITH exploded AS (
                    SELECT c."Id", a.idx, a.value,
                           coalesce(c."CombatDescriptions"->>((a.value->>'Name') || ' стат'),
                                    c."CombatDescriptions"->>(a.value->>'Name'), '') AS txt
                    FROM games."Creatures" c,
                    LATERAL jsonb_array_elements(c."Attacks") WITH ORDINALITY a(value, idx)
                ), classified AS (
                    SELECT "Id", idx, value,
                        CASE
                            WHEN value->>'Name' ~* 'манёвр' THEN 'Maneuver'
                            WHEN value->>'Name' IN ('Молниемёт','Плевок слизью','Порыв ветра',
                                          'Психическая атака','Нейрокнут','Замедляющая атака',
                                          'Атака вихрем') THEN 'Ranged'
                            WHEN value->>'DamageFormula' = '1D3'
                                 AND value->>'Name' NOT LIKE 'Ближний бой%' THEN 'Special'
                            ELSE 'Melee'
                        END AS kind,
                        CASE
                            WHEN txt ~ 'равен БкУ' THEN 'OnlyBonus'
                            WHEN txt ~ '(1/2|½)\s*БкУ' THEN 'Half'
                            WHEN txt ~ 'БкУ' THEN 'Full'
                            ELSE 'None'
                        END AS bonus
                    FROM exploded
                ), rebuilt AS (
                    SELECT "Id", jsonb_agg(
                               value || jsonb_build_object('Kind', kind, 'DamageBonus', bonus)
                               ORDER BY idx) AS attacks
                    FROM classified
                    GROUP BY "Id"
                )
                UPDATE games."Creatures" c
                SET "Attacks" = r.attacks
                FROM rebuilt r
                WHERE c."Id" = r."Id"
                  AND NOT EXISTS (
                      SELECT 1 FROM jsonb_array_elements(c."Attacks") a WHERE a.value ? 'Kind');
                """);

            // IsMelee вычисляется из Kind, а число атак переехало на само существо —
            // оба ключа в записи атаки больше не нужны.
            migrationBuilder.Sql("""
                UPDATE games."Creatures"
                SET "Attacks" = (
                    SELECT coalesce(jsonb_agg((a.value - 'IsMelee' - 'AttacksPerRound')
                                              ORDER BY a.idx), '[]'::jsonb)
                    FROM jsonb_array_elements("Attacks") WITH ORDINALITY a(value, idx))
                WHERE EXISTS (
                    SELECT 1 FROM jsonb_array_elements("Attacks") a
                    WHERE a.value ? 'IsMelee' OR a.value ? 'AttacksPerRound');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE games."Creatures"
                SET "CreatureCharacteristics" =
                        ("CreatureCharacteristics" - 'AverageDamageBonus')
                        || jsonb_build_object('AverageBonusToHit',
                                              "CreatureCharacteristics"->'AverageDamageBonus')
                WHERE "CreatureCharacteristics" ? 'AverageDamageBonus';
                """);

            migrationBuilder.DropColumn(
                name: "Skills",
                schema: "games",
                table: "Creatures");
        }
    }
}
