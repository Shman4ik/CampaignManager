# Расхождения правил v1 с книгой

Журнал карточки **T0.2** (`docs/v2/TASKS.md`). Тесты `tests/CampaignManager.Rules.Tests`
фиксируют **текущее** поведение v1, а не «правильное»: v1 заморожен. Всё, в чём код расходится
с книгой (или сам с собой), записывается сюда, а тест остаётся на текущем поведении и помечается
`[Trait("finding", "F-…")]` — чтобы при исправлении в 2.0 (`T1.7`, `T2.6`) было видно, какие
ожидания меняются осознанно.

Страницы — книга Хранителя «Зов Ктулху» 7e, в той же нумерации, что в комментариях кода v1.

Формат записи: **номер** · где в коде · что делает v1 · что говорит книга · какой тест фиксирует.
Номера по разделам: `F-S…` — лист, `F-C…` — кости и бой, `F-P…` — погоня и разборщики.

## Статус в 2.0

Тесты листа, костей и разборщиков перенесены на `Core` в T1.7 (`tests/CampaignManager.Core.Tests`), тот же
`[Trait("finding", …)]` стоит и там. Ожидание изменено только у находок, которые 2.0 закрывает самим
устройством; остальные перенесены с поведением v1. `tests/CampaignManager.Rules.Tests` (v1) до T3.3 не
трогаем, в T3.3 проект удаляется целиком (решение владельца 2026-10-02).

| Находка | В `Core` (T1.7) | Что дальше |
|---|---|---|
| F-S01 возраст вне 15–89 | как в v1 | исправить в T2.4 — решение владельца 2026-10-02 (и решить, что делать с 90) |
| F-S02 урон ≥ максимума ПЗ | как в v1 («при смерти») | T2.6 — один конвейер ран с боем |
| F-S03 Удача до 01 | как в v1 | **исправлено в T2.7**: уровень с порогом 01 не предлагается (`LuckRules.Options`) |
| F-S04 «Языки» | **исправлено**: родители с общим прогрессом — по коду справочника | — |
| F-S05 Мифы без навыка | **исправлено**: Мифы из справочника, строка заводится; случай засчитывается, только если записан | — |
| F-S06 деньги строкой | **исправлено**: наличные — `decimal?`; `TryParseMoney` — только перенос | T1.3 переносит тесты разбора |
| F-S07 варианты слота | **исправлено устройством**: варианты — FK на справочник | T1.3 отчитывается о несопоставленных |
| F-C01 «д» в формуле | **исправлено** в `DiceFormula` | T2.6: бой и погоня бросают только через неё |
| F-C02…F-C08 | не переносились — бой | T2.6 |
| F-C09 «Автомат» | **исправлено в T2.7 устройством**: Ближний бой и Стрельба — по коду навыка (`CheckRules.IsCombatSkill`) | — |
| F-P01…F-P10 | не переносились — погоня | T2.6 |
| F-P11 предел привыкания | **исправлено**: `SanityLossFormula` и бросок — один разбор | — |
| F-P12 урон и бросок | **исправлено**: разборщик урона понимает всё, что `DiceFormula` | — |
| F-P13 «Шок» | как в v1 (разобран с эффектом) | исправить в T2.1 — решение владельца 2026-10-02 |

## Лист сыщика

Пути — от `CampaignManager.Web/Components/Features/`. Тесты — `tests/CampaignManager.Rules.Tests/Sheet/`.

- **F-S01** · `Characters/Services/InvestigatorCreationRules.cs:13–16, 84–85` · `MinAge`
  цитирует книгу «от 15 до 90 лет», а `MaxAge = 89`; `BandFor` для возраста вне 15–89 (0, 14, 90)
  молча возвращает строку «Молодой» 20–39 — без вычетов и с одной проверкой ОБР. Помощник
  зажимает возраст в 15–89, поэтому сейчас это не всплывает. · Книга (стр. 30, по цитате в
  комментарии v1): возраст от 15 до 90 лет; 90 лет в таблицу не попадают, а возраст вне её — ошибка
  ввода, а не «молодой». ·
  `InvestigatorCreationRulesTests.BandFor_OutsideTable_FallsBackToYoung`.
- **F-S02** · `Characters/Services/WoundRules.cs:23–38` · `ApplyDamage` с уроном одной атаки не
  меньше максимума ПЗ ставит серьёзную рану и «при смерти». Бой (`Combat/Services/CombatService.cs:1330–1337`)
  в том же случае объявляет мгновенную смерть — два движка расходятся. · Книга (стр. 118): урон одной
  атаки ≥ максимума ПЗ — смерть (по комментарию `CombatService`). · `WoundRulesTests.ApplyDamage_AtLeastMaxHp_MarksDying_NotDead`.
- **F-S03** · `Characters/Services/LuckRules.cs:65–75, 84–89` · При навыке 5–9 (и 2–3 для трудного
  уровня) порог уровня равен 1, и вариант «чрезвычайный успех» выкупает бросок до 01 — а 01 по
  `CombatService.CalculateSuccessLevel` уже критический успех. Ярлык варианта и уровень итогового
  броска расходятся. · Книга (стр. 97, по комментарию `LuckRules` и `Characters/CLAUDE.md`):
  критический успех Удачей не покупается — v1 сам отказывает в нём в `CanSpendOn`. · `LuckRulesTests.Options_SmallTarget_ExtremeOptionLandsOnCriticalRoll`.
- **F-S04** · `Characters/Services/SpecializationRules.cs:17–27` · Закрытый список родителей
  содержит старое имя «Языки», а справочник навыков и профессии называют родителя «Язык,
  иностранный» (`StartsWith` не совпадает): иностранные языки бонус смежной специализации не
  получают никогда. · Книга (стр. 76–77) и комментарий v1: языки делят прогресс наравне с ближним
  боем, стрельбой и выживанием. · `SpecializationRulesTests.BonusFor_ForeignLanguageParentFromCatalog_GetsNoBonus`.
- **F-S05** · `Characters/Services/SanityRules.cs:181–188` · `RecordMythosInsanity` на листе без
  навыка «Мифы Ктулху» увеличивает счётчик случаев и возвращает 5, хотя `AddMythos` ничего не
  записал; следующий случай даст уже +1. · Книга (стр. 160–161): первый случай +5, следующие +1;
  v1 противоречит себе — `AddMythos` для того же листа честно возвращает 0. · `SanityRulesTests.RecordMythosInsanity_NoMythosSkill_CountsCaseAndReportsGainAnyway`.
- **F-S06** · `Characters/Services/FinanceRules.cs:94–109` (и `DevelopmentPhaseRules.cs:249–252`,
  пересчёт денег в фазе развития) · `TryParseMoney` выбрасывает всё, кроме цифр, точки и запятой, и
  запятую делает десятичной: «$1,500» → 1,5 (фаза развития превращает 1 500 долларов в 41,50),
  «-50» → 50 (минус теряется), «1/2» → 12. Сам v1 пишет суммы без разделителя тысяч, так что ломается
  ручной ввод Хранителя. · Книга (стр. 94, по комментарию v1): к оставшимся наличным прибавляется
  столбец «Наличные» — от неверно прочитанного остатка. ·
  `FinanceRulesTests.TryParseMoney_Quirks`, `DevelopmentPhaseRulesTests.RecalculateFinances_ThousandsComma_ReadAsDecimal`.
- **F-S07** · `Characters/Services/OccupationSkillResolver.cs:152–157, 204–233` · Для названных
  навыков профессии есть и слот `Unresolved`, и разбор «Родитель (специализация)», а для вариантов
  `SkillChoices` — нет: вариант, которого нет в справочнике (например «Искусство/ремесло (актёрская
  игра)» у Детектива полиции, если такой специализации нет в базе), молча выпадает, а группа без
  единого найденного варианта исчезает целиком — профессия получает меньше восьми слотов без всякой
  пометки. · Книга (стр. 37): ровно восемь профессиональных навыков плюс Средства; v1 сам требует
  это от данных (`RequiredSkillCount`). · `OccupationSkillResolverTests.BuildSlots_ChoiceOptionsMissingFromCatalog_DroppedSilently`.

## Кости и бой

Тесты — `tests/CampaignManager.Rules.Tests/{Dice,Combat,Checks}`. Номера строк — по коду v1 на
момент T0.2 (после шва `RandomOverride`, поэтому у `CombatService.cs` они на 13 больше, чем в AUDIT).

- **F-C01** · `CombatService.cs:325-416` (`RollDiceFormula`, `MaximizeDiceFormula`, регулярка
  `^([+-]?)(\d*)D(\d+)$`) · Понимает только латинскую `d` и ASCII-минус: «1д6», «1Д6», «1D6−1» не
  разбираются и дают 0, ни одной кости не брошено. При этом `Utilities/Services/DamageFormulaParser.cs:23`
  понимает `[dDдД]`, а `Bestiary/Services/SanityLossFormula.MaxLoss` — `[dд]`: «1д6», вписанная
  в потерю рассудка, отнимает 0, а предел привыкания из «0/1д6» записывается как 6. · Книга пишет
  кости как «1D6»/«1d6»; русская «д» — обычная запись в переводе и в данных Хранителя, одна строка
  должна давать одно и то же число и при броске, и при максимуме, и при пределе. · Тесты:
  `Dice/DiceFormulaTests.RollDiceFormula_CyrillicDOrUnicodeMinus_ReturnsZero`,
  `.SanityLossFormula_SameCyrillicFormula_GivesSix`,
  `Combat/SanityCheckTests.ResolveSanityCheck_CyrillicFormula_LosesNothing`,
  `.ApplyResult_CyrillicFormula_LimitSixButLossZero`.
- **F-C02** · `CombatService.cs:693, 762-766` (ближний бой), `:1015-1017, 1039-1040` (стрельба),
  `:1144, 1192` (манёвр) · `Resolve*Attack`/`ResolveManeuver` меняют участников ещё при разрешении,
  до «Применить»: ход и число атак атакующего, счётчик защит цели, прицел, патроны, счётчик
  очереди, заклинившее оружие со случайными 1d6 раундами починки. `CancelPendingResult` ничего
  этого не возвращает (AUDIT «Ошибки, которые не переносить», п. 1). · Отменённый предпросмотр не
  должен тратить ход, патрон и защиту цели; эффекты — только при применении (стр. 106, 111, 113,
  114). · Тесты: `Combat/MeleeAttackTests.ResolveMeleeAttack_ChangesCombatantsBeforeApply`,
  `Combat/RangedAttackTests.ResolveRangedAttack_JamAndAmmoChangedBeforeApply`,
  `.ResolveRangedAttack_VolleyCountsCheckBeforeApply`.
- **F-C03** · `CombatService.cs:221-236` (`DelayTurn`, условие `idx <= CurrentTurnIndex` на
  строке 232) · Если ход откладывает сам текущий боец, индекс уменьшается, и ход достаётся
  предыдущему, уже ходившему (А, **Б**, В → откладывает Б → ходит А). Ход должен перейти к
  следующему (В) (AUDIT, п. 2). · Страницы у отложенного хода в коде нет. · Тест:
  `Combat/RoundAndTurnTests.DelayTurn_CurrentCombatant_TurnRollsBackToPrevious`.
- **F-C04** · `CombatService.cs:52-58` (`RemoveCombatant`) · Индекс хода не сдвигается при
  удалении участника выше текущего: в А, Б, **В**, Г после удаления А ходит Г, а В пропускает ход;
  если текущий был последним — ход прыгает к первому (AUDIT, п. 3). · Удаление не должно менять,
  кто ходит. · Тесты: `Combat/RoundAndTurnTests.RemoveCombatant_BeforeCurrent_SkipsCurrentTurn`,
  `.RemoveCombatant_BeforeLastCurrent_JumpsToFirst`.
- **F-C05** · `Combat/Components/Attack/AttackSetupState.cs:88-89, 255-256, 482-483` · Проверку ВЫН
  при серьёзной ране (своей и от контратаки) в панели атаки вписать нельзя: поля приватные,
  только обнуляются, в `AttackSetup` всегда уходит `null`, и движок бросает d100 сам. Остальные
  броски атаки вписываются (AUDIT, п. 9). · Любой бросок можно не бросать, а вписать с костей
  (правило `Characters/CLAUDE.md`, стр. 117). · Тест:
  `Combat/ApplyResultTests.AttackSetupState_HasNoWayToEnterMajorWoundConRoll`. Остальное из п. 9
  (1d6 раундов починки, встречная ЛВК погони) — только по коду: починка бросается в
  `CombatService.cs:1040` без поля ввода, ЛВК — слой погони.
- **F-C06** · `CombatService.cs:1352, 1411` (ВЫН при серьёзной ране, своя и от контратаки), `:1537`
  (ИНТ при потере 5+ рассудка), `CombatService.Spells.cs:360` (ВЫН при расплате ПЗ за нехватку
  ПМ) · Успех считается как `roll <= value`, мимо `CalculateSuccessLevel`. В диапазоне 1..99 итог
  совпадает (96–99 на ВЫН < 50 и так провал), расходятся края: 100 при значении 100 и выше (ВЫН
  тварей бывает за 100) засчитывается успехом, 01 при значении 0 — провалом. · 01 — всегда
  критический успех, 100 — всегда крах (стр. 87–88). · Тесты:
  `Combat/ThresholdBypassTests` (весь класс). Остальные инлайны из AUDIT (`DyingCheckModal`,
  `MedicinePanel`, `SpellCastPanel`, `TakeCoverPanel`) живут в razor и тестами слоя не покрыты.
- **F-C07** · `CombatService.cs:1078-1095` (`ResolveRangedAttack`) · Прицел (`IsAiming`)
  сбрасывается только при попадании; промах возвращается раньше, и бонусная кость за прицел
  достаётся следующему выстрелу. Сам код в `ResetRoundTracking` (`:206-207`) пишет «прицеливание
  сохраняется до выстрела». · Прицел даёт бонусную кость одному следующему выстрелу и им
  тратится, попал он или нет (стр. 111). · Тест:
  `Combat/RangedAttackTests.ResolveRangedAttack_Miss_KeepsAim` (для контраста —
  `.ResolveRangedAttack_Hit_ConsumesAim`).
- **F-C08** · `CombatService.cs:1761-1764` (`ApplyManeuverEffect`, `ManeuverType.BreakFree`) ·
  «Вырваться» делает схваченный — атакующий манёвра, а успех снимает захват с цели манёвра (того,
  кто держит): `defender.IsGrappled = false`. Сам вырвавшийся остаётся «в захвате» с прежним
  `GrappledBy`. · Успешный манёвр «вырваться» освобождает того, кто его выполнил (стр. 103). ·
  Тест: `Combat/ManeuverTests.ApplyResult_BreakFree_ClearsDefenderNotAttacker`.
- **F-C09** · `Checks/Services/SkillCheckRules.cs:116-118` (`IsCombatSkill`) · Огнестрел узнаётся
  только по префиксу «Стрельба», а в стандартном листе (`SkillsModel.DefaultSkillsModel`, группа
  «Сражение (Огнестрельное)») есть строка «Автомат» — провал по ней диалог проверки предлагает
  повторить. · Ближний бой и Стрельбу (любую специализацию огнестрела) повторно не проверяют
  (стр. 102). · Тест: `Checks/SkillCheckRulesTests.PushBlockReason_SubmachineGunSkill_CanBePushed`.

## Погоня и разборщики

Тесты — `tests/CampaignManager.Rules.Tests/Chase/` и `.../Parsers/`. Где не сказано иное,
файлы погони — `CampaignManager.Web/Components/Features/Chase/Services/`.

**F-P01 · Уровень успеха от урезанного порога.** `ChaseService.cs:570-574` (помеха),
`:655-658` (преграда), `ChaseService.Rules.cs:184-186` (водитель с раной), `:372-374` (след),
`:409-411` (укрытие). Порог трудной и чрезвычайной проверки считается `GetDifficultyThreshold`
(`ChaseService.cs:1230`) как ½ и ⅕ навыка, и уже от него — `CalculateSuccessLevel(roll, threshold)`.
Прошёл или нет — верно, но уровень в результате и в журнале занижен: навык 60, трудная
проверка, бросок 10 → «трудный успех»; навык 100, чрезвычайная, бросок 20 → «обычный».
Книга (стр. 87–89): уровень — от полного навыка, сложность решает только, хватает ли его;
общая функция `CalculateSuccessLevel(roll, skill, difficulty)` так и считает.
Тесты: `ObstacleTests.ResolveHazard_HarderDifficulty_LevelFromCutThreshold`,
`VehicleTests.ResolveDriverControlCheck_LevelFromHalfSkill`,
`OptionalRulesTests.ResolveTrackingCheck_LevelFromCutThreshold`.

**F-P02 · «1д6» в помехе — ноль урона.** `ChaseService.cs:613-616` бросает формулу помехи через
`CombatService.RollDiceFormula`, а тот понимает только латинскую `D`
(`CombatService.cs:415`, `^([+-]?)(\d*)D(\d+)$`). Вписанная в редактор локации «1д6» молча
даёт урон 0, кости не бросаются, в журнале урона нет. По книге (стр. 133) провал помехи
наносит её урон. Тест: `ObstacleTests.ResolveHazard_CyrillicDiceFormula_DealsZero`.

**F-P03 · Удаление участника выше текущего пропускает ход.** `ChaseService.cs:174-180`: индекс
хода после удаления не сдвигается, поэтому удалили того, кто уже ходил, — ход перескакивает через
текущего. По книге (стр. 131–132) ход идёт по порядку ЛВК, удаление чужого участника его не
меняет. Тест: `TurnOrderTests.RemoveParticipant_AboveCurrent_SkipsCurrentTurn`.

**F-P04 · Ход — индекс по отфильтрованному списку активных.** `ChaseService.cs:465-490`:
`CurrentTurnIndex` указывает в `Participants.Where(IsActive)`. Выбыл уже ходивший (ранен,
пойман, спрятался) — список сдвигается, и ход прямо посреди действия переходит к следующему.
Тест: `TurnOrderTests.GetActiveParticipant_ActorAboveDropsOut_TurnJumpsToNext`.

**F-P05 · Отложенное действие переставляет навсегда.** `ChaseService.Rules.cs:684-718` меняет
участников местами в самом `Participants`, а `NextRound` (`ChaseService.cs:492-498`) порядок по
ЛВК не восстанавливает. По книге (стр. 132) уступить можно только в этом раунде.
Тест: `TurnOrderTests.DelayAction_OrderStaysSwappedInNextRound`.

**F-P06 · Транспорт ломает преграду Комплекцией водителя.** `ChaseService.cs:753` берёт
`participant.BuildValue` (Комплекция персонажа; у НПС за рулём часто 0 → одна кость), а таран —
`EffectiveBuild`, Комплекцию машины (`ChaseService.Rules.cs:94`). По книге (стр. 135) транспорт
наносит 1d10 за каждый пункт своей Комплекции. Тесты:
`ObstacleTests.AttemptDestroyBarrier_VehicleNoRoll_RollsDriverBuildNotVehicleBuild`,
`ObstacleTests.AttemptDestroyBarrier_VehicleWithoutDriverBuild_RollsOneDie`.

**F-P07 · Остаток урона машины теряется.** `ChaseService.cs:1104-1105` применяет
`TargetVehicleDamage` (остаток до десятка) только вместе с потерей Комплекции
(`TargetBuildLoss is > 0`). Удар меньше 10 по целой машине не копится: два тарана по 6 оставляют
её целой, по книге (стр. 136) — 12 урона, минус 1 Комплекции и 2 в остатке. Отдача атакующему
(`ChaseService.Rules.cs:105-107`) считается без его собственного остатка и остаток не пишет.
Тесты: `VehicleTests.ApplyResult_CollisionBelowTen_CarryIsLost`,
`VehicleTests.ResolveVehicleCollision_Recoil_IgnoresAttackerCarry`.

**F-P08 · Порог Вождения при укрытии — в обход функции.** `ChaseService.Rules.cs:416`:
`actualRoll <= drivingThreshold`. Скрытность при этом идёт через `CalculateSuccessLevel`.
Расходится только на 01: по общей функции это всегда критический успех, а инлайн при пороге
Вождения 0 (навык 0, или до 4 при чрезвычайной сложности) его проваливает. На 96–100 итог
совпадает: бросок выше порога инлайн и так проваливает, а 100 — крах Скрытности. Тест:
`OptionalRulesTests.ResolveHide_InVehicle_CriticalFailsDrivingWithZeroThreshold`.

**F-P09 · Встречную ЛВК в погоне нельзя вписать — по коду.** Сервис броски принимает
(`ChaseService.Rules.cs:641`, это покрыто `TurnOrderTests.ResolveDexterityTie_*`), но кнопка
«Бросить встречную ЛВК» (`Components/ChaseOptionalRules.razor:209-210`) всегда передаёт
`null, null` — Хранитель не может вписать броски игроков. Тестом не воспроизводится: это разметка.
Вторая половина пункта 9 AUDIT — 1d6 раундов починки оружия — живёт в бою
(`CombatService.cs:1040`), а не в погоне.

**F-P10 · Журнал смены способа передвижения врёт про СКО.** `ChaseService.Rules.cs:510-514`
берёт СКО из статблока («полёт 20»), если Хранитель её не вписал, но текст записи
(`:530-532`) смотрит на вписанное значение и сообщает «своей СКО нет — половина обычной».
По книге (стр. 141) и по самому коду своя СКО важнее половины. Тест:
`ParticipantTests.ChangeMovementMode_StatBlockSpeed_LogSaysHalf`.

**F-P11 · Предел привыкания считает «1д6», бросок — нет.** `Bestiary/Services/SanityLossFormula.cs:46`
понимает `[dд]` и для «1/1д6» даёт предел 6, а потеря рассудка бросается
`CombatService.RollDiceFormula` / `MaximizeDiceFormula` (`CombatService.cs:1470-1472`) — для «1д6»
это 0, и при провале, и при крахе. Книга (стр. 167): предел — максимум провальной части, та же
формула, что бросается. Тест: `SanityLossFormulaTests.MaxLoss_CyrillicDice_CountsButRollIsZero`.

**F-P12 · Разборщик урона и бросок формулы понимают разное.** `Utilities/Services/DamageFormulaParser.cs:23-28`
против `CombatService.RollDiceFormula` (`CombatService.cs:325`): голое число «5» и «1 d 6» с
пробелами бросок понимает (5 и 1d6), разборщик оставляет неразобранными; русскую «д» — наоборот.
Одна и та же строка урона даёт разный итог в зависимости от того, разобрана ли она в
`DamageInfo`. Это частный случай «формулы костей разбираются в пяти местах» (AUDIT, «Кости и
уровни успеха»). Тесты: `DamageFormulaParserTests.Parse_DisagreesWithRollDiceFormula`,
`DamageFormulaParserTests.Parse_CyrillicDice_RollsThroughExpression`.

**F-P13 · «Шок» считается разобранным.** `Utilities/Services/DamageFormulaParser.cs:178-192`:
известный эффект сам по себе делает формулу разобранной, поэтому «Шок» даёт `IsParsed = true`
с пустым списком костей. Комментарий шага 4 (`:86`) и описание `WeaponStatsParser.cs:12-13`
обещают, что такая строка остаётся текстом. Противоречие внутри кода, не книги. Тест:
`DamageFormulaParserTests.Parse_EffectOnly_IsParsedDespiteComment`.

Каталог оружия в файлах репозитория не лежит — только в базе. Все различные значения колонок
«Дальность» (28), «Атак» (17), «Боезапас» (33) и «Стоимость» (75) вместе с результатом разбора
сохранились в удалённой миграции `WeaponStatsBackfill` (коммит `9a4e0d7`); по ним идут
`WeaponStatsParserTests.*_EveryCatalogValue_MatchesBackfill` — текущий разборщик даёт ровно то
же, что записано в базу. Колонок «Урон» и «Осечка» целиком в файлах нет: осечка проверена по
порогам из миграции приведения каталога (`8e3de69`), урон — по формам из комментариев и миграций.
