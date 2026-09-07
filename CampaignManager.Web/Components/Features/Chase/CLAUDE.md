# Chase Feature

Chase-scene resolution per Call of Cthulhu 7e rules (Chapter 7).

## Key Services
- `ChaseService` — **not** the standard DI/DbContextFactory pattern (see root `CLAUDE.md` "Service Pattern"). It's a stateful, in-memory session service holding `Phase`, `Participants`, `Locations`, `CurrentRound`, `CurrentTurnIndex`, `ChaseLog`. The Keeper enters all dice results manually — the service never rolls dice itself.

## Key Models
- `ChaseParticipant`, `ChaseLocation`, `ChaseActionResult`.

## Notes
- If you're extending chase resolution, follow the existing stateful-service shape rather than converting it to the DbContextFactory CRUD pattern — it mirrors `Combat/CLAUDE.md`'s `CombatService`, which has the same deliberate deviation.

## Оформление (UI)
Те же правила, что в `Combat/CLAUDE.md` — палитра дизайн-системы вместо дефолтной тейлвиндовской,
иконки Font Awesome вместо эмодзи, литеральные имена классов. Специфика погони:
- Роли закреплены за цветом: жертва — `success`, преследователь — `error`. Это единственное место,
  где цвет несёт собственный смысл, а не тяжесть события.
- Раскраска журнала (`ChaseLog`) кодирует **категорию** действия, а не каждый его вид:
  `accent` — движение и проверка скорости, `warning` — препятствия, `error` — насилие, серый — пропуск.
  Финал погони выделен насыщеннее: `error-700` («Пойман») и `success-700` («Сбежал»).
  Новый `ChaseActionType` добавлять в существующую категорию, а не заводить ему отдельный цвет.
- Маркеры на треке и в легенде: преграда — `fa-road-barrier` в `accent-600`, помеха — `fa-bolt` в `warning-600`.
