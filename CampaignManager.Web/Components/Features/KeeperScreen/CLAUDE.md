# KeeperScreen Feature

Ширма Хранителя: справочные таблицы из «Книги Хранителя», которые за столом нужны быстрее, чем
их найдёшь в книге. Видна только Хранителю и администратору.

## Где в интерфейсе
- **Кнопка «Ширма»** (`Components/KeeperScreenButton`) — в `Shared/PageHeader`, то есть в шапке
  любой страницы, и отдельно в своей шапке режима игры `ScenarioDetailPage`. Сама прячется от
  всех, кроме Хранителя/администратора (`IdentityService.IsKeeper`).
- **Выезжающая панель** (`Components/KeeperScreenHost`) — в `Layout/MainLayout`, после панели
  плеера. Встаёт справа поверх текущего экрана: бой, сценарий, лист остаются под ней и не
  перезагружаются. Закрывается кнопкой в своей шапке или тапом по затемнению.
- **Страница `/reference`** (`Pages/KeeperReferencePage`) — то же содержимое для закладки.
  Раздел — в адресе (`?block=checks|dice|push|luck|damage|firearms|sanity|group`).

## Как устроено
- Кнопка и панель — **разные интерактивные острова одного circuit** (кнопка бывает и на
  статичной странице, панель живёт в статичном лэйауте). Общий у них только scoped
  `Services/KeeperScreenState` (открыта ли, какой раздел, кампания групповой проверки) с событием
  `Changed` — так же, как `MusicPlaybackService` связывает страницы с панелью плеера.
- Паузу circuit состояние ширмы **не переживает намеренно** (`RegisterPersistentService` нет):
  после возобновления ширма закрыта. Постоянный адрес раздела — `/reference?block=…`.
- `Components/KeeperReference` — вкладки и один раздел; своей карточки нет, рамку даёт место
  (панель или `cm-card` страницы). Разделы — `Components/Blocks/*Block.razor`, групповая проверка —
  `Checks/Components/GroupCheckPanel`.
- Слои: панель 45/46 — над шапкой (30) и боковой панелью (40), под модалками (50) и панелью
  плеера (55). Ширма заканчивается над плеером правилом `.cm-music-bar ~ .ks-drawer`, поэтому
  `<KeeperScreenHost/>` обязан стоять в `MainLayout` **после** `<MusicPlayerBar/>` в том же
  `.page-with-sidebar`. У панели анимация без `fill-mode`: оставшийся `transform` сделал бы её
  containing block для `position: fixed` потомков.

## Данные: ни одной второй копии
- `Model/OtherDamageReference` (static) — **единственное** место таблицы III «Другие виды урона»
  (стр. 122): шесть строк тяжести (`Tiers`), урон от падения (`Falls`) и категории ядов
  (`Poisons`, стр. 126–127). Её же читает погоня (`Chase/Components/ChaseActionPanel`,
  `LocationEditor`), поэтому менять строки — значит менять и урон помех.
- `Model/SanityReference` — примеры потерь рассудка (стр. 153) и непроизвольные действия (стр. 152).
  Пороги безумия здесь **не** лежат: раздел «Рассудок» берёт их у `Characters/Services/SanityRules`.
- `Services/CombatModifierReference` — памятка по костям стрельбы и ближнего боя. **Своего списка
  модификаторов нет**: каждая строка — вопрос к `CombatService.CalculateAttackModifiers` с одним
  включённым условием, дальность — `GetRequiredLevelForRange`/`GetDifficultyName`. Своё здесь только
  «когда положено». Новый модификатор в бою → добавить сюда одну строку-«пробу», иначе памятка о нём
  промолчит.
- Раздел «Проверки» считает пороги через `CombatService.CalculateSuccessLevel`/`GetTargetNumber`,
  «Удача» — через `LuckRules`, «Рассудок» — через `SanityRules`, лимит Удачи —
  `DevelopmentPhaseRules.MaxLuck`. Числа в разметке не дублировать.
- Тексты — пересказ книги своими словами со ссылкой на страницу, а не цитаты.
