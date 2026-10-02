---
name: scenario-import
description: "Переносит сценарий «Зова Ктулху» из книги правил или стороннего текста на платформу CampaignManager 2.0 одним JSON-файлом через «Импорт JSON» (или API импорта). USE FOR: занести сценарий на платформу, перенести приключение из книги правил, добавить сценарий в базу, заполнить локации/проверки/ключевые факты/раздатки/тварей/предметы/состав НПС/прегенов, экспортировать сценарий в JSON, переделать сценарий целиком. DO NOT USE FOR: правку одного поля уже заведённого сценария (быстрее руками на рабочем месте), создание кампании или прохождения, генерацию сыщиков игроков. INVOKES: mcp__Claude_Browser__* (preview_start, javascript_tool), mcp__postgres__execute_sql для проверки (схема cm), Bash/Write для подготовки JSON."
---

# Перенос сценария на платформу (2.0)

Сценарий заводится **одним JSON-файлом** через «Импорт JSON» в панели фильтров на `/scenarios` (окно с «Проверить» и
«Импортировать») или напрямую API `POST /api/v1/scenarios/import?dryRun=true|false&name=`. Ручное заполнение вкладок
рабочего места — запасной путь: на сценарий среднего размера (≈20 локаций, ≈18 НПС) это сотни касаний.

Знание формата и правил — `src/CampaignManager.Server/Scenarios/CLAUDE.md`, раздел «Импорт и экспорт файлом»; типы —
`src/CampaignManager.Contracts/Scenarios/ScenarioExchange.cs` (`ScenarioFile`). Живой образец — «Экспорт JSON» в шапке
любого сценария (`GET /api/v1/scenarios/{id}/export`): экспорт отдаёт ровно то, что принимает импорт.

## Что куда ложится (схема `cm`)

| В файле | Таблица | Как ищется |
|---|---|---|
| `name`, `description`, `location`, `era`, `epoch`, `journal` | `scenarios` (`name`, `summary`, `setting`, `setting_date`, `era`, `body_md`) | — |
| `locations[]` (+ `parent`, `musicTags`, `tracks`) | `scenario_locations`, `location_tracks` | родитель — по имени в файле, трек — по названию фонотеки |
| `locations[].skillChecks[]` | `scenario_checks` | навык — по имени справочника; «ИНТ»/«Интеллект» — характеристика; «Удача» |
| `keyFacts[]` | `scenario_key_facts` | — |
| `handouts[]` | `scenario_handouts` | картинка — адрес файла этой базы (`/api/v1/files/{id}`) |
| `creatures[]` | `scenario_creatures` | `creature` — название бестиария; без него — `name` + `statblock` |
| `items[]` | `scenario_items` | `item` — название справочника; без него (или не нашлось) — свой реквизит `name` |
| `npcs[]` | `characters` (`kind = Npc`) + `scenario_npcs` | НПС с тем же именем в библиотеке **занимается**, а не заводится |
| `pregens[]` | `characters` (`kind = Pregen`, `scenario_id`) | всегда новые листы |

Имена везде сравниваются **без регистра и «ё»**. Идентификаторов в файле нет и не нужно.

## Порядок работы

1. **Прочитать источник целиком.** Книга правил — `X:\Knowledge\CallOfCthulhu\*.md` (`15-end.md` — глава 15 «Сценарии»,
   `11-15.md` — глава 14 со статблоками). Имена, числа и потери Рассудка переносятся точно; описания — **пересказом**,
   не цитатой (D5: текст книги в публичный репозиторий не кладётся, а файл сценария может туда попасть как пример).
2. **Уточнить у пользователя** глубину (только сценарий / + листы НПС / + твари и предметы / + прегены).
3. **Проверить справочники**, на которые сошлётесь по имени: твари, предметы, треки, навыки (запросы ниже). Тварь,
   которой нет в бестиарии, лучше сначала завести на `/bestiary` (или приложить `statblock` — она станет тварью только
   этого сценария).
4. **Собрать JSON** в скратчпаде.
5. **Пробный прогон** (`dryRun=true`) — отчёт по строкам; исправлять, пока `failed = 0`.
6. **Импорт** и **сверка с базой**.

## Формат файла

```json
{
  "name": "Название", "location": "Бостон, Массачусетс", "era": "Июнь 1925 года", "epoch": "Classic",
  "description": "Кратко для списка", "journal": "## Markdown Хранителя …",
  "keyFacts": [{ "title": "…", "type": "Backstory", "content": "…" }],
  "locations": [{
    "name": "День 1. Инструктаж", "address": "…", "description": "Markdown",
    "parent": "имя другой локации из этого же файла",
    "musicTags": ["тайна"], "tracks": ["Название трека фонотеки"],
    "skillChecks": [
      { "skillName": "Внимание", "difficulty": "Hard", "successResult": "…", "failureResult": "…" },
      { "skillName": "ИНТ" }, { "skillName": "Удача", "difficulty": "Extreme" }
    ]
  }],
  "handouts": [{ "name": "Пометка Хранителя", "description": "Текст для игроков", "keeperNote": "Только Хранителю" }],
  "creatures": [{ "creature": "Глубоководный", "name": "Вожак", "count": 2, "location": "в гроте", "notes": "…" }],
  "items": [{ "item": "Фонарь" }, { "name": "Дневник Пирса", "description": "…", "location": "кабинет" }],
  "npcs": [{
    "name": "Джозеф Тёрнер", "role": "Enemy", "count": 1, "notes": "заметка о появлении именно здесь",
    "occupation": "…", "age": 45, "gender": "мужской", "backstory": "…",
    "characteristics": { "str": 70, "con": 60, "siz": 80, "dex": 55, "int": 65, "app": 40, "pow": 75, "edu": 50 },
    "hitPoints": 14, "magicPoints": 15, "sanity": 40, "luck": 0,
    "damageBonus": "+1d4", "build": "1", "moveSpeed": 7, "dodge": 27,
    "skills": { "Ближний бой (драка)": 50, "Скрытность": 55, "Язык, иностранный (латынь)": 30 }
  }],
  "pregens": [{ "name": "…", "…": "те же поля, что у НПС, без role/count/notes",
                "weapons": [{ "name": "Револьвер .38", "skill": "Стрельба (пистолет)", "damage": "1d10", "range": "15 ярдов", "attacks": "1 (3)", "ammo": "6", "malfunction": "100" }],
                "biography": { "appearance": "…", "traits": "…" }, "equipment": [{ "name": "Лупа" }] }]
}
```

- `type` факта: `Backstory` | `Truth` | `Timeline` | `Reward`. `role` НПС: `Enemy` | `Ally` | `Neutral`. `count` — 1–99.
- `difficulty`: пусто (обычная) | `Hard` | `Extreme`.
- `epoch`: `Classic` | `Modern`; нет — угадывается по году в `era` (1890–1949 — классика). `era` — время действия текстом.
- Характеристики в порядке статблока: СИЛ→`str`, ВЫН→`con`, ТЕЛ→`siz`, ЛВК→`dex`, ИНТ→`int`, НАР→`app`, МОЩ→`pow`, ОБР→`edu`.
- Лист НПС собирает `SheetBuilder.FromImport` (как помощник): ПЗ/ПМ/БкУ/Комплекция/Скорость, **расходящиеся с формулой**,
  становятся поправками листа; `dodge` — значение навыка Уклонение; `sanity` — текущий Рассудок книги. Навык — имя
  справочника, старое написание v1 или «Родитель (уточнение)»; не нашёлся — свой навык (отчёт его назовёт).
- `parent` — **по имени**: опечатка не роняет импорт, локация уходит на верхний уровень с предупреждением.
- Поля анонса v1 (`isTemplate`, `isPublished`, `scheduledDate`, `announcementText`) читаются и отбрасываются: анонс и
  запись игроков — у прохождения.
- Импорт **всегда** создаёт новый сценарий; `name` в запросе — название копии. Обновления «на месте» нет: переделать —
  импортировать заново и удалить старый.
- **Одна транзакция**: хоть одна строка с ошибкой (навыка нет в справочнике, твари нет в бестиарии и нет статблока,
  `count: 0`, пустое имя) — не записано ничего, отчёт перечисляет все ошибки сразу.

### Как раскладывать материал сценария

- Локации = **сцены**, а не только места: `День 2. Утёс Юджина Клейтона` полезнее, чем `Утёс`. Хронология в названии даёт
  Хранителю порядок за столом. Вложенность — для того, что внутри: бараки внутри раскопок, погреб внутри хижины.
- В `journal` — то, что не привязано к сцене: мотивы сыщиков, общие правила, слабости нежити, сводка параметров.
- В `keyFacts` — предыстория, зловещая истина, хронология по дням, итоги и награды.
- В `handouts` — то, что выдают игрокам. В `description` — **только** текст для игроков (его показывают на весь экран и
  на втором экране); всё, что адресовано Хранителю («зачитайте, когда…», «проверка Рассудка 1/1d6»), — в `keeperNote`.
  Картинку раздатки загружают потом на вкладке «Раздатки» (адрес файла из другой базы импорт пропустит).
- Статблоки, которые книга не приводит («стандартный служитель Гла'аки»), — ссылкой на тварь бестиария (`creature`).

## Проверка справочников до импорта (`mcp__postgres__execute_sql`, ветка Neon `dev`)

```sql
select name from cm.creatures where lower(name) = lower('Глубоководный');
select name from cm.items where lower(name) like lower('%фонарь%');
select name from cm.music_tracks order by name;
select name from cm.skills where lower(name) like lower('%наука%') order by name;
select name from cm.characters where kind = 'Npc' and status <> 'Archived' and lower(name) = lower('Джозеф Тёрнер');
```

## Валидация файла до импорта

```bash
node -e "const d=JSON.parse(require('fs').readFileSync(process.argv[1],'utf8'));const n=new Set((d.locations||[]).map(l=>l.name.toLowerCase()));(d.locations||[]).filter(l=>l.parent&&!n.has(l.parent.toLowerCase())).forEach(l=>console.log('BAD PARENT:',l.name,'->',l.parent));console.log('ok',(d.locations||[]).length,(d.keyFacts||[]).length,(d.handouts||[]).length,(d.npcs||[]).length,(d.pregens||[]).length)" scenario.json
```

## Импорт

Сервер 2.0 запускается из своего worktree (`dotnet run --project src/CampaignManager.Server --urls https://localhost:<порт>`,
Development), вход — тестовый: `https://localhost:<порт>/dev/login?as=keeper&returnUrl=/scenarios`. Браузер — своя вкладка
(`preview_start` с `url`). Экран — WebAssembly: паузы circuit и диалога переподключения больше нет.

Файл на 80 тыс. символов не гонять через `form_input` и не вставлять строкой в `javascript_tool` — он целиком пройдёт
через контекст. Проще всего — API из `curl` с тем же тестовым входом (кука в файле; только Development):

```bash
B=https://localhost:<порт>; J=$SCRATCH/cm-cookies.txt
curl -sk -c $J -o /dev/null "$B/dev/login?as=keeper&returnUrl=/scenarios"
# пробный прогон: failed должен быть 0
curl -sk -b $J -H 'Content-Type: application/json' --data-binary @scenario.json "$B/api/v1/scenarios/import?dryRun=true"   | node -e "const r=JSON.parse(require('fs').readFileSync(0,'utf8'));console.log('failed',r.failed);r.lines.filter(l=>l.outcome==='Failed'||l.message).forEach(l=>console.log(l.part,l.name,l.outcome,l.message));r.warnings.forEach(w=>console.log('!',w))"
# запись; scenarioId — новый сценарий
curl -sk -b $J -H 'Content-Type: application/json' --data-binary @scenario.json "$B/api/v1/scenarios/import?dryRun=false"
# экспорт — образец формата
curl -sk -b $J "$B/api/v1/scenarios/<id>/export" -o export.json
```

Тестовый вход заводит `dev-keeper@cm.test` — он и станет автором. Импорт под настоящей учёткой — через окно в браузере:
кнопка «Импорт JSON» (`data-testid="scenario-import-open-dialog"`), поле файла `scenario-import-file` (из
`javascript_tool` — `File` через `DataTransfer` и событие `change`), «Проверить» — `scenario-import-check`,
«Импортировать» — `scenario-import-run`, отчёт — `scenario-import-report`. Статика сервера (`MapStaticAssets`) знает только
файлы сборки — подложить файл в `wwwroot` и забрать `fetch`-ем, как в v1, не выйдет.

На ветке Neon `dev` лежат перенесённые сценарии — их **только читают и экспортируют**. Пробные импорты называть
с префиксом задачи (`[T…] …`) и удалять после проверки (`DELETE /api/v1/scenarios/{id}`; прегены удалённого сценария
остаются в библиотеке — убрать их в архив: `PUT /api/v1/characters/{id}/status` с `If-Match`). В прод не писать.

## Сверка результата (`mcp__postgres__execute_sql`)

```sql
select s.id, s.name, s.era, s.setting_date,
       (select count(*) from cm.scenario_locations l where l.scenario_id = s.id) locations,
       (select count(*) from cm.scenario_checks c join cm.scenario_locations l on l.id = c.location_id where l.scenario_id = s.id) checks,
       (select count(*) from cm.scenario_key_facts f where f.scenario_id = s.id) facts,
       (select count(*) from cm.scenario_handouts h where h.scenario_id = s.id) handouts,
       (select count(*) from cm.scenario_creatures c where c.scenario_id = s.id) creatures,
       (select count(*) from cm.scenario_items i where i.scenario_id = s.id) items,
       (select count(*) from cm.scenario_npcs n where n.scenario_id = s.id) cast_count,
       (select count(*) from cm.characters p where p.scenario_id = s.id and p.kind = 'Pregen') pregens
from cm.scenarios s where s.name = '<название>';
```

Дерево локаций:

```sql
select l.name, p.name as parent, l.ord
from cm.scenario_locations l left join cm.scenario_locations p on p.id = l.parent_id
where l.scenario_id = '<id>' order by p.name nulls first, l.ord;
```

## Проверка на iPad

Основное устройство — iPad Pro M2: окно импорта и рабочее место смотреть на `resize_window` 1366×1024 и 1024×1366,
потом `preset: "desktop"`.
