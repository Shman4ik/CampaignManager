-- Перенос существующей базы на схлопнутую историю миграций AppDb.
--
-- Контекст: 46 миграций AppDb заменены одной 20260910132807_InitialCreate.
-- Схема при этом не изменилась — снапшот модели после схлопывания побайтово
-- совпал с прежним, — поэтому саму базу трогать не нужно. Переписать надо
-- только журнал миграций, иначе следующий `dotnet ef database update` решит,
-- что схемы нет, и попытается создать её заново.
--
-- Выполнять ОДИН РАЗ на каждой базе, которая уже накатана до
-- 20260909221424_BestiaryStatblockModel включительно. Порядок:
--   1. Снять бэкап.
--   2. Выполнить этот скрипт.
--   3. Проверить, что EF считает базу актуальной:
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

DELETE FROM public."__EFMigrationsHistory"
WHERE "MigrationId" <> '20260910132807_InitialCreate';

INSERT INTO public."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260910132807_InitialCreate', '10.0.12')
ON CONFLICT ("MigrationId") DO NOTHING;

COMMIT;

-- Ожидаемый результат — ровно одна строка:
-- SELECT * FROM public."__EFMigrationsHistory";
