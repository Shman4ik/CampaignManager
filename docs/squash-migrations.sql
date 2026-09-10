-- Перенос существующей базы на схлопнутую историю миграций AppDb.
--
-- Контекст: 46 миграций AppDb заменены одной 20260910142150_InitialCreate.
-- Схема приложения при этом не менялась — кроме одного намеренного удаления:
-- вместе с выпиленной LLM-валидацией персонажа уходит таблица
-- games."LlmKnowledgeEntries". В InitialCreate её уже нет, поэтому на новой
-- базе она и не появится, а на существующей её надо удалить этим скриптом.
--
-- ┌─────────────────────────────────────────────────────────────────────────┐
-- │ ВНИМАНИЕ: скрипт удаляет games."LlmKnowledgeEntries" вместе с данными.  │
-- │ Там лежит ~31 КБ авторского справочника CoC 7e на русском (правила      │
-- │ создания персонажа и справочник навыков), к самой валидации отношения   │
-- │ не имеющего. Выгрузить его заранее: docs/export-llm-knowledge.sql       │
-- └─────────────────────────────────────────────────────────────────────────┘
--
-- Выполнять ОДИН РАЗ на каждой базе, которая уже накатана до
-- 20260909221424_BestiaryStatblockModel включительно. Порядок:
--   1. Снять бэкап.
--   2. Выгрузить базу знаний: docs/export-llm-knowledge.sql (если она нужна).
--   3. Выполнить этот скрипт.
--   4. Проверить, что EF считает базу актуальной — ОБА контекста, журнал у них общий:
--      dotnet ef database update --project CampaignManager.Web --context AppDbContext
--      dotnet ef database update --project CampaignManager.Web --context AppIdentityDbContext
--      -> оба «No migrations were applied. The database is already up to date.»
--
-- На ПУСТОЙ базе скрипт выполнять НЕ надо: там достаточно обычного
-- database update — он создаст схему из InitialCreate.

BEGIN;

-- Страховка: не трогаем журнал, если база не докачена до последней старой миграции.
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM public."__EFMigrationsHistory"
        WHERE "MigrationId" = '20260909221424_BestiaryStatblockModel'
    ) THEN
        RAISE EXCEPTION
            'База не накатана до 20260909221424_BestiaryStatblockModel — схлопывание применять нельзя';
    END IF;
END $$;

-- Единственное изменение схемы: таблица удалённой LLM-валидации.
DROP TABLE IF EXISTS games."LlmKnowledgeEntries";

-- ВАЖНО: журнал общий для обеих моделей. AppDbContext и AppIdentityDbContext пишут
-- в одну public."__EFMigrationsHistory", поэтому «удалить всё, кроме InitialCreate»
-- нельзя: вместе с историей AppDb уходит и миграция Identity, а следующий
-- database update --context AppIdentityDbContext пытается создать AspNetRoles заново
-- и падает с 42P07. Схемы identity схлопывание не касается, её строка остаётся как есть.
DELETE FROM public."__EFMigrationsHistory"
WHERE "MigrationId" NOT IN (
    '20260910142150_InitialCreate',      -- AppDb, схлопнутая история
    '20250314173627_InitialMigration'    -- AppIdentityDb, не трогаем
);

INSERT INTO public."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260910142150_InitialCreate', '10.0.12')
ON CONFLICT ("MigrationId") DO NOTHING;

-- Страховка на случай, если в проекте появилась третья модель со своими миграциями.
DO $$
DECLARE ids text;
BEGIN
    SELECT string_agg("MigrationId", ', ' ORDER BY "MigrationId")
      INTO ids FROM public."__EFMigrationsHistory";
    IF ids IS DISTINCT FROM '20250314173627_InitialMigration, 20260910142150_InitialCreate' THEN
        RAISE EXCEPTION 'В журнале осталось «%» вместо двух ожидаемых строк', ids;
    END IF;
END $$;

COMMIT;

-- Ожидаемый результат — ровно две строки, по одной на модель:
--   20250314173627_InitialMigration | 9.0.2     (AppIdentityDbContext)
--   20260910142150_InitialCreate    | 10.0.12   (AppDbContext)
-- SELECT * FROM public."__EFMigrationsHistory";
