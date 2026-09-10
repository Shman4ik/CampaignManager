# Characters Feature

Player character sheets for Call of Cthulhu 7e, persisted as JSONB via `CharacterStorageDto` (see `CampaignManager.Web/Model/CharacterStorageDto.cs`).

## Key Services
- `CharacterService(dbContextFactory, identityService, logger, IMemoryCache)` — CRUD.
  `CreateCharacterAsync` требует явный `CharacterKind` и ровно одного владельца
  (`campaignPlayerId` / `campaignId` / `scenarioId` — или ничего, тогда НПС попадает в общую
  библиотеку). Списки: `GetNpcsAsync`, `GetPregenTemplatesAsync`, `GetScenarioPregensAsync`.
- `CharacterGenerationService(SkillService skillService)` — stateless random-generation logic; no `DbContextFactory` (it doesn't persist anything itself). Финансы считает по эпохе: у 1920-х и современности разные столбцы таблицы II «Наличные и активы».
- `DerivedAttributeRules` (static) — **единственное** место, где живут формулы вторичных атрибутов
  (ПЗ, ПМ, Рассудок, потолок Удачи, СКО, Комплексия, БкУ, Уклонение). И генератор, и лист персонажа
  считают через него: `InitializeNewSheet` — для чистого/сгенерированного листа, `Recalculate` — после
  правки характеристики или возраста на странице. Не дублировать эти формулы на месте: именно из-за
  этого отредактированный вручную лист раньше расходился с правилами.
- `SanityRules` (static) — максимум Рассудка, пороги безумия, начисление навыка Мифов за
  связанное с ними безумие (`RecordMythosInsanity`).
- `DevelopmentPhaseRules` (static) — **единственное** место, где живёт фаза развития сыщиков
  (стр. 92–94, 164–167): проверки опыта по отмеченным навыкам, +2d6 Рассудка за навык,
  дошедший до 90%, восстановление Удачи, награда Хранителя, самолечение с ключевой связью,
  варианты «занятия и Средства» и снижение привыкания к ужасному. Модалка только показывает
  результат — считать проценты на месте нельзя.
- `FinanceRules` (static) — таблица II «Наличные и активы» (стр. 45) в одном месте: по ней
  считает деньги и `CharacterGenerationService`, и пересчёт Средств в фазе развития.
  Столбец выбирает `isModern` (эпоха названа и она не классическая — как при генерации).
- `Dice` (static) — 1d100, NdM и бросок с бонусной костью (меньший из двух десятков).
- `WoundRules` (static) — порог серьёзной раны (≥ половины максимума ПЗ) и вывод состояния
  «без сознания» / «при смерти» из нуля ПЗ.
- `SpecializationRules` (static) — бонус +10 смежным специализациям. Список навыков, где
  специализации делятся прогрессом, закрытый (Ближний бой, Стрельба, Языки, Выживание) —
  книга прямо противопоставляет им Науку, так что вешать бонус на любую группу нельзя.
- `OccupationService(dbContextFactory, IMemoryCache, logger)` — occupation catalog (skill point formulas, tags).

## Key Models
- `CharacterStorageDto.Kind` (`CharacterKind`: `PlayerCharacter` / `Pregen` / `Npc`) — **единственный**
  признак вида персонажа, обычная колонка. Не выводить вид из статуса, из набора внешних ключей или
  из полей внутри JSONB: ровно так эта модель и запуталась до упрощения.
- `Character` — composed of `PersonalInfo`, `Characteristics` (STR/DEX/CON/etc with `.Regular`/`.Half`/`.Fifth`), `DerivedAttributes` (HP/MP/Sanity/Luck as `AttributeWithMaxValue`), `Skills` (`SkillsModel` → `SkillGroup[]` → `Skill[]`, each with `.Regular`/`.Half`/`.Fifth`), `State` (`CharacterState` — IsUnconscious, HasSeriousInjury, IsDying, etc.), `Weapons` (`List<Weapon>`), `Spells` (`List<Spell>`), plus `BiographyInfo`, `Equipment`/`EquipmentItem`, `Finances`, `InsanityCondition`.
- `Occupation : BaseDataBaseEntity, INamedEntity`.
- `CharacterGenerationLog` / `GenerationLogEntry` — audit trail of random-generation rolls.

## Разметка листа

- Лист собран из `<CharacterSection>` — сворачиваемая секция с градиентной шапкой,
  иконкой, `aria-expanded` и `data-testid="section-header-{Id}"`. Раскрытие хранит
  страница (`_sectionVisibility`), компонент только показывает: `Expanded` + `OnToggle`.
  Слоты: `TitleSuffix` — управляющие элементы рядом с заголовком (статус персонажа,
  «Фаза развития»), `HeaderActions` — кнопки справа перед стрелкой; клики в обоих
  не сворачивают секцию. `Gradient` и `BodyClass` меняют вид (лог генерации — янтарный
  и со скроллом).
- **Блоки внутри секции своей карточки не заводят.** `UnifiedPersonalInfoCard`,
  `WeaponComponent`, `SpellComponent`, `EquipmentComponent`, `FinancesComponent` —
  просто `<section>` с заголовком `cm-section-title`; рамку и отступ даёт секция.
  Раньше каждый рисовал `bg-white shadow rounded-lg p-4`, и на белой секции получалась
  белая карточка на белой карточке, а в характеристиках — ещё и серая подложка третьим
  слоем. Если блок надо визуально отделить от соседнего в той же секции, секции ставят
  `BodyClass="p-4 cm-stack"` — линейка между соседями вместо вложенной карточки.
- `SkillGroupCard` — единственная настоящая карточка внутри секции: их много в
  masonry-сетке, и рамка (`border`, не тень) там несёт смысл.

## Notes
- Потолок Удачи всегда 99, а не стартовый бросок (стр. 93): начальное значение дальше нигде не
  используется, а Удача растёт в фазу развития. Старые листы правит `NormalizeLuckCap` при открытии.
- Два порога безумия считаются по разным окнам и хранятся в разных полях `CharacterState`:
  `LastSanityLoss` — потеря от одной причины (≥5 → проверка ИНТ, стр. 152),
  `SanityLossEpisode` — накопленная за игровой день (≥1/5 текущего Рассудка → бессрочное, стр. 153).
  Складывать их в один счётчик нельзя: два провала по 3 не дают проверку ИНТ.
  Имя `SanityLossEpisode` оставлено ради уже сохранённых JSONB-листов.
- Уклонение хранится дважды: навык (хозяин, туда вкладывают пункты) и `PersonalInfo.Dodge`,
  который читает боёвка (`Combat/Model/Combatant`). На листе поле «Укло.» только зеркалит навык;
  правка навыка синхронизирует его через `HandleSkillValueEdited`.
- Строки навыков размечены `@key="skill"`. Без него Blazor переиспользует поле ввода соседнего
  навыка, когда список перетасовывается (навык уехал из свёрнутой группы в «изменённые»),
  и показывает Хранителю чужое значение.
- Идентификатор строки и `Character.Id` внутри JSONB всегда равны. Их расхождение раньше приводило
  к тому, что сохранение листа создавало новую строку вместо обновления, поэтому и
  `CreateCharacterAsync`, и `CopyPregenToScenarioAsync` выставляют оба.
- Копия листа делается ровно в одном месте — `CopyPregenToScenarioAsync` (преген расходуется
  бронью). НПС в сценарий не копируется: там связь `ScenarioNpc`, см. `Scenarios/CLAUDE.md`.
- Фаза развития живёт в модалке `DevelopmentPhaseModal` (кнопка в шапке секции «Навыки»).
  Список отмеченных навыков она снимает **один раз на открытие**: пока Хранитель бросает кости,
  состав таблицы не должен меняться под руками. Отметки стираются кнопкой
  «Стереть отметки и завершить» либо аварийной «Снять отметки» в шаге 1 (с подтверждением на
  месте — вложенную модалку тут не заводим). Обе внутри модалки: отметка навыка нужна **только**
  этой фазе, и на самом листе кнопки сброса нет — именно поэтому её оттуда и убрали.
- Мифы Ктулху и Средства не отмечают галочкой (стр. 92) — `DevelopmentPhaseRules.CanBeChecked`,
  и `SkillGroupCard` вместо чекбокса рисует для них пустое место.
- `CharacterState.MythosHabituations` — привыкание к ужасному (стр. 167): накопленная потеря
  рассудка по видам тварей. Записи ведёт `MythosHabituationPanel` внутри `SanityPanel`;
  вид подтягивается из бестиария, предел — из `CreatureCharacteristics.SanityLoss` через
  `Bestiary/Services/SanityLossFormula`. Сама потеря рассудка списывается через колбэк
  в `SanityPanel`: пороги безумия считаются только там.
- `Characters/Model/Skill.cs` (a character's own skill *value*) is a different type from `Skills/Model/SkillModel.cs` (the master skill catalog) — don't confuse the two when searching for "Skill".
- Same pattern for `Weapon`/`Spell`: the character holds `List<Weapon>`/`List<Spell>` referencing the catalog types defined in the `Weapons`/`Spells` features.
- `Character.Weapons` — это **копии** каталожных записей, а не сами записи: у копии свой
  `Id` и обязательный `CatalogWeaponId` (у самодельного оружия Хранителя он `null`).
  Копию делает `WeaponFactory.CopyForCharacter`; отдать в лист каталожный экземпляр нельзя —
  он лежит в общем `IMemoryCache` внутри `WeaponService`, и правка патронов на листе
  меняла бы справочник у всех пользователей. См. `Weapons/CLAUDE.md`.
- Числа из оружия (ёмкость магазина, порог осечки, дальность, число атак) читаются через
  `WeaponStatsReader`, а не из строковых полей: старые копии в JSONB несут только текст.

## Authorization
- `CharacterService.CanAccessCharacterAsync(dbContext, campaignPlayerId, access)` is the single access rule for
  stored characters; every read and write path goes through it. A character bound to a `CampaignPlayer` belongs to
  that player and to the keeper of their campaign. An unbound row (`CampaignPlayerId is null` — NPC and pregen
  templates) is shared library content: anyone signed in may read it, only keepers may change it. Administrators
  bypass both rules.
- `GetCharacterByIdAsync` returns `null` both for "does not exist" and for "no access" on purpose — the caller must
  not be able to probe which characters exist.
- `ReservePregenAsync` deliberately does **not** use that helper: reserving a pregen is a player writing to an
  unbound row, and it is constrained by its own rules (`Kind = Pregen`, ещё не забронирован, привязан к сценарию).
- Use `identityService.GetCurrentUserEmailAsync()` in authorization paths, never the synchronous
  `GetCurrentUserEmail()` — the latter reads `HttpContext` and returns `null` during interactive rendering.

## Несохранённые правки листа
- `CharacterPage` — единственная страница со снимком `[PersistentState]` (`CharacterPage.State.cs`,
  свойство `PersistedDraft`). Circuit восстанавливает **только** помеченное этим атрибутом, а сама
  страница собирается заново, поэтому без снимка любая пауза (уход на вкладку, сон планшета,
  перезапуск сервера) молча перечитывала лист из базы и стирала несохранённые правки навыков.
- Черновик применяется в `LoadCharacterDataAsync` **после** загрузки из базы и только если
  `CharacterId` совпал: чужой черновик выбрасывается, иначе на экране окажется не тот лист.
  При восстановлении показывается уведомление — иначе Хранитель не отличит правку от базы.
- Добавляя на страницу новое состояние, которое обязано пережить паузу, клади его в
  `CharacterDraft`, а не в приватное поле. Всё остальное (открытые секции, модалки) паузу не
  переживает намеренно.
- Лист сохраняется сам (`CharacterPage.AutoSave.cs`): раз в `AutoSaveInterval` (3 с) страница
  считает SHA-слепок JSON персонажа и, если он разошёлся с сохранённым, делает `UpdateCharacterAsync`.
  Кнопка «Сохранить» осталась — ею создают новый лист и сохраняют немедленно.
  - **Это опрос, а не подписка, и по-другому не выйдет.** Поля правятся обычным `@bind` внутри
    дочерних компонентов (`WeaponComponent`, `EquipmentComponent`, `BiographyComponent`), которые
    родительской странице ничего не сообщают: ни события, ни даже лишнего рендера у неё нет.
    `EditContext.OnFieldChanged` тоже мимо — на листе нет ни одного `Input*`-компонента.
    Слепок по JSON ловит любую правку любого поля разом и не требует обвешивать колбэками каждый блок.
  - Слепок (`MarkSaved`) снимается с того, что пришло из базы, — **до** восстановления черновика и
    до `NormalizeLuckCap`. Значит и вернувшийся после паузы черновик, и правка потолка Удачи у
    старого листа уезжают в базу первым же тиком, а не ждут кнопки.
  - Автосохранение не создаёт новый лист: пока `CharacterStorageDto is null`, оно молчит —
    у нового листа ещё не выбран владелец (кампания/сценарий), это делают руками.
  - Ручное и автоматическое сохранение делят `_isBusy`, иначе два `UPDATE` одной строки идут внахлёст.
  - Уведомление о неудаче показывается один раз на серию: тик раз в три секунды иначе повесил бы
    Хранителю на экран несмываемую красную плашку. Состояние видно значком в шапке (`AutoSaveIndicator`).
