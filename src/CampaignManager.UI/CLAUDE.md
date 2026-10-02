# CampaignManager.UI — дизайн-система, UI-кит, оболочка

Страницы и компоненты 2.0 (Blazor WebAssembly, D1). Ходит только в `Contracts` и `Core`. Знание
перенесено из `docs/design-system.md`, `design-system.css` v1 и корневого `CLAUDE.md` (каркас
страницы, кнопки, Alert, iPad); обходы v1 (circuit, пререндер, `!`-утилиты) — нет. Все компоненты
вживую — `/dev/ui` (только Development), каждый новый компонент кита — туда же.

## Папки

| Папка | Что |
|---|---|
| `Styles/` | Tailwind v4: `theme.css` (токены), `base.css`, `components.css` (классы `cm-*`), `shell.css` (оболочка), `sheet.css` (лист сыщика) |
| `Shared/` | UI-кит: `Button`, `Badge`, `Alert`, `Field`, `Modal`, `DialogService`, `ToastService`, `DataTable`, `StringListEditor`, `AsyncContent`, `RollInput`, `DiceInput`, `Markdown`, `PageHeader`, `Tabs`, `FilterPanel`, `Pagination`, `EmptyState`, `LoadingIndicator` |
| `Layout/` | `MainLayout`, `NavMenu` (единый список пунктов), `NavRail`, `BottomNav`, `ConnectionIndicator` |
| `Identity/` | `/login` (в Development — ещё «Войти как …», тестовый вход), `RedirectToLogin`, `UserMenu` — подвал рельса и листа «Ещё» (вход — T1.4); `RoleLabels` — роли и статусы заявок по-русски; `IUserSession` — перечитать `/me` без перезагрузки |
| `Platform/` | `ApiActivity` — состояние связи и записи; `BrowserStorage` — `localStorage` для черновиков; `ApiErrors` — текст отказа API |
| `Pages/` | страницы; `Pages/Dev` — `/dev/ping`, `/dev/files` (T1.5, загрузка и отдача; сироты — в админке), `/dev/ui`, `/dev/checks` (T2.7) |
| `Campaigns/` | главная, кампании, участники, журнал — [CLAUDE.md](Campaigns/CLAUDE.md) |
| `Catalogs/` | справочники: общая `CatalogPage<T>`, страницы семи справочников, импорт и экспорт — [CLAUDE.md](Catalogs/CLAUDE.md) |
| `Profile/` | личный кабинет `/profile` — [CLAUDE.md](Profile/CLAUDE.md) |
| `Admin/` | `/admin/users`, `/admin/applications`, `/admin/files`, счётчик заявок `AdminBadges` — [CLAUDE.md](Admin/CLAUDE.md) |
| `Characters/` | лист сыщика `/character/{id}`: секции, автосохранение, рассудок, книги, фаза развития — [CLAUDE.md](Characters/CLAUDE.md) |
| `Checks/` | диалог проверки, групповая проверка — [Checks/CLAUDE.md](Checks/CLAUDE.md) |
| `KeeperScreen/` | ширма Хранителя: кнопка, панель, `/reference` — [KeeperScreen/CLAUDE.md](KeeperScreen/CLAUDE.md) |
| `Music/` | фонотека `/music`, плеер (кнопка в слоте шапки, панель внизу колонки, звук — модуль JS) — [Music/CLAUDE.md](Music/CLAUDE.md) |
| `wwwroot/` | свои шрифты (`fonts/`), Font Awesome 6.7.2 (`lib/fontawesome/`), `logo.svg`; `styles.css` — сборка, не в git |

Службы кита регистрирует `AddCampaignManagerUi(isDevelopment)` (зовёт `Web.Client`).

## Токены и Tailwind v4

- **Один источник** — `@theme` в `Styles/theme.css`. Из токена Tailwind делает и утилиту
  (`bg-primary-700`), и переменную (`--color-primary-700`); классы `cm-*` берут цвет из переменной.
  В v1 цвета статусов жили дважды (`tailwind.config.js` и `:root`) и расходились. Конфига
  `tailwind.config.js` в v4 нет.
- **Палитры заменяют стандартные целиком** (`--color-*: initial`): `primary` (графит — единственная
  нейтральная), `secondary` (тёплый камень), `accent` (сталь), `success`/`warning`/`error`, `white`,
  `black`, `page` (фон страницы). Перенося разметку v1:
  `gray-*`/`slate-*` → `primary-*` (в v1 два серых спорили на одном экране), `info-*` → `accent-*`
  (повторяла hex-в-hex), `red/green/amber/blue-*` → `error/success/warning/accent-*`. Несуществующий
  класс Tailwind просто не соберёт — проверяй экран, а не только сборку.
- Класс — **целым литералом** (`bg-error-100`), не `$"bg-{tone}-100"`: Tailwind ищет классы в
  исходниках текстом (`.razor` и `.cs` проекта, `.razor` сервера — `@source` в `tailwind.css`).
  Тоны компонентов — `Shared/Tone.cs` (`ToneClasses`).
- Переименования v3 → v4, которые встретятся при переносе: `shadow-sm` → `shadow-xs`, `shadow` →
  `shadow-sm`, `rounded` → `rounded-sm`, `rounded-sm` → `rounded-xs`, `outline-none` →
  `outline-hidden`, `ring` → `ring-3`, `flex-shrink-0` → `shrink-0`, `!mb-0` → `mb-0!` (но «!» теперь
  почти не нужен, см. ниже). Рамка по умолчанию в v4 — `currentColor`; в `base.css` она возвращена
  к `primary-200`.
- Сборка — MSBuild-таргет `Tailwind` в csproj: `npm ci` по `package-lock.json` (только когда он
  поменялся), затем `npx tailwindcss`. Нужен Node; первая сборка ставит `node_modules` (в git нет).
  Версия Tailwind — в `package.json`, точно. **На Windows таргет ставит `npm_config_os=win32`**:
  в `~/.npmrc` владельца `os=linux`, и npm ставил нативные модули Linux — сборка падала с
  «Cannot find module lightningcss.win32-x64-msvc.node».

## Слои и каскад

- Порядок слоёв: `theme → base → components → utilities`. Классы `cm-*` — в `components`, поэтому
  **утилита на элементе перебивает компонентный класс без «!»**: `class="mb-0"` у Alert, `w-20` у
  `cm-input`. В v1 `design-system.css` грузился после Tailwind — отсюда 148 `!`-утилит.
- **Стили вне слоя бьют все слои.** Изолированный CSS (`*.razor.css`) оборачивай в
  `@layer components { … }` — Razor это поддерживает (`Shared/RollInput.razor.css`); иначе
  утилиты на элементах компонента снова перестанут работать.
- Обратная ловушка: элемент, который `base`-правило должно прятать, не может нести утилиту
  `display` — `flex` из `utilities` перебьёт `display: none` из `base`. Так заглушка «Загрузка…»
  (`#app-loading`) не пряталась; её оформление целиком в `base.css`.
- Глобальные таблицы — только `Styles/*.css`. Шрифты (`wwwroot/fonts/fonts.css`) и Font Awesome
  подключены отдельными `<link>` в `Server/Components/App.razor`: в них относительные `url()`.

## Шрифты и иконки

- Свои, без CDN (в v1 — Google Fonts, cdnjs и `flowbite.min.css` поверх всего): Inter (интерфейс),
  Bitter (книжные заголовки `cm-title-serif`; замена Bookman Old Style, у которого нет webfont),
  JetBrains Mono (`cm-dice`, `font-mono`: кости, формулы). Вариативные woff2 Fontsource, кириллица и
  латиница, лицензии OFL рядом.
- Font Awesome Free **6.7.2**, а не 7: разметка v1 использует имена 6-й (`fa-edit`, `fa-trash-alt`
  и т. п. — алиасы там есть). Подключены solid, regular, brands. Иконка — `<i class="fa-solid fa-…">`,
  эмодзи вместо иконок нет.

## Каркас страницы

```razor
@page "/campaigns"

<PageHeader Title="Кампании">
    <Actions><Button Small="true" Icon="fa-plus" OnClick="…">Новая</Button></Actions>
</PageHeader>

<div class="cm-page">…</div>
```

- `PageHeader` есть у каждой страницы; он же ставит `<PageTitle>` (`DocumentTitle` — если вкладка
  должна называться иначе). Действия — в `Actions`, всегда `Small` (шапка 56px).
- **На телефоне (уже `md`) кнопки шапки с иконкой сами становятся значками 36×36**, подпись — в
  `sr-only` (правило у `.cm-topbar-actions` в `shell.css`): иначе «Добавить заклинание» рядом с
  «Музыкой» и «Ширмой» сжимала заголовок в ноль, а библиотека НПС раздвигала страницу вбок. Поэтому
  у кнопки шапки — `Icon` и подпись простым текстом; прятать подпись самому (`hidden md:inline`) не
  нужно — `hidden` к тому же снимает с кнопки имя. Подпись остаётся у кнопки без иконки («Назад») и
  со счётчиком (`cm-count-badge`) — таких в шапке не больше одной. Проверка — `scrollWidth` на 390×844
  под Хранителем (у него в шапке ещё «Музыка» и «Ширма»).
- **Кнопки модулей в шапке** (ширма, плеер) — через слоты: модуль рисует
  `<SectionContent SectionName="@PageHeaderSlots.KeeperScreen">` из своего компонента в
  `MainLayout`, шапка показывает его `SectionOutlet`. Шапка модулей не импортирует (в v1
  `PageHeader` тянул Music и KeeperScreen). У слота один владелец — новому модулю свой слот в
  `PageHeaderSlots`. `ShowModuleButtons="false"` — только на самой ширме.
- Внутри: `cm-section` + `cm-section-header` > `cm-section-title`; `cm-card` + `cm-card-header` /
  `-body` / `-footer`; `cm-meta`, `cm-text-muted`, `cm-grid-cards`.
- **Одна карточка, никогда две.** Блоки в карточке разделяет линейка (`cm-stack`,
  `cm-section-divided`), элементы списка — строки `border-t border-primary-200 first:border-t-0`.
  Компонент, который всегда стоит в карточке, своей не рисует и пишет об этом в комментарии
  (`DataTable`, `StringListEditor`). `EmptyState` сам карточка — ставится вместо списка.
- **Никогда `max-w-*` + `mx-auto` на содержимое страницы**: колонка `cm-main` — флекс, и `mx-auto`
  снимает растяжение — редкая страница схлопывается узким столбцом.

## Оболочка

- `NavMenu.Items` — **один список** на рельс планшета и нижнюю панель телефона (в v1 их было два,
  `MobileBottomNav` на iPad был мёртв). Новый раздел — одна строка там.
- Рельс — с `md` (768px): iPad в обеих ориентациях. Подпись под иконкой видна всегда, одной строкой
  11px — не длиннее «Заклинания», иначе `ShortLabel` (тест `Rail_labels_fit_one_line`). Развернуть
  рельс нельзя — на столе широкое меню только отнимало ширину.
- Телефон: на панели пункты с `OnPhoneBar` (не больше четырёх) и «Ещё» — лист со всем списком
  (`Modal` с `Placement="Sheet"`), закрывается сам при переходе. Состав панели («последний сыщик» v1
  и т. п.) — решение владельца: в M2.
- **Права на пункты** — `NavAudience` у пункта; `MainLayout` берёт принципал из каскадного
  `AuthenticationState` (`MeAuthenticationStateProvider`, `/api/v1/me`) и показывает
  `NavMenu.VisibleTo(user)`: гость — только «Главная», игрок — разделы под входом, Хранитель — свои,
  администратор (несёт и роль Keeper) — всё. Меню только прячет — защищает сервер (`AccessPolicy`).
  Роли сверяются по `nameof(UserRole.…)`. Счётчики на пунктах — `Badges` (адрес пункта → число); сейчас
  один — заявки на Хранителя (`Admin/AdminBadges`, наполняет `MainLayout` при входе администратора).
- Принципал меняется и без перезагрузки: `IUserSession.RefreshAsync()` перечитывает `/me` (новое имя из кабинета,
  снятая с себя роль администратора) — меню и `AuthorizeView` перерисуются сами.
- **Кабинет, «Войти», «Выйти» — не пункты `NavMenu`**, а `UserMenu` в слоте `Footer`: в рельсе —
  кабинет с инициалом и «Выйти» (без сессии — «Войти» с `returnUrl` текущей страницы), в листе
  «Ещё» — имя, роль, «Выйти» и кабинет. Вход и выход — серверные адреса, `forceLoad: true`.
- «Нет доступа» и «Загрузка…» `AuthorizeRouteView` (`Routes.razor`) — тоже на ките: `PageHeader` +
  `EmptyState`, `LoadingIndicator`.
- `ErrorBoundary` в `MainLayout` сбрасывается при переходе: ошибка одной страницы не запирает
  приложение. Плашка `#blazor-error-ui` — для ошибок вне страниц.

## Кнопки

Цвет несёт смысл (`ButtonVariant`): `Primary` — одно главное действие; `Secondary` — нейтральное;
`OutlinePrimary`/`OutlineError` — правка и удаление в строке; `Error` — необратимое подтверждение в
диалоге; `Success` — одобрить чужую заявку; `Ghost` — крестики и шевроны. Синей кнопки нет
(`cm-btn-info` v1 раскрашивал одно действие по-разному).

- `Button` рисует те же `cm-btn*`, что ручная разметка; `Href` — ссылка-кнопка. Свой `class`
  подмешивается (`w-full`). `Busy` — спиннер и выключенная кнопка на время запроса.
- Кнопка без текста — 44×44 (36×36 у `Small`) и **обязательный `AriaLabel`** (без него компонент
  бросает исключение). Голая иконка в строке — **только** в плотной таблице справочника и только
  карандаш (`fa-pen`) и корзина (`fa-trash-can`), `AriaLabel` с именем строки («Изменить: Кольт
  .45»); та же строка карточкой на телефоне — те же иконки. Остальное — с подписью.

## Сообщения

- **Только `<Alert Tone="…">`** — ошибка сохранения, правило книги, успех. Своих
  `<div class="bg-error-50">` не заводить (в v1 — семь тонов). `ShowIcon`, `Icon`, `Small`
  (плотные панели листа), `Actions`, `OnClose`. Свой нижний отступ; не нужен — `class="mb-0"`.
- **`ToastService`** — короткое сообщение о результате действия («Сохранено», «Не удалось
  удалить»); рисуется тем же Alert. Ошибка висит до закрытия, остальное уходит через 5 с. То, что
  должно стоять у формы (ошибки проверки, правило), — Alert на странице, не Toast.
- `<ValidationSummary>` классы игнорирует — оформление в `.validation-errors` (`components.css`),
  тоном Alert error. `ValidationMessage` — `.validation-message`.
- Тонированные панели правил боя и погоны (результат атаки, действие погони) — содержимое экрана с
  контролами, а не уведомления; их не переводили на Alert и в 2.0.

## Markdown

`<Markdown Text="…">` — хроника журнала, анонс ваншота, тексты сценариев; оформление `cm-markdown`.
HTML строит `MarkdownText` (Markdig): **сырой HTML выводится текстом** (`DisableHtml`), ссылки и картинки —
только `http(s)`, `mailto` и относительные, остальное (`javascript:`, `data:`) становится `#`. Пишет
Хранитель, читают игроки — поэтому без исключений. HtmlSanitizer v1 (AngleSharp) в WebAssembly не
берём: мегабайты ради того, что даёт `DisableHtml`.

## Черновики и ошибки API

- Несохранённая правка — черновик в `localStorage` через `BrowserStorage` (встроенные
  `localStorage.*` по JS-интеропу, своих скриптов нет). Хранилище бывает недоступно (приватный режим) —
  тогда черновика просто нет. Ключ — `cm.<что>:<id>`; стирать после успешной записи и при явном отказе.
  Пример — окно встречи журнала (`Campaigns/SessionEditorModal`).
- Текст отказа для пользователя — `ApiErrors.Describe(HttpRequestException)`: ProblemDetails как есть,
  401 — «сессия закончилась», без ответа — «нет связи», 5xx — общая фраза.

## Поля

- `<Field Label Hint Note Error>` — подпись, контрол, правило книги («(стр. 32)»), ошибка. Подпись
  обнимает контрол (`<label>`), id не нужен. Несколько контролов под одной подписью (флажки,
  `RollInput`, `StringListEditor`) — `Group="true"` (`<fieldset>`). `Inline` — подпись слева.
- `cm-input` — во всю ширину; ширину задаёт `class` на `Field` или обёртка. Шрифт поля на телефоне
  16px: при меньшем Safari на iPhone увеличивает страницу при фокусе. `cm-input-sm` — тесные ячейки
  листа (модификатор к `cm-input`).
- Мелкий флажок в плотной строке — `<label class="cm-tap-target">`: касание 36×36, раскладка не
  растёт.
- `StringListEditor` отдаёт **новый список** на каждую правку (`@bind-Values`): документ получает
  новый экземпляр, правку на месте не заметили бы ни EF, ни сравнение черновика.

## Диалоги

- **Один диалог — `<Modal>`** (нативный `<dialog>`: фокус, Esc, подложка, верхний слой; прокрутку
  страницы блокирует `ModalWindow.razor.js`). Своих `fixed inset-0` не заводить. **Закрытый Modal
  ничего не рендерит.** Закрывает страница (`Open = false` в `OnClose`).
- `Placement`: `Center` (по умолчанию), `Sheet` — лист снизу (меню «Ещё»), `Drawer` — панель справа во всю
  высоту (ширма Хранителя: справочник поверх экрана, страница под ним остаётся).
- `Dismissible="false"` — подложка и Esc не закрывают (крестик остаётся): диалоги посреди игры, где
  случайное касание не должно ничего пропускать (проверка ВЫН умирающих). Chrome закрывает окно на
  втором Esc сам — JS открывает его обратно.
- Подтверждение — `await Dialogs.ConfirmAsync(new ConfirmRequest(…))` или `ConfirmDeleteAsync`:
  без флагов `_showDelete*` (в v1 — 41 в 9 файлах). Фокус по умолчанию на «Отмене».
- Подвал: `Secondary` «Отмена» слева, действие справа.

## Списки

- Порядок страницы-списка: `FilterPanel` → загрузка/пусто → список → `Pagination`. Загрузка, ошибка с
  «Повторить» и пусто — `AsyncContent` (без `@if (_isLoading)` и своих try/catch); `Key` — id из
  адреса. Любой обработчик фильтра сбрасывает страницу на первую.
- `FilterPanel`: одна строка с переносом, с `md` липнет под шапку (на телефоне несколько строк фильтров
  заняли бы треть экрана), «Сбросить» в конце; без подписей над
  полями (`placeholder`, `aria-label`, «Все типы»); поиск `min-w-48 flex-1`, селект
  `w-full sm:w-56 shrink-0`, блок на отдельную строку — `w-full order-last`.
- `DataTable<T>` + `DataColumn`: сортировка по заголовку, раскрытие строки (`RowDetail`), карточки
  на телефоне (`CardTemplate`), прокрутка вбок в своей обёртке. Сортирует **сама** (`SortBy` у
  колонки) или **снаружи** — постраничный список с сервера: `@bind-Sort` и `SortKey` колонки, порядок
  задаёт страница. Своей карточки не рисует.
  - `Page`/`PageSize` — страница **после** сортировки (сортировать одну страницу бессмысленно); `TableFrom`
    (`Md`/`Lg`/`Xl`) — с какой ширины таблица вместо карточек, порог — по числу колонок; `Scroll` — таблица
    свой скроллпорт с липкими шапкой и первой колонкой (`cm-table-scroll`); `CanExpand` — у строки без
    деталей нет шеврона; `OpenKey` — строка, раскрытая снаружи (`?open=`); `HeaderClass` у колонки — её
    ширина долей. `cm-clip` у ячейки — многоточие, раскрытая строка снимает обрезку.
  - Ячейки колонок таблица рисует раньше, чем `DataColumn` получат новые параметры: содержимое, которое
    зависит от внешнего состояния (фильтр прячет метку), отстаёт на рендер — такую таблицу пересоздают
    `@key` (так делает `CatalogPage`).
- `Tabs` ничего не хранит; открытая вкладка — в адресе (`?tab=`), чтобы на неё можно было дать
  ссылку.

## Бросок — `RollInput`

Любой бросок можно не бросать, а **вписать** с настоящих костей — требование, а не удобство (правило
листа v1). `RollInput` — поле «выпало» и кнопка «Бросить» (`IDiceRoller` из DI), уровень успеха —
только `Check.Evaluate` (Core), подписи — `RulesText`. Если успех ниже требуемой сложности —
«нужен трудный успех». В v1 было четыре реализации; новая проверка в бою, погоне, листе — этот
компонент. Тон и подпись уровня — `LevelText` (общий с диалогом проверки). Проверка навыка целиком
(сложность, кости, Удача, повтор, отметка) — не своя разметка вокруг `RollInput`, а `Checks/SkillCheckPanel`.
Сумма костей NdM (прирост 1d10, 2d6 Рассудка, 2d10 Средств) — `DiceInput`: та же пара «вписать / бросить», значение —
сумма; правило Core получает её через `EnteredDiceRoller.Total`.

## Связь вместо circuit

- Экран живёт в браузере (WebAssembly), обрыв связи его не сбрасывает; пользователю нужно знать
  одно — дошли ли правки. `ApiActivity` считает записи в пути и доступность сервера; наполняет его
  `ApiActivityHandler` в `Web.Client` на каждом `HttpClient` (`ConfigureHttpClientDefaults`), UI про
  HTTP-клиент не знает. `ConnectionIndicator` — плашка «Сохранение…» / «Сохранено» / «Нет связи»
  с «Проверить» (пинг).
- «Нет связи» — только когда ответа не было (`HttpRequestException` без ответа) или браузер offline.
  409, 500 — ответ: их показывает страница (Alert, Toast).

## JS

Только ES-модули через `IJSObjectReference`: `Компонент.razor.js` рядом с компонентом, импорт
`"./_content/CampaignManager.UI/<путь>/Компонент.razor.js"`. Модуль возвращает объект-хэндл с
`release()`/`dispose()`, компонент зовёт его в `DisposeAsync` и ловит `JSDisconnectedException`.
Без глобальных скриптов, `window.*`-функций и `eval` (в v1 ~90 из 141 строки `app.js` были мертвы).

## iPad и телефон

- Проверка — `/dev/ui` и страница в браузере: **1366×1024** (главная ориентация), **1024×1366**
  (у `lg:` граница ровно 1024 — трёхколоночные сетки здесь самые тесные), для страниц игрока —
  **390×844**. Сбросить вьюпорт после проверки.
- Тап-цели: `cm-btn-sm` (36px) — пол; `cm-btn` 44px; пункт меню 46px. Ничего не держится на
  `title=` — наведения на iPad нет. Широкое — в `overflow-x-auto` (`cm-table-wrap`), ряды кнопок —
  `flex-wrap`. Учитывай безопасную зону (`env(safe-area-inset-bottom)` у нижней панели).

## `/dev/*` и окружение

`/dev/ui` и ссылка на неё с главной видны только при `UiEnvironment.IsDevelopment`. Окружение
WebAssembly **запекается при сборке**: Debug (`dotnet run`) — Development, `dotnet publish` —
Production, от окружения сервера не зависит. Страницы `/dev/*` без `[Authorize]` и открыты гостю;
данные за ними всё равно под правами API (`/dev/files` без входа получит 401).

## Тесты

`tests/CampaignManager.UI.Tests` — bUnit: диалог (закрытый не рендерится, подтверждение), бросок
(вписанный, вне 1–100, бонусные кости, уровни), таблица (сортировка в себе и снаружи), редактор
списка, `AsyncContent`, `ToastService`, `ApiActivity`, правила меню и пункты по ролям, `UserMenu`
гостя и вошедшего (`AddAuthorization()` bUnit — вход Auth0 в браузере агента не пройти). Базовый класс `KitContext`
регистрирует кит как `Web.Client`; JS-интероп в свободном режиме. Вёрстку bUnit не проверит —
её смотрят в браузере.
