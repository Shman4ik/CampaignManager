---
name: scenario-import
description: "Переносит сценарий «Зова Ктулху» из книги правил или стороннего текста на платформу CampaignManager одним JSON-файлом через UI-импорт. USE FOR: занести сценарий на платформу, перенести приключение из книги правил, добавить сценарий в базу, заполнить локации/ключевые факты/раздатки/состав НПС, экспортировать сценарий в JSON, обновить существующий сценарий целиком. DO NOT USE FOR: правку одного поля уже заведённого сценария (быстрее руками в UI), создание кампании, генерацию персонажей игроков. INVOKES: mcp__Claude_Browser__* (preview_start, navigate, javascript_tool), mcp__postgres__execute_sql для проверки, Bash/Write для подготовки JSON."
---

# Перенос сценария на платформу

Сценарий заводится **одним JSON-файлом** через кнопку «Импорт JSON» на `/scenarios`.
Ручное заполнение форм — запасной путь: на сценарий среднего размера (≈20 локаций, ≈18 НПС)
уходит 80–100 обращений к браузеру против 4–5 при импорте.

## Что куда ложится

| Сущность | Куда | Как заводится |
|---|---|---|
| Основные поля, `journal` (Markdown) | колонки `Scenarios` | импорт |
| Локации + проверки навыков | JSONB `Locations` | импорт |
| Ключевые факты | JSONB `KeyFacts` | импорт |
| Раздатки | JSONB `Handouts` | импорт |
| Листы НПС + роль/количество | таблицы `Characters` + `ScenarioNpcs` | импорт |
| Существа из бестиария | JSONB `ScenarioCreatures` | **руками**, `AddCreatureModal` |
| Предметы | JSONB `ScenarioItems` | **руками**, `AddItemModal` |

Существа и предметы выбираются из общих библиотек, поэтому импорт их не заводит: сначала
запись должна появиться в `/bestiary` или `/items`, потом её привязывают к сценарию.

## Порядок работы

1. **Прочитать источник целиком.** Книга правил лежит в `X:\Knowledge\CallOfCthulhu\*.md`
   (`15-end.md` — глава 15 «Сценарии», `11-15.md` — глава 14 «Чудовища, звери и инопланетные боги»
   со статблоками). Не пересказывать по памяти: имена, числа и потери РАС переносятся дословно.
2. **Уточнить у пользователя** глубину (только сценарий / + существа / + листы НПС / + предметы)
   и статус (`isTemplate`, `isPublished`). Это меняет объём работы в разы.
3. **Собрать JSON** в скратчпаде. Формат — `Components/Features/Scenarios/Model/ScenarioImportDto.cs`;
   живой образец всегда можно получить кнопкой «Экспорт JSON» на любом заведённом сценарии.
4. **Проверить JSON** до импорта (см. «Валидация» ниже).
5. **Импортировать** через UI и **сверить с базой**.
6. **Дозавести существ и предметы** руками, если они в объёме.

## Формат файла

Минимальный скелет; полный список полей — в DTO.

```json
{
  "name": "Название", "location": "Город", "era": "Июнь 1925 года",
  "isTemplate": true, "isPublished": false,
  "description": "...", "journal": "## Markdown Хранителя ...",
  "keyFacts": [{ "title": "...", "type": "Backstory", "content": "..." }],
  "locations": [{
    "name": "День 1. Инструктаж", "address": "...", "description": "Markdown",
    "parent": "имя другой локации из этого же файла",
    "skillChecks": [{ "skillName": "Внимание", "difficulty": "Hard",
                      "successResult": "...", "failureResult": "..." }]
  }],
  "handouts": [{ "name": "...", "description": "Markdown", "fileUrl": null }],
  "npcs": [{
    "name": "Джозеф Тёрнер", "role": "Enemy", "count": 1,
    "notes": "заметка о появлении именно в этом сценарии",
    "occupation": "...", "age": 0, "gender": "мужской", "backstory": "...",
    "characteristics": { "str": 70, "con": 120, "siz": 80, "dex": 55,
                         "int": 65, "app": 0, "pow": 75, "edu": 0 },
    "hitPoints": 20, "magicPoints": 15, "sanity": 0, "luck": 0,
    "damageBonus": "+1d4", "build": "1", "moveSpeed": 7, "dodge": 15,
    "skills": { "Ближний бой (драка)": 50, "Уклонение": 15, "Скрытность": 55 }
  }]
}
```

- `type` ключевого факта: `Backstory` | `Truth` | `Timeline` | `Reward`.
- `difficulty`: пусто (обычная) | `Hard` | `Extreme`.
- `role` НПС: `Enemy` | `Ally` | `Neutral`.
- Характеристики в порядке статблока: СИЛ→`str`, ВЫН→`con`, ТЕЛ→`siz`, ЛВК→`dex`,
  ИНТ→`int`, НАР→`app`, МОЩ→`pow`, ОБР→`edu`.
- **`parent` — по имени**, не по идентификатору. Опечатка не роняет импорт: локация останется
  на верхнем уровне, а предупреждение появится в отчёте.
- Импорт **всегда создаёт новый сценарий**. Обновления «на месте» нет: чтобы переделать,
  удалите старый и импортируйте заново.
- НПС с таким же именем уже в библиотеке — импорт **занимает существующий лист**, параметры из
  файла к нему не применяются (о чём пишет в предупреждениях).

### Как раскладывать материал сценария

- Локации = **сцены**, а не только места: `День 2. Утёс Юджина Клейтона` полезнее, чем `Утёс`.
  Хронология в названии даёт Хранителю порядок за столом.
- Вложенность — для того, что находится внутри: бараки внутри места раскопок, погреб внутри хижины.
- В `journal` — то, что не привязано к сцене: мотивы сыщиков, общие правила, слабости нежити,
  сводная таблица параметров, примечание об опасности.
- В `keyFacts` — предыстория, зловещая истина, хронология по дням, итоги и награды.
- В `handouts` — то, что зачитывают или выдают игрокам дословно.
- Статблоки, которые книга не приводит («используйте стандартные параметры служителя Гла'аки»),
  берите из бестиария платформы: `select * from games."Creatures" where "Name" = '...'`.

## Валидация до импорта

```bash
node -e "const fs=require('fs');const d=JSON.parse(fs.readFileSync(process.argv[1],'utf8'));const n=new Set(d.locations.map(l=>l.name));d.locations.filter(l=>l.parent&&!n.has(l.parent)).forEach(l=>console.log('BAD PARENT:',l.name,'->',l.parent));console.log('ok',d.locations.length,d.keyFacts.length,d.handouts.length,d.npcs.length)" scenario.json
```

## Импорт через UI

Файл на 80 тыс. символов не стоит гнать через `form_input` — он целиком пройдёт через контекст.
Положите его временно в `wwwroot/` и заберите из страницы через `fetch`, затем **удалите файл**.

```bash
cp scenario.json X:/source/CampaignManager/CampaignManager.Web/wwwroot/_import-tmp.json
```

```js
// mcp__Claude_Browser__javascript_tool на /scenarios
const openBtn = [...document.querySelectorAll('button')].find(b => b.textContent.includes('Импорт JSON'));
openBtn.click();
await new Promise(r => setTimeout(r, 1500));
const ta = document.getElementById('importJson');
ta.value = await (await fetch('/_import-tmp.json')).text();
ta.dispatchEvent(new Event('input',  { bubbles: true }));
ta.dispatchEvent(new Event('change', { bubbles: true }));   // @bind слушает change
await new Promise(r => setTimeout(r, 800));
[...document.querySelectorAll('button')].find(b => b.textContent.includes('Импортировать')).click();
```

```bash
rm X:/source/CampaignManager/CampaignManager.Web/wwwroot/_import-tmp.json
```

## Сверка результата

```sql
select s."Name",
       jsonb_array_length(s."Locations") loc,
       jsonb_array_length(s."KeyFacts")  kf,
       jsonb_array_length(s."Handouts")  ho,
       (select count(*) from games."ScenarioNpcs" n where n."ScenarioId" = s."Id") cast_count
from games."Scenarios" s where s."Name" = '<название>';
```

Родители локаций и листы НПС:

```sql
select l->>'Name', (l->>'ParentLocationId') is not null as nested
from games."Scenarios" s, jsonb_array_elements(s."Locations") l
where s."Id" = '<id>' order by (l->>'Order')::int;
```

Таблица листов персонажей называется `games."Characters"` (не `CharacterStorage`).

## Грабли автоматизации Blazor-страниц

Это то, на чём реально теряется время. Всё проверено на этом проекте.

- **Вкладку надо держать активной.** `wwwroot/js/circuit-persistence.js` через 5 секунд после
  ухода вкладки в фон зовёт `Blazor.pauseCircuit()`, и появляется «Сеанс приостановлен».
  Скрытая панель браузера считается фоном. Лечится так:

  ```js
  Object.defineProperty(document, 'visibilityState', { get: () => 'visible', configurable: true });
  Object.defineProperty(document, 'hidden',          { get: () => false,     configurable: true });
  ```

  Переживает только текущую страницу — повторяйте после каждой навигации. Если диалог уже висит,
  сначала нажмите в нём «Продолжить».
- **После клика нужна пауза.** `read_page` сразу после `computer:left_click` отдаёт разметку
  *до* перерисовки, и вы работаете со старым DOM. Между ними — `wait` 1–2 секунды.
- **`ref_*` протухают** после любой перерисовки Blazor. Для многошаговых сценариев надёжнее
  `javascript_tool` с поиском элемента по тексту прямо перед кликом.
- **`@bind` слушает `change`**, а не `input`. Установка `.value` из JS требует
  `dispatchEvent(new Event('change', { bubbles: true }))`, иначе модель не обновится и кнопка
  сохранения останется выключенной.
- **Идентификаторы в DOM не уникальны.** На странице сценария и `AddCreatureModal`, и
  `AddItemModal` держат поля `quantity` / `location` / `notes`, и обе всегда в разметке.
  `document.getElementById('notes')` попадёт в *существ*. Ищите поле внутри видимой модалки
  (`el.offsetParent !== null`) или используйте уникальные `edit-item-*` / `edit-creature-*`.
- **`blazor-error-ui` со `style.display: block` — circuit мёртв.** Дальше клики не проходят,
  и никакие таймауты не помогут: перезагрузите страницу. Проверять так:
  `document.getElementById('blazor-error-ui').style.display`.
- **Координаты скриншота ≠ координаты страницы.** Кадр приходит масштабированным
  (например, 800×504 при `window.innerWidth` 2342). Делите координаты из
  `getBoundingClientRect()` на `innerWidth / кадр.width`.
- **Проверяйте на iPad-вьюпортах.** Основное устройство проекта — iPad Pro M2:
  `resize_window` на 1366×1024 и 1024×1366, сброс через `preset: "desktop"`.

## Ручной ввод, когда импорт не подходит

- **Ключевые факты** на `/scenarios/{id}/edit` — это **не модалка**, а строка редактирования
  прямо в списке: «Добавить» дописывает пустую строку, у неё две иконки — дискета (сохранить)
  и корзина (удалить). Каждое сохранение сразу пишет в базу.
- **Локации и раздатки** — настоящие модалки, тоже с немедленной записью.
- Порядок (`Order`) проставляется автоматически по позиции.
