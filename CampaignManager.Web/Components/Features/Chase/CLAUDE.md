# Chase Feature

Chase-scene resolution per Call of Cthulhu 7e rules (Chapter 7).

## Key Services
- `ChaseService` — **not** the standard DI/DbContextFactory pattern (see root `CLAUDE.md` "Service Pattern"). It's a stateful, in-memory session service holding `Phase`, `Participants`, `Locations`, `CurrentRound`, `CurrentTurnIndex`, `ChaseLog`. The Keeper enters all dice results manually — the service never rolls dice itself.

## Key Models
- `ChaseParticipant`, `ChaseLocation`, `ChaseActionResult`.

## Notes
- If you're extending chase resolution, follow the existing stateful-service shape rather than converting it to the DbContextFactory CRUD pattern — it mirrors `Combat/CLAUDE.md`'s `CombatService`, which has the same deliberate deviation.
