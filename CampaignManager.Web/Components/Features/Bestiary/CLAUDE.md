# Bestiary Feature

Creature/monster catalog for Call of Cthulhu 7e (independent entity — not owned by a Campaign or Scenario).

## Key Services
- `CreatureService(dbContextFactory, IMemoryCache, logger)` — CRUD + cached lookups.

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

## Notes
- `CreatureCharacteristics.SanityLoss` — потеря рассудка при встрече в формате «успех/провал»
  («0/1d6»). Лежит в JSONB, отдельной колонки нет. Из провальной части
  `Services/SanityLossFormula.MaxLoss` выводит предел привыкания к ужасному (стр. 167),
  которым пользуется лист персонажа (`Characters/Components/MythosHabituationPanel`).
  Пустая строка — предел придётся вписать Хранителю вручную, поэтому парсер возвращает 0,
  а не «угадывает».
- Scenarios embed creatures via `ScenarioCreature : Creature` (see `Scenarios/CLAUDE.md`) rather than referencing the Bestiary catalog directly — a scenario's creature instance is its own row, not a foreign key.
