# Spells Feature

Master spell catalog (independent entity).

## Key Services
- `SpellService(dbContextFactory, IMemoryCache, logger)` — CRUD + cached lookups.

## Key Models
- `Spell : BaseDataBaseEntity, INamedEntity` (declared in `SpellModel.cs`).

## Страница каталога `/spells`

Устроена так же, как каталоги оружия, предметов и книг (`Features/Weapons/CLAUDE.md`,
`Features/Items/CLAUDE.md`, `Features/Books/CLAUDE.md`), и правки к ним имеет смысл
вести вместе. Разметку списка держит `Components/SpellsListView.razor` (таблица +
карточки) и `Components/SpellTableRow.razor` (строка), страница отвечает за фильтры,
пагинацию и модалки.

- Колонки: `Название · Тип · Стоимость · Время сотворения · действия`. Их пять, а не
  девять, как у оружия, поэтому порог таблицы `lg` (1024): портрет iPad её вмещает —
  замерено, таблица занимает там 907px ровно по контейнеру. Ниже — карточки.
- **Колонку типа показывает `ShowType`** (`selectedTypeFilter` пуст на странице).
  При фильтре по типу она повторяет фильтр в каждой строке; тогда тип уезжает
  в раскрытие, а освободившаяся ширина достаётся стоимости.
- **Раскрытая строка не повторяет колонки.** Стоимость и время сотворения — длинный
  свободный текст («(Урон*2+1) магии за раунд, 1d20 рассудка»): в строке они обрезаны
  многоточием и разворачиваются на месте (`.sp-row-open > .sp-clip`), а не печатаются
  заново блоком. В раскрытии остаются другие названия и описание — их в колонках нет
  вообще (описание в каталоге длинное, в среднем ~530 знаков, колонкой оно быть не может).
- Плотность и закрепление задаёт `SpellsListView.razor.css`: `max-height` делает
  `.sp-table` скроллпортом, шапка и колонка названия держатся на `position: sticky`,
  страница целиком не прокручивается. `::deep` обязателен — строки рисует дочерний
  `SpellTableRow`. Ширины текстовых колонок — доли (`.sp-col-type/-cost/-time`),
  подобранные замером: стоимость вдвое длиннее времени сотворения, при равных долях
  у времени оставался пустой запас, а стоимость обрезалась.
- Кнопки строки — иконки `cm-btn-sm cm-btn-icon` 36×36 с `aria-label`.
- На странице 25 заклинаний вместо 6: плотная строка занимает одну строку текста.

## Notes
- A character's known spells are just `Character.Spells` (`List<Spell>`) — same catalog type, no separate character-specific spell model (unlike Skills/Weapons, which have character-specific wrapper types).
