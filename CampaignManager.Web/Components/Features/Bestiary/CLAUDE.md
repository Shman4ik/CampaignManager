# Bestiary Feature

Creature/monster catalog for Call of Cthulhu 7e (independent entity — not owned by a Campaign or Scenario).

## Key Services
- `CreatureService(dbContextFactory, IMemoryCache, identityService, logger)` — CRUD + cached lookups.
  Правят только Хранитель и администратор — как у оружия (`Weapons/CLAUDE.md`, «Права»):
  `Create/Update/DeleteCreatureAsync` зовут `EnsureKeeperAsync`, `CreatureCard` получает `CanEdit`,
  без него «Изменить»/«Удалить» не рисуются. Импорт пишет через те же методы.
- `CreatureImportService(creatureService, logger)` — обмен бестиарием одним JSON (`{ "creatures": [...] }`,
  голый массив или одно существо). Кнопки «Импорт JSON»/«Экспорт JSON» на `/bestiary`, экспорт одного
  существа — кнопка «JSON» на странице правки. Формат — `Model/CreatureImportDto.cs`; вложенные части —
  те же классы, что лежат в JSONB, поэтому формат не расходится с моделью.

## Key Models
- `Creature : BaseDataBaseEntity, INamedEntity` — `CreatureCharacteristics`, `Attacks`
  (`List<CreatureAttack>`), `Skills` (`List<CreatureSkill>`), `SpecialAbilities`
  (`Dictionary<string,string>`) и legacy-словарь `CombatDescriptions`.
- `CreatureCharacteristics` повторяет статблок книги (гл. 14, стр. 277–280): СИЛ/ЛВК/ИНТ/ВЫН/МОЩ/ТЕЛ
  как `.Value` + `.DiceRoll`, `HealPoint`, `ManaPoint`, `AverageDamageBonus`, `AverageComplexity`,
  `Speed`/`SwimSpeed`/`FlySpeed`/`SpeedNote`, `AttacksPerRound`(+`Note`), `Armor`(+`ArmorNote`),
  `DodgeSkill`, `SanityLoss`, `Initiative`.
- `CreatureAttack` — одна строка раздела «Бой»: `SkillValue`, `DamageFormula`, `Kind`
  (`CreatureAttackKind`), `DamageBonus` (`CreatureDamageBonusMode`).

## Статблок: что откуда берётся
Книга печатает у каждой твари строки «Броня», «Уклонение», «Атак за раунд», «Навыки» и
«Потеря рассудка». При первой заливке бестиария они осели свободным текстом в
`CombatDescriptions`, а типизированные поля остались пустыми. Миграции
`BestiaryStatblockBackfill` и `BestiaryStatblockModel` разобрали этот текст обратно в поля.
**Источник правды теперь — типизированные поля, а не словарь.** `CombatDescriptions` остаётся
как исходный текст книги (его показывает `CreatureEditPage` только для чтения) и как источник
для повторного разбора; новые данные туда писать не нужно.

- `AverageDamageBonus` — это бонус к **урону** (`+2d6`, `-2`, `0`), а не к попаданию.
  Раньше поле называлось `AverageBonusToHit` и хранило максимум костей числом («48» вместо
  «+8d6»), из-за чего `CombatService` считал его плоским уроном.
- `AttacksPerRound` живёт на существе, а не на атаке: в книге это одна строка на весь статблок.
  Столько же раз тварь может уклониться или контратаковать до бонусной кости за численное
  превосходство (стр. 279).
- `Armor` — только число. Половина тварей несёт «Броня: нет» плюс оговорку («огнестрел наносит
  минимальный урон», «регенерирует 2 ПЗ за раунд») — она в `ArmorNote`, и нулевая `Armor`
  ещё не значит, что существо уязвимо.
- `SwimSpeed`/`FlySpeed` пусты у большинства: пустое плавание означает половину обычной
  скорости, пустой полёт — что летать существо не умеет (стр. 141).
- Наружности, Образования и Удачи книга у чудовищ не печатает — полей под них нет. Старые
  ключи `Appearance`/`Education`/`Luck`/`Constitutions` остались в JSONB у части записей и
  просто игнорируются при десериализации; удалять их миграцией не стали.
- `Initiative` — необязательное переопределение очерёдности. Ноль означает «взять ЛВК»
  (`Combatant` так и делает), потому что ходы идут по убыванию ЛВК (стр. 110).

## Импорт JSON
- В отличие от фонотеки, существо с занятым именем по умолчанию **обновляется** (галочка в окне):
  основной сценарий — привести заведённых тварей к книге. Обновление переписывает статблок целиком,
  но не трогает `CombatDescriptions` (исходный текст книги) и `Images`, если поля `images` в файле нет.
  Пустой массив `images: []` картинки снимает.
- `CombatService.RollDiceFormula` **молча пропускает** нераспознанные куски формулы: «2d6+БкУ» даст
  только 2d6, «1д6» (русская «д» из книги) — ноль. Поэтому импорт нормализует «д»/«−» и
  предупреждает о формуле урона или БкУ, которые бой не бросит (`CreatureImportService.IsRollable`).
- Предупреждения, а не отказ: существо без атак (привидение, рой) или без ПЗ сохраняется, но
  Хранитель видит, что в бою оно поведёт себя не так, как ждёшь.

## Иллюстрации
- `Images` (`List<CreatureImage>`: `Url` + `Caption`) — jsonb-колонка, первая картинка — обложка
  карточки. Заменила одиночный `ImageUrl` (миграция `CreatureImages` перенесла его первым элементом).
- `Url` — объект MinIO («images/beasts/…») или внешняя ссылка; `CreatureImage.ToSrc` решает, куда
  вести тег img. Форма правки грузит файл сразу под постоянным именем
  `images/beasts/{slug}-{8 hex}.{ext}` — прежняя схема писала всех в `{имя}.jpg` и затёрла бы вторую картинку.
- `ScenarioCreature` наследует `Images`, поэтому копия в сценарии (`AddCreatureModal`, клон шаблона
  в `ScenarioService`) переносит их вместе с навыками — раньше навыки при добавлении в сценарий терялись.

## Страница каталога `/bestiary`
- Каркас общий для каталогов (см. «Каталог» в `docs/design-system-v1.md`): `FilterPanel`,
  `LoadingIndicator`, `EmptyState`, `Pagination`. Фильтры сбрасывают страницу на первую.
- По умолчанию страница — **галерея плиток** (`.cr-grid` в `CreaturesPage.razor.css`, `auto-fill`
  по 13rem: пять колонок в альбоме iPad, четыре в портрете). Плитка — обложка 4:5, имя поверх,
  под ней ПЗ / броня / потеря рассудка. Броня 0 с оговоркой в `ArmorNote` на плитке — «особая»,
  а не «нет». На странице 20 существ — ровно четыре ряда в обеих ориентациях.
- Тап по плитке раскрывает `CreatureCard` в полный статблок во всю ширину сетки
  (`col-span-full`, `grid-auto-flow: dense` подтягивает следующие плитки в освободившуюся дыру):
  картинка слева на 40% (до 28rem), статблок справа. Раскрытое существо одно и держится в
  адресе (`?open={id}`), а не в поле — переживает паузу circuit'а и F5.
- После раскрытия страница прокручивает к карточке (`revealElement` в `wwwroot/js/app.js`),
  причём только в рендере, где `Open` уже новый: `NavigateTo` меняет параметр не сразу, и
  рендер сразу после клика ещё рисует плитку. Отступ считается по липким панелям —
  `FilterPanel` липнет под шапку (`top: 56px`), вместе они занимают ~120px. По той же
  причине картинка в раскрытой карточке не липкая: она пряталась бы под фильтрами.
- `CreatureCard` делит статблок на две части: короткие числа (СИЛ…МОЩ, ПЗ, ПМ) — сеткой
  `.cr-stats` с `auto-fill`, где колонки считает ширина самой карточки; строки с оговорками
  (бонус к урону, скорость, атаки за раунд, броня, уклонение, потеря рассудка) — каждая во всю
  ширину. Не складывать их обратно в одну `grid-cols-2`: длинные оговорки рвут её на куски.

## Notes
- `CreatureCharacteristics.SanityLoss` — потеря рассудка при встрече в формате «успех/провал»
  («0/1d6»). Лежит в JSONB, отдельной колонки нет. Из провальной части
  `Services/SanityLossFormula.MaxLoss` выводит предел привыкания к ужасному (стр. 167),
  которым пользуется лист персонажа (`Characters/Components/MythosHabituationPanel`).
  Пустая строка — предел придётся вписать Хранителю вручную, поэтому парсер возвращает 0,
  а не «угадывает».
- Scenarios embed creatures via `ScenarioCreature : Creature` (see `Scenarios/CLAUDE.md`) rather than referencing the Bestiary catalog directly — a scenario's creature instance is its own row, not a foreign key.
