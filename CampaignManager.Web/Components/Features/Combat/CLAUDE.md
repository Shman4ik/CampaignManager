# Combat Feature

Combat-encounter resolution per Call of Cthulhu 7e rules (Chapter 6).

## Key Services
- `CombatService` (`sealed partial class`) — **not** the standard DI/DbContextFactory pattern (see root `CLAUDE.md` "Service Pattern"). It's a stateful, in-memory session service holding `Combatants`, `CurrentRound`, `CurrentTurnIndex`, `CombatLog`. Split across partial-class files — check for siblings before assuming `CombatService.cs` is the whole implementation.

## Персистентность
`CombatService` живёт в circuit, поэтому сам по себе бой не переживает ни обрыв связи, ни уход
вкладки в фон — на планшете за столом это происходит регулярно. Поэтому:
- `CreateSnapshot()` / `RestoreSnapshot()` (`CombatService.State.cs`) сериализуют состояние боя,
  включая `CharacterSource` и `CreatureSource` внутри участников.
- Свойство `PersistedState` помечено `[PersistentState]`, сервис зарегистрирован через
  `RegisterPersistentService` в `Program.cs` — Blazor сам сохраняет снапшот при паузе circuit
  и возвращает его при возобновлении (см. корневой `CLAUDE.md`, «Circuit State Persistence»).
- Добавил поле в состояние боя — добавь его в `CombatSnapshot`, `CreateSnapshot` и
  `RestoreSnapshot`, иначе оно молча потеряется при переподключении.
- В отличие от погони, бой **не** сохраняется в базу: он переживает переподключение и деплой,
  но не полную перезагрузку вкладки.

## Key Models
- `Combatant`, `CombatActionResult`, and per-action setup types: `AttackSetup`, `ManeuverSetup`, `FleeSetup`, `CoverSetup`, `SanityCheckSetup`.
- `CombatSide` (`Party` / `Enemy` / `Neutral`) — сторона участника. Заменяет прежний флаг
  «игрок / существо»: союзный НПС теперь стоит рядом с отрядом, и правило шальной пули
  (`FindUnluckiestAlly`, стр. 112) ищет союзника по стороне, а не по «это лист игрока».
  Сторона берётся из роли НПС в сценарии (`NpcRole.Ally → Party`, `Enemy → Enemy`, иначе `Neutral`).
- `ParticipantOption` / `ParticipantSourceKind` — строка списка «добавить участника»: несёт исходную
  сущность, сторону и количество, поэтому годится и бою, и погоне.

## Названия навыков и осечка

Числовые характеристики оружия спрашиваются у `Weapons/Services/WeaponStatsReader`, а не
у строковых полей: `AmmoCapacity`, `TryMalfunctionThreshold`, `BaseRangeMeters`, `Attacks`.
Он сначала смотрит разобранное поле (`AmmoInfo`, `MalfunctionThreshold`, …), и только если
его нет — разбирает строку на лету. Вторая ступень существует ради старых копий оружия
в JSONB листов: они записаны до появления разобранных полей.

- `CombatService.FindSkillValue(character, weapon)` — сначала `Weapon.SkillId` (точная
  ссылка на `games."Skills"`, есть у каталожного оружия и у копий, снятых с каталога),
  потом точное имя, и только потом эвристика.
- `SkillNameMatcher` **остаётся**: оторванные копии в листах и оружие, заведённое
  Хранителем руками, ссылки не несут и хранят свободную строку. Каталог оружия писал навык
  сокращённо («Стрельба (П)»), лист сыщика — полностью («Стрельба (пистолет)»); сравнение
  строк «в лоб» давало 0 у любого стрелка, поэтому база и специализация сравниваются
  отдельно, с таблицей сокращений и совпадением слов по префиксу. Новое сокращение — в
  `SpecializationAliases`, а не в `FindSkillValue`.
- Порог осечки (стр. 113) — это `Weapon.MalfunctionThreshold`. Строковый разбор
  («100», «00» — это 100 на процентных костях, пустая строка) живёт в
  `WeaponStatsParser.ParseMalfunction` и доступен через `WeaponStatsReader` как ветка
  совместимости. `int.Parse` на «00» когда-то давал 0, и оружие клинило при любом броске;
  порог вне 1–100 считается отсутствующим.

## Существа в бою

Числа существа берутся из разобранного статблока (`Bestiary/CLAUDE.md`), а не из
свободного текста:

- **Бонус к урону** — `AverageDamageBonus` (формула «+2d6»), а режим его применения
  приходит в `AttackSetup.CreatureDamageBonus` из самой атаки: книга пишет его прямо в
  строке урона — «урон 2d6 + БкУ», «+ ½ БкУ» у акулы, «равен БкУ» у шоггота (стр. 278).
  Для оружия сыщика поле пустое, и режим по-прежнему выводит `ResolveDamageBonusType`.
- **Атак за раунд** — `CreatureCharacteristics.AttacksPerRound`, одна строка на существо.
  Столько же защит за раунд до бонусной кости за численное превосходство (стр. 279).
- **Очерёдность** — по убыванию ЛВК (стр. 110). `Initiative` у существа необязательна:
  ноль означает «взять ЛВК», и `Combatant` так и делает.
- **Удачи у чудовищ нет** — правило шальной пули (стр. 112) ищет невезучего среди
  союзников-сыщиков.

## Проверка Рассудка и привыкание

`SanityCheckPanel` подставляет формулу потери из выбранного существа, а не заставляет
Хранителя набивать её руками. Дальше `SanityCheckSetup.SourceCreature*` тянется до
`ApplyResult`, и `RecordHabituation` складывает потерю в `Character.State.MythosHabituations`
— тот же счётчик, что показывает `Characters/Components/MythosHabituationPanel`.
Счётчик ведётся **по виду тварей и по имени**, а не по особи и не по `CreatureId`:
сотня Глубоководных отнимает столько же, сколько один (стр. 167), а запись Хранитель мог
завести руками до появления твари в бестиарии. Набранный предел урезает саму потерю
в `ResolveSanityCheck` — это и есть привыкание.

## Добавление участников
`Components/ParticipantPicker.razor` — **единственный** список участников, общий с погоней
(`Chase/Pages/ChaseHelperPage.razor` использует его же с `DetailMode="speed"`). Он сам грузит все
источники по `CampaignId` и `ScenarioId`: персонажей кампании, состав НПС сценария, свободных
прегенов сценария, НПС библиотеки и кампании, монстров сценария и бестиарий. Новый источник
добавлять в него, а не отдельной вкладкой на странице.

`Combatant.Id` — идентификатор участника боя, а не листа персонажа: у двух громил из одного листа
он разный (иначе путаются захват, удаление и «чей ход»), а сам лист лежит в `SourceCharacterId`.
Состав сценария с `Count > 1` добавляется сразу пачкой, участники получают номера `#1`, `#2`.

Один лист персонажа выставляется в бой **один раз**: страница отдаёт списку `UsedCharacterIds`
(по `SourceCharacterId` участников), строка такого листа гаснет с подписью «уже в бою», а
`HandleParticipantPicked` дополнительно отсекает повтор. Существа дублируются свободно — трёх
одинаковых глубоководных Хранитель ставит одним и тем же выбором. Погоня делает то же самое
со своими `Participants`.

## Notes
- Same deliberate deviation from the CRUD service template as `Chase/CLAUDE.md`'s `ChaseService` — both are runtime resolution engines, not persistence services.

## Оформление (UI)
- Только палитра дизайн-системы: `primary` / `secondary` / `accent` / `success` / `warning` / `error`.
  Дефолтные тейлвиндовские `blue-*`, `red-*`, `green-*`, `purple-*`, `orange-*`, `yellow-*`, `amber-*` здесь не используются —
  они ярче общего тона приложения. Нейтральный серый (`gray-*`) остаётся как в остальном коде.
- Иконки — Font Awesome (`<i class="fa-solid fa-…"></i>`), как в `Sidebar`/`AboutPage`. Эмодзи не использовать:
  они цветные и выбиваются из стиля.
- Бейджи состояний в `CombatantCard` — константы `Badge*` в самом компоненте: заливка `-100`, текст `-800`, рамка `-200`.
  Цвет кодирует **тяжесть** состояния (`BadgeGood` / `BadgeImpaired` / `BadgeCritical` / `BadgeNeutral`), а не конкретное
  состояние — его называет подпись. Плотная заливка оставлена только для смерти, агонии и бессрочного безумия.
- Вкладки действий и кнопки источников участников (`CombatHelperPage`) описаны массивами `Tabs` / `*AddModes`
  и подсвечиваются одним акцентом; новую вкладку добавлять в массив, а не отдельной кнопкой со своим цветом.
- Классы должны быть литералами: Tailwind сканирует исходники и не видит интерполированные имена вроде `bg-{variant}-100`.
