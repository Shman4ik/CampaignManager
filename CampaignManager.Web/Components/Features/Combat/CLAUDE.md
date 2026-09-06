# Combat Feature

Combat-encounter resolution per Call of Cthulhu 7e rules (Chapter 6).

## Key Services
- `CombatService` (`sealed partial class`) — **not** the standard DI/DbContextFactory pattern (see root `CLAUDE.md` "Service Pattern"). It's a stateful, in-memory session service holding `Combatants`, `CurrentRound`, `CurrentTurnIndex`, `CombatLog`. Split across partial-class files — check for siblings before assuming `CombatService.cs` is the whole implementation.

## Key Models
- `Combatant`, `CombatActionResult`, and per-action setup types: `AttackSetup`, `ManeuverSetup`, `FleeSetup`, `CoverSetup`, `SanityCheckSetup`.

## Notes
- Same deliberate deviation from the CRUD service template as `Chase/CLAUDE.md`'s `ChaseService` — both are runtime resolution engines, not persistence services.
