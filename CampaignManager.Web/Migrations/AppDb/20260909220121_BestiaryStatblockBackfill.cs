using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampaignManager.Web.Migrations.AppDb
{
    /// <summary>
    ///     Наполнение пустых полей статблока существ из свободного текста
    ///     <c>CombatDescriptions</c> и починка двух испорченных наборов данных.
    ///     <para>
    ///         Книга печатает у каждой твари «Броня», «Уклонение», «Потеря рассудка»,
    ///         «Атак за раунд» и «Навыки» (гл. 14, стр. 277–280). При заливке бестиария
    ///         эти строки попали в словарь <c>CombatDescriptions</c>, а типизированные
    ///         поля <c>Armor</c>, <c>DodgeSkill</c> и <c>SanityLoss</c> остались нулевыми —
    ///         боёвка и лист персонажа читают именно их, а не словарь.
    ///     </para>
    ///     <para>
    ///         Все шаги идемпотентны: числовые поля заполняются только там, где стоит 0
    ///         или пусто, поэтому правка Хранителя переживёт повторный запуск.
    ///         Разбор строк повторяет <c>Bestiary/Services/CreatureStatblockParser</c>.
    ///     </para>
    /// </summary>
    /// <inheritdoc />
    public partial class BestiaryStatblockBackfill : Migration
    {
        /// <summary>
        ///     Ключи <c>CombatDescriptions</c>, которые описывают не атаку, а другую строку
        ///     статблока. В список атак они попали по ошибке (см. шаг 4).
        /// </summary>
        private const string ServiceKeys =
            "'Броня','Уклонение','Навыки','Обитает','Потеря рассудка','Атак за раунд'";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Шаг 1. Потеря рассудка ────────────────────────────────────────────
            // «1/1d10 пунктов при виде бесформенного отродья.» → «1/1d10».
            // Книга пишет кости и латиницей, и кириллицей («1д6/1д20»), поэтому в классе
            // символов оба варианта. У записей с двумя формулами («0/1d4 пункта при виде
            // цвета, 1/1d8 при виде жертвы») берётся первая — она про саму тварь.
            // Записи без дроби («нет, если служитель Гла'аки похож на человека») не
            // разбираются и остаются пустыми: предел привыкания впишет Хранитель (стр. 167).
            migrationBuilder.Sql("""
                UPDATE games."Creatures"
                SET "CreatureCharacteristics" = jsonb_set(
                        "CreatureCharacteristics", '{SanityLoss}', to_jsonb(m.parsed))
                FROM (
                    -- Скобки в шаблоне задают то, что вернёт substring, поэтому вся запись
                    -- «успех/провал» лежит в одной внешней группе, а разбор её половин —
                    -- в некаптурящих. Иначе вернулась бы только успешная половина.
                    SELECT "Id", substring(
                        "CombatDescriptions"->>'Потеря рассудка'
                        from '^\s*((?:[0-9]+|[0-9]*[dдDД][0-9]+)\s*/\s*[0-9]*[dдDД][0-9]+(?:\s*\+\s*[0-9]+)?)'
                    ) AS parsed
                    FROM games."Creatures"
                    WHERE coalesce("CreatureCharacteristics"->>'SanityLoss', '') = ''
                ) AS m
                WHERE games."Creatures"."Id" = m."Id" AND m.parsed IS NOT NULL;
                """);

            // ── Шаг 2. Броня ──────────────────────────────────────────────────────
            // Разбирается только число в начале строки: «9 пунктов (кожа).» → 9,
            // «нет.» → 0 (и так уже 0, поэтому запись не меняется). Формулы («1/5 МОЩ
            // дхоула») и описания механик вместо числа («Отродья неуязвимы для любого
            // физического оружия») не разбираются — под них в модели поля пока нет,
            // текст остаётся в CombatDescriptions.
            migrationBuilder.Sql("""
                UPDATE games."Creatures"
                SET "CreatureCharacteristics" = jsonb_set(
                        "CreatureCharacteristics", '{Armor}', to_jsonb(m.parsed))
                FROM (
                    SELECT "Id", nullif(substring(
                        "CombatDescriptions"->>'Броня'
                        from '^\s*([0-9]+)\s*(?:пункт|\(|\.|$)'
                    ), '')::int AS parsed
                    FROM games."Creatures"
                    WHERE coalesce(("CreatureCharacteristics"->>'Armor')::int, 0) = 0
                ) AS m
                WHERE games."Creatures"."Id" = m."Id" AND m.parsed IS NOT NULL;
                """);

            // ── Шаг 3. Уклонение ──────────────────────────────────────────────────
            // «47% (23/9)» → 47. Половинное и пятое значения в скобках не хранятся:
            // их считает интерфейс. Записи вроде «зомби недоступно: у него нет
            // собственной воли» не разбираются, и Combatant.DodgeSkill останется на
            // запасном варианте ½ ЛВК (стр. 73).
            migrationBuilder.Sql("""
                UPDATE games."Creatures"
                SET "CreatureCharacteristics" = jsonb_set(
                        "CreatureCharacteristics", '{DodgeSkill}', to_jsonb(m.parsed))
                FROM (
                    SELECT "Id", nullif(substring(
                        "CombatDescriptions"->>'Уклонение' from '^\s*([0-9]+)\s*%'
                    ), '')::int AS parsed
                    FROM games."Creatures"
                    WHERE coalesce(("CreatureCharacteristics"->>'DodgeSkill')::int, 0) = 0
                ) AS m
                WHERE games."Creatures"."Id" = m."Id" AND m.parsed IS NOT NULL;
                """);

            // ── Шаг 4. Пересборка списка атак ─────────────────────────────────────
            // В колонку Attacks механически перенесли все ключи CombatDescriptions,
            // из-за чего «Броня», «Уклонение» и «Обитает» стали атаками с уроном 1D3
            // и навыком 50% — Хранитель видел их в выпадающем списке атак в бою.
            // Собираем список заново из реальных атак, забирая числа из парной строки
            // «<название> стат» («Ближний бой стат» = «60% (30/12), урон 2d6 + БкУ»),
            // а при её отсутствии — из хвоста самого описания.
            // Урон «равен БкУ» (стр. 278, шоггот и тёмная молодь) записывается как «0»:
            // бонус к урону боёвка добавляет отдельно, поэтому 0 + БкУ даёт верный итог.
            migrationBuilder.Sql($$"""
                WITH base AS (
                    SELECT c."Id",
                           regexp_replace(n.name, '\s*\([0-9]+/[0-9]+/[0-9]+\)$', '') AS atk,
                           substring(n.name from '\(([0-9]+)/[0-9]+/[0-9]+\)$') AS name_skill,
                           c."CombatDescriptions"->>n.name AS descr,
                           coalesce(c."CombatDescriptions"->>(n.name || ' стат'),
                                    c."CombatDescriptions"->>n.name, '') AS stat
                    FROM games."Creatures" c,
                    LATERAL (
                        SELECT DISTINCT regexp_replace(k, ' стат$', '') AS name
                        FROM jsonb_object_keys(c."CombatDescriptions") k
                        WHERE regexp_replace(k, ' стат$', '') NOT IN ({{ServiceKeys}})
                    ) n
                ), melee AS (
                    SELECT "Id", max(nullif(substring(stat from '([0-9]+)\s*%'), '')::int) AS skill
                    FROM base WHERE atk LIKE 'Ближний бой%' GROUP BY "Id"
                ), rebuilt AS (
                    SELECT b."Id", jsonb_agg(jsonb_build_object(
                        'Name', b.atk,
                        'SkillValue', coalesce(
                            nullif(substring(b.stat from '([0-9]+)\s*%'), '')::int,
                            b.name_skill::int, m.skill, 50),
                        'DamageFormula', coalesce(
                            nullif(substring(b.stat from '[Уу]рон[^0-9]{0,14}([0-9]+\s*[dдDД]\s*[0-9]+)'), ''),
                            CASE WHEN b.stat ~ 'равен БкУ' THEN '0' ELSE '1D3' END),
                        'IsMelee', true,
                        'AttacksPerRound', 1,
                        'Description', b.descr
                    ) ORDER BY b.atk) AS attacks
                    FROM base b LEFT JOIN melee m ON m."Id" = b."Id"
                    GROUP BY b."Id"
                )
                UPDATE games."Creatures" c
                SET "Attacks" = coalesce(r.attacks, '[]'::jsonb)
                FROM (SELECT c2."Id", r2.attacks
                      FROM games."Creatures" c2 LEFT JOIN rebuilt r2 ON r2."Id" = c2."Id") r
                WHERE c."Id" = r."Id"
                  AND EXISTS (
                      SELECT 1 FROM jsonb_array_elements(c."Attacks") a
                      WHERE a->>'Name' IN ({{ServiceKeys}}) OR a->>'Name' LIKE '% стат');
                """);

            // ── Шаг 5. Средний бонус к урону ──────────────────────────────────────
            // Книга печатает бонус костями («Средний бонус к урону: +8d6», стр. 306),
            // но при заливке сохранили максимум этих костей числом. CombatService
            // разбирает поле как формулу, поэтому «48» означало ровно 48 пунктов
            // каждым попаданием вместо броска 8d6 — систематическое завышение урона
            // у всех крупных тварей.
            //
            // Каждая строка сверена с книгой поимённо, поэтому запись подменяется
            // только при точном совпадении имени и текущего значения: тварь, которую
            // Хранитель успел поправить руками, не совпадёт и останется как есть.
            // Отрицательные значения («-1», «-2») и «0» книга печатает именно так —
            // их не трогаем. «Бесформенное отродье» выбивается из общего правила:
            // там сохранили число костей, а не их максимум (книга даёт +2d6, стр. 281).
            migrationBuilder.Sql("""
                UPDATE games."Creatures" c
                SET "CreatureCharacteristics" = jsonb_set(
                        c."CreatureCharacteristics", '{AverageBonusToHit}', to_jsonb(m.fixed))
                FROM (VALUES
                    ('Бесформенное отродье',       '2',   '+2d6'),
                    ('Бродящий меж миров',         '6',   '+1d6'),
                    ('Бьякхи',                     '6',   '+1d6'),
                    ('Великая Раса с планеты Йит', '36',  '+6d6'),
                    ('Гаст',                       '12',  '+2d6'),
                    ('Глубоководный',              '4',   '+1d4'),
                    ('Гноф-кех',                   '18',  '+3d6'),
                    ('Гончая Тиндала',             '6',   '+1d6'),
                    ('Дагон и Гидра',              '36',  '+6d6'),
                    ('Дхоул',                      '384', '+64d6'),
                    ('Звёздное отродье Ктулху',    '60',  '+10d6'),
                    ('Звёздный вампир',            '12',  '+2d6'),
                    ('Летучий полип',              '30',  '+5d6'),
                    ('Ллойгор',                    '30',  '+5d6'),
                    ('Обитатель песков',           '4',   '+1d4'),
                    ('Охотящийся ужас',            '18',  '+3d6'),
                    ('Служитель Внешних богов',    '6',   '+1d6'),
                    ('Старец',                     '18',  '+3d6'),
                    ('Тёмная молодь',              '24',  '+4d6'),
                    ('Упырь',                      '4',   '+1d4'),
                    ('Хтониец',                    '30',  '+5d6'),
                    ('Шантак',                     '24',  '+4d6'),
                    ('Шоггот',                     '48',  '+8d6'),
                    ('Шоггот-владыка',             '6',   '+1d6')
                ) AS m(name, broken, fixed)
                WHERE c."Name" = m.name
                  AND c."CreatureCharacteristics"->>'AverageBonusToHit' = m.broken;
                """);

            // Волк и скелет человека несут строку «нет» там, где книга печатает
            // «Средний бонус к урону: нет» — для CombatService это неразбираемая
            // формула, поэтому приводим к нулю.
            migrationBuilder.Sql("""
                UPDATE games."Creatures"
                SET "CreatureCharacteristics" = jsonb_set(
                        "CreatureCharacteristics", '{AverageBonusToHit}', to_jsonb('0'::text))
                WHERE lower(trim("CreatureCharacteristics"->>'AverageBonusToHit')) = 'нет';
                """);

            // ── Шаг 6. Инициатива ─────────────────────────────────────────────────
            // Очерёдность в бою идёт по убыванию ЛВК (стр. 110), и Combatant берёт её
            // из этого поля. У всех существ там 0, поэтому любая тварь ходила после
            // любого сыщика, а с необязательным правилом бросков инициативы (стр. 122)
            // проверка ЛВК против 0 не могла дать ничего лучше провала.
            migrationBuilder.Sql("""
                UPDATE games."Creatures"
                SET "CreatureCharacteristics" = jsonb_set(
                        "CreatureCharacteristics", '{Initiative}',
                        "CreatureCharacteristics"->'Dexterity'->'Value')
                WHERE coalesce(("CreatureCharacteristics"->>'Initiative')::int, 0) = 0
                  AND coalesce(("CreatureCharacteristics"->'Dexterity'->>'Value')::int, 0) > 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Откат восстановил бы заведомо неверные данные (нулевую броню, отсутствующее
            // уклонение, плоский бонус к урону вместо костей), а исходные строки никуда
            // не делись — они остались в CombatDescriptions. Полный откат — из бэкапа.
        }
    }
}
