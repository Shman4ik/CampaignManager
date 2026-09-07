# Combat Feature

Combat-encounter resolution per Call of Cthulhu 7e rules (Chapter 6).

## Key Services
- `CombatService` (`sealed partial class`) — **not** the standard DI/DbContextFactory pattern (see root `CLAUDE.md` "Service Pattern"). It's a stateful, in-memory session service holding `Combatants`, `CurrentRound`, `CurrentTurnIndex`, `CombatLog`. Split across partial-class files — check for siblings before assuming `CombatService.cs` is the whole implementation.

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
