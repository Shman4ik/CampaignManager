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
