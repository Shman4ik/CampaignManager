-- Выгрузка базы знаний LLM перед удалением таблицы.
--
-- Миграция 20260910141633_DropLlmKnowledgeEntries удаляет games."LlmKnowledgeEntries"
-- насовсем: Down() восстанавливает только структуру, но не содержимое. В таблице лежит
-- около 31 КБ авторского справочного текста на русском — правила создания персонажа
-- и справочник навыков CoC 7e, — который к самой LLM-валидации отношения не имеет
-- и может пригодиться отдельно.
--
-- ВЫПОЛНИТЬ ДО `dotnet ef database update`, иначе содержимое пропадёт.
--
-- Вариант 1 — сохранить каждую запись отдельным .md-файлом (psql, клиентская сторона):
--
--   psql "$CONNECTION_STRING" -At \
--     -c "select \"Content\" from games.\"LlmKnowledgeEntries\" where \"Key\"='character-creation'" \
--     > coc7e-character-creation.md
--
--   psql "$CONNECTION_STRING" -At \
--     -c "select \"Content\" from games.\"LlmKnowledgeEntries\" where \"Key\"='skills-reference'" \
--     > coc7e-skills-reference.md
--
-- Вариант 2 — одним JSON-файлом со всеми полями:
--
--   psql "$CONNECTION_STRING" -At -f docs/export-llm-knowledge.sql > llm-knowledge-backup.json

select jsonb_pretty(jsonb_agg(to_jsonb(e) order by e."SortOrder"))
from games."LlmKnowledgeEntries" e;
