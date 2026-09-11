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
  `GetLifestyle` — раздел «Достаток» (стр. 44): жильё и транспорт, положенные Средствам.
- `InvestigatorCreationRules` (static) — **единственное** место, где живут шаги 1–2 главы 3
  (стр. 28–31, 45–46): формулы бросков характеристик, таблица возрастных модификаторов
  (`AgeBands`), проверка улучшения ОБР, готовые наборы блиц-метода и пределы «покупки»
  характеристик. Вторичные атрибуты остаются за `DerivedAttributeRules`.
- `BiographyTables` (static) — списки 1d10 для шага 4 (стр. 40–43) и набор слов «Описание».
- `OccupationSkillResolver` (static) — раскладывает профессию на слоты навыков (стр. 31, 38–39):
  названный навык, «любая специализация», выбор из перечисленного книгой списка, социальный слот,
  «ещё один любой». Там же `RequiredSkillCount` (восемь) и `ProfessionalSkillCount` — проверка
  «ровно восемь профессиональных навыков плюс Средства», её показывает `/occupations`.
- `InvestigatorFactory(SkillService)` — собирает лист из `InvestigatorDraft`: характеристики,
  навыки (включая добавленные специализации), деньги. Считает не сам, а через `DerivedAttributeRules`
  и `FinanceRules`.
- `Dice` (static) — 1d100, NdM и бросок с бонусной костью (меньший из двух десятков).
- `WoundRules` (static) — порог серьёзной раны (≥ половины максимума ПЗ) и вывод состояния
  «без сознания» / «при смерти» из нуля ПЗ.
- `SpecializationRules` (static) — бонус +10 смежным специализациям. Список навыков, где
  специализации делятся прогрессом, закрытый (Ближний бой, Стрельба, Языки, Выживание) —
  книга прямо противопоставляет им Науку, так что вешать бонус на любую группу нельзя.
- `OccupationService(dbContextFactory, IMemoryCache, logger)` — occupation catalog (skill point formulas, tags).
  `SyncWithRulebookAsync` — апсерт по имени из `Occupation.GetDefaultOccupations()`, за кнопкой
  «Синхронизировать с правилами» на `/occupations`. Это **единственный** способ доставить книжные
  данные в живую базу: миграции нигде не применяются автоматически, а доступ к базе на чтение.
  Ничего не удаляет — профессии, заведённые Хранителем сверх списка, остаются нетронутыми.
  Переименования книги держит `RenamedByRulebook` («Детектив» → «Детектив полиции»); без него
  апсерт по имени завёл бы вторую строку и оставил старую.

## Key Models
- `CharacterStorageDto.Kind` (`CharacterKind`: `PlayerCharacter` / `Pregen` / `Npc`) — **единственный**
  признак вида персонажа, обычная колонка. Не выводить вид из статуса, из набора внешних ключей или
  из полей внутри JSONB: ровно так эта модель и запуталась до упрощения.
- `Character` — composed of `PersonalInfo`, `Characteristics` (STR/DEX/CON/etc with `.Regular`/`.Half`/`.Fifth`), `DerivedAttributes` (HP/MP/Sanity/Luck as `AttributeWithMaxValue`), `Skills` (`SkillsModel` → `SkillGroup[]` → `Skill[]`, each with `.Regular`/`.Half`/`.Fifth`), `State` (`CharacterState` — IsUnconscious, HasSeriousInjury, IsDying, etc.), `Weapons` (`List<Weapon>`), `Spells` (`List<Spell>`), plus `BiographyInfo`, `Equipment`/`EquipmentItem`, `Finances`, `InsanityCondition`.
- `Occupation : BaseDataBaseEntity, INamedEntity`. Навыки лежат в трёх полях: `OccupationSkills`
  (jsonb `List<string>` — названные книгой навыки), `SkillChoices` (jsonb
  `List<OccupationSkillChoice>` — «выбрать N из перечисленных») и счётчики `SocialSkillSlots` /
  `FreeSkillSlots`. `SkillChoices` — отдельная колонка именно потому, что `OccupationSkills`
  остался плоским списком строк и старые строки обязаны читаться дальше.
- `CharacterGenerationLog` / `GenerationLogEntry` — audit trail of random-generation rolls.
- `InvestigatorDraft` — состояние помощника создания (см. ниже). Плоский POCO: он переживает
  паузу circuit как `[PersistentState]`, поэтому ни EF-сущностей, ни циклов в нём быть не должно.
- `CharacteristicKey` / `CharacteristicInfo` / `AgeBand` — восемь характеристик и строка таблицы
  возрастных модификаторов.

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

## Помощник создания сыщика

`InvestigatorWizardPage` (`/character/wizard`) ведёт игрока по пяти шагам главы 3 плюс выбор
способа и сводка; шаги лежат в `Components/Wizard/`. Страница создаёт лист сама
(`CreateCharacterAsync`) и уходит на него — в `CharacterPage` игрок после помощника не возвращается.

- **Маршрут именно `/character/wizard`, а не `/character/create/wizard`.** У `CharacterPage` есть
  `@page "/character/create/{Kind}"` со строковым параметром: он перехватил бы любой третий сегмент.
- Владельца помощник берёт из query (`campaignId`, `scenarioId`, `kind`) — теми же правилами, что и
  `CharacterPage`: НПС попадает в сценарий связью, преген — полем `ScenarioId`.
- **Любой бросок можно не бросать, а вписать.** За столом кости настоящие, поэтому у каждого броска
  (характеристики, набор варианта 3, проверка ОБР, Удача, 1d10 варианта 6) рядом с кнопкой есть поле
  ввода. Добавляя в помощник новый бросок, оставляй такую же пару — это требование, а не удобство.
- Слоты профессии (`OccupationSkillResolver.BuildSlots`) собирает **страница**, а шаг только
  показывает: выбор игрока хранится в `Draft.SlotChoices` по индексу слота, и шаг «Навыки» читает
  тот же список. Разъедутся индексы — навыки профессии уедут не туда.
- **Имена навыков в `Occupation.OccupationSkills` пишутся ровно как в справочнике навыков**
  («Язык, иностранный», не «Языки (иностр.)»). `OccupationSkillResolver.Aliases` остался только
  ради справочников, сохранённых до этого приведения, — новые данные туда добавлять не нужно.
  Навык, который не нашёлся, превращается в слот `Unresolved`, и игрок выбирает замену руками;
  у книжных профессий таких слотов быть не должно.
- Навыки с широким спектром (Искусство/ремесло, Наука, Ближний бой, Стрельба, Выживание, Иностранный
  язык) в справочнике — родители, и на лист не попадают: `BuildGroupsFromSkills` их отфильтровывает.
  Поэтому слот такого навыка — выбор специализации, а своя («Иностранный язык (латынь)») кладётся в
  `Draft.AddedSpecializations` и добавляется на лист в `InvestigatorFactory.BuildSkillList`.
- Блиц-метод (вариант 5) — единственный, который меняет ещё и шаг «Навыки»: вместо бюджета очков он
  раздаёт девять готовых значений. Хранится это в `Draft.BlitzValues`, из которых пересчитываются
  обычные `OccupationPoints`; навык, чья база уже выше выбранного значения, остаётся на базе.
- Черновик помощника переживает паузу circuit (`PersistedDraft`). Всё, что должно пережить уход на
  другую вкладку, кладётся в `InvestigatorDraft`, а не в приватное поле шага.
- **Специализацию, которую книга называет прямо, слот отдаёт без выбора.** «Язык, иностранный
  (латынь)» у Врача, «Искусство/ремесло (черчение)» у Инженера — таких навыков в справочнике нет,
  и резолвер выдаёт `Fixed` с заполненным `ParentSkillName`. На лист они попадают через
  `OccupationSkillResolver.FixedSpecializations`, который `RebuildSlots` кладёт в
  `Draft.AddedSpecializations`. Забыть это — значит молча потерять профессиональный навык.
- Слот «выбрать N из перечисленных» (`SkillChoices`) разворачивает варианты-родители в их
  специализации: «четыре специализации из Ближний бой / Взлом / … / Стрельба» у Преступника даёт
  один список со всеми видами ближнего боя и стрельбы плюс пункты «Другая специализация: …».
  Соседние слоты одной группы делят пул — уже выбранное из списка убирается, иначе повтор просто
  сгорел бы: одинаковые имена схлопываются в один профессиональный навык.
- Справочник профессий в базе приведён к списку «Примеры занятий» (стр. 37–39) — все 28 книжных
  профессий, у каждой ровно восемь профессиональных навыков плюс Средства. Помощник показывает то,
  что лежит в `Occupations`; править это можно и руками на `/occupations`, а вернуть книжное
  состояние — кнопкой «Синхронизировать с правилами».
- **Археолог, Бухгалтер и Механик — не из русских «Примеров занятий».** Их в главе 3 нет вообще
  (проверено по всем файлам `X:\Knowledge\CallOfCthulhu`: «Пункты проф. навыков» встречается ровно
  28 раз). Достались приложению от самодельного списка имён в самом первом коммите
  (`1b31a90`, `CharacterGenerationService.cs` — там же были «Автор», «Актер», «Атлет», «Бармен»,
  «Ботаник»: калька с английского) и потому долго жили с семью навыками вместо восьми. Состав
  выправлен по англоязычным правилам, где они есть: Archaeologist — Roll20-компендиум CoC 7e,
  Accountant и Mechanic (and Skilled Trades) — Investigator Handbook. Теперь они в
  `GetDefaultOccupations()` наравне с книжными, и синхронизация держит их в порядке.
  Заводя ещё одну «не из книги», клади её в тот же блок с пометкой источника — иначе следующий
  читатель решит, что она книжная.
- Генератор (`CharacterGenerationService`) раскладывает профессию **тем же** резолвером и делает
  выбор в слотах случайно (взвешенно по `Occupation.Tags`). Не заводить там второй разбор
  `OccupationSkills`: именно из-за него генератор раньше молча терял «Стрельбу» и «Науку» —
  это родители, на лист они не попадают.

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
