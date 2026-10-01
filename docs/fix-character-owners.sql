-- Перенос листов, нарушающих CK_Characters_Owner (миграция AddCharacterOwnerCheck).
--
-- ВЫПОЛНЯТЬ ДО `dotnet ef database update`: пока эти строки в базе, миграция падает с 23514.
-- База общая у dev и прода, поэтому запускать только с согласия владельца. Скрипт целиком в одной
-- транзакции: каждый UPDATE обязан задеть ровно одну строку в ожидаемом (плохом) состоянии, а в конце
-- нарушителей не должно остаться — иначе RAISE и откат всего. Повторный запуск после успешного
-- падает на первой же проверке и ничего не трогает. После применения файл удалить, как прежние
-- разовые скрипты (см. корневой CLAUDE.md).
--
-- Подпись игрока внутри листа (`PersonalInfo.PlayerName`) у всех шести листов сыщиков — «Unknown»
-- или пусто, поэтому хозяина восстанавливали по времени создания и журналу git:
--  * до ccf14b5 (2026-03-28 13:30 UTC) `CharacterPage` на `/character/create/{campaignId}` вообще не
--    загружала место игрока — каждый новый лист уходил с CampaignPlayerId = NULL и подписью «Unknown»;
--  * «Доктор Элиас Уэйн» сделан `/character/wizard` без кампании 2026-09-11 06:51 UTC — за два часа
--    до мёржа помощника (#46): смоук-тест на общей базе, после создания лист не открывали ни разу.
--
-- Решения (каждое — отдельный шаг ниже, их можно менять по одному):
--  1. «Элизабет Миллер» → Evgenii Kartoshkin, «Норман и сыновья». Создана 2025-11-17 17:42:18 UTC,
--     через 10 минут после его вступления (17:32:20); больше в тот день никто не вступал, а своего
--     листа в этой кампании у него нет. Остаётся активной.
--  2. «Альберт Андерсон» → Evgenii Kartoshkin, «Ваншот Элексир Жизни», неактивным. Создан
--     2026-03-28 13:05:04 UTC, через 38 секунд после его вступления (13:04:26). Через полчаса
--     вышел ccf14b5, и в 13:33 он завёл «Уильяма Харриса» — тот и остаётся его активным листом.
--  3. «Сивилия» (две строки) и «Элизабет Джонсон» → архивные прегены-заготовки. Сделаны вечером
--     2026-03-27, до создания кампании ваншота (03-28 11:54) — в тот вечер никто ни во что не
--     вступал: это подготовка Хранителя к «Эликсиру жизни». Обе «Сивилии» — копии заготовки
--     019d30f0 (старый баг «сохранение создаёт новую строку»), у второй в JSONB даже её Id: автосохранение
--     такого листа перезаписывало бы саму заготовку, поэтому Id внутри листа выравнивается по строке.
--  4. «Доктор Элиас Уэйн» → архивный преген-заготовка (данные смоук-теста). Если он не нужен совсем —
--     см. закомментированный DELETE в конце; удаление необратимо.
--  5. НПС «Джон Смит» → НПС кампании «Норман и сыновья», без места игрока. Создан 2025-04-22 на
--     месте игрока самого Хранителя («Дмитрий»), вид Npc ему проставила миграция SimplifyNpcModel по
--     CharacterType из JSONB — она обнуляла у НПС ScenarioId, но не CampaignPlayerId. Из-за этого
--     главная показывала его в карточке кампании как сыщика Хранителя.

BEGIN;

DO $$
DECLARE
    n int;
BEGIN
    -- 1. Элизабет Миллер → Evgenii Kartoshkin в «Норман и сыновья»
    UPDATE games."Characters"
    SET "CampaignPlayerId" = '019a92df-eec1-7cb5-8c9b-2febd05f78c5',
        "Character" = jsonb_set("Character", '{PersonalInfo,PlayerName}', to_jsonb('Evgenii Kartoshkin'::text)),
        "LastUpdated" = now()
    WHERE "Id" = '019a92e9-0db1-707d-a414-130c3470977c'
      AND "Kind" = 'PlayerCharacter' AND "CampaignPlayerId" IS NULL;
    GET DIAGNOSTICS n = ROW_COUNT;
    IF n <> 1 THEN RAISE EXCEPTION 'Шаг 1 (Элизабет Миллер): обновлено % строк вместо 1', n; END IF;

    -- 2. Альберт Андерсон → Evgenii Kartoshkin в «Ваншот Элексир Жизни», неактивным
    UPDATE games."Characters"
    SET "CampaignPlayerId" = '019d348b-be94-7094-9580-6307aabfed6d',
        "Status" = 'Inactive',
        "Character" = jsonb_set("Character", '{PersonalInfo,PlayerName}', to_jsonb('Evgenii Kartoshkin'::text)),
        "LastUpdated" = now()
    WHERE "Id" = '019d348c-5222-7696-8feb-4a103562530c'
      AND "Kind" = 'PlayerCharacter' AND "CampaignPlayerId" IS NULL;
    GET DIAGNOSTICS n = ROW_COUNT;
    IF n <> 1 THEN RAISE EXCEPTION 'Шаг 2 (Альберт Андерсон): обновлено % строк вместо 1', n; END IF;

    -- 3–4. Заготовки Хранителя и смоук-тест помощника → архивные прегены. Id внутри листа
    -- выравнивается по строке: у второй «Сивилии» там Id заготовки 019d30f0.
    UPDATE games."Characters"
    SET "Kind" = 'Pregen',
        "Status" = 'Archived',
        "Character" = jsonb_set("Character", '{Id}', to_jsonb("Id"::text)),
        "LastUpdated" = now()
    WHERE "Id" IN ('019d30f1-8bd6-7c51-a732-1e9d5dc6258d',  -- Сивилия (архивная)
                   '019d3132-3458-7a2f-b49b-2189c4e97e97',  -- Сивилия (активная, Id заготовки в JSONB)
                   '019d3116-fdfc-7755-910b-c46029a601d8',  -- Элизабет Джонсон
                   '01a08f3c-6670-7fe5-8626-1b7b8ca734f7')  -- Доктор Элиас Уэйн
      AND "Kind" = 'PlayerCharacter' AND "CampaignPlayerId" IS NULL;
    GET DIAGNOSTICS n = ROW_COUNT;
    IF n <> 4 THEN RAISE EXCEPTION 'Шаги 3–4 (заготовки): обновлено % строк вместо 4', n; END IF;

    -- 5. Джон Смит → НПС кампании «Норман и сыновья»
    UPDATE games."Characters"
    SET "CampaignPlayerId" = NULL,
        "CampaignId" = '01960aa4-68c0-72dc-a8c2-f547c7a83765',
        "LastUpdated" = now()
    WHERE "Id" = '01965f50-53b1-7eb2-9729-257fe64cdeec'
      AND "Kind" = 'Npc' AND "CampaignPlayerId" = '01960ab1-7909-75b1-a89b-7f2d9c265906';
    GET DIAGNOSTICS n = ROW_COUNT;
    IF n <> 1 THEN RAISE EXCEPTION 'Шаг 5 (Джон Смит): обновлено % строк вместо 1', n; END IF;

    -- Итог: ни одной строки, которую не пропустит CK_Characters_Owner (выражение — как в AppDbContext).
    SELECT count(*) INTO n
    FROM games."Characters"
    WHERE NOT (("Kind" = 'PlayerCharacter' AND "CampaignPlayerId" IS NOT NULL AND "CampaignId" IS NULL AND "ScenarioId" IS NULL)
               OR ("Kind" = 'Pregen' AND "CampaignId" IS NULL)
               OR ("Kind" = 'Npc' AND "CampaignPlayerId" IS NULL AND "ScenarioId" IS NULL));
    IF n <> 0 THEN RAISE EXCEPTION 'Остались строки, нарушающие CK_Characters_Owner: %', n; END IF;
END $$;

COMMIT;

-- Если «Доктор Элиас Уэйн» не нужен даже в архиве (необратимо, только по отдельному решению):
-- DELETE FROM games."Characters" WHERE "Id" = '01a08f3c-6670-7fe5-8626-1b7b8ca734f7' AND "Kind" = 'Pregen';
