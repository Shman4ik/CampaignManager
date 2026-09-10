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
--   4. Проверить, что EF считает базу актуальной:
--      dotnet ef database update --project CampaignManager.Web --context AppDbContext
--      -> «No migrations were applied. The database is already up to date.»
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

DELETE FROM public."__EFMigrationsHistory"
WHERE "MigrationId" <> '20260910142150_InitialCreate';

INSERT INTO public."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260910142150_InitialCreate', '10.0.12')
ON CONFLICT ("MigrationId") DO NOTHING;

COMMIT;

-- Ожидаемый результат — ровно одна строка:
-- SELECT * FROM public."__EFMigrationsHistory";
