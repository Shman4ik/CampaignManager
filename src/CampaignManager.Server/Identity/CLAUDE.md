# Identity — вход и пользователи

Вход через Auth0, строки `cm.users`, `GET /api/v1/me`. Права — соседний модуль
[Access](../Access/CLAUDE.md). Тенант, домены, Google-клиент и коннекшены описаны в корневом
`CLAUDE.md`, «Authentication (Auth0)» — здесь только то, что относится к коду 2.0.

## Устройство

- **Две схемы под одной политикой.** Схема по умолчанию `CampaignManager` — policy scheme: запрос
  с `Authorization: Bearer …` уходит в JWT (мобильное приложение), всё остальное — в куку
  `.CampaignManager.Auth` (веб). Challenge той же схемы: API получает **401**, страница — редирект
  на `/account/login?returnUrl=…` и оттуда сразу на страницу Auth0 (своей витрины гостя с «Войти» нет, главная — тоже
  `[Authorize]`; открыты только `/about`, `/legal`, `/login`, `/dev/*`, ошибки); нет прав — всегда **403** (страница сама рисует «Нет доступа»).
- **Вход веба** — OIDC code flow, как в v1: `ResponseMode = Query` (код GET-ом: Lax-куки корреляции
  и nonce на кросс-сайтовый `form_post` не уедут), `MapInboundClaims = false`, userinfo не спрашиваем.
  `/account/login?returnUrl=…` всегда ведёт на страницу Auth0 (`ui_locales=ru`): Google, passkey и
  почту с паролем предлагает она сама, приложение её не обходит — `connection` ставит только автовход.
  `login_hint` — почта прошлого входа, если это была учётка с паролем/passkey (почта Google в поле
  повела бы к паролю). Оба параметра кладёт `OnRedirectToIdentityProvider`; `prompt` не шлём.
  Страница входа (шаблон с картинкой, тема, passkey) настраивается в тенанте — `tools/auth0/`.
- **Выход** — `/account/logout`: своя кука, кука прошлого входа и сессия Auth0 (`/oidc/logout` с
  `client_id` — без неё следующий вход молча пускает под той же учёткой). Переход с чужого сайта
  (`Sec-Fetch-Site: cross-site`) — 400. Адрес возврата — только свой хост (`ReturnUrl.Normalize`:
  `//evil`, `/\evil`, `/%5Cevil` — на `/`).
- **Адрес возврата — один, `ReturnUrl.Normalize`,** у тестового входа, входа и автовхода через Auth0 (`RedirectUri`
  в state) и выхода. Он сразу годен для заголовка `Location`: не-ASCII, управляющие символы и пробел — в `%XX`
  UTF-8, уже закодированное — как есть (`/skills?q=меч` → `/skills?q=%D0%BC%D0%B5%D1%87`). Сырую кириллицу
  Kestrel в заголовок не пускает — был 500 на `/dev/login` и после колбэков Auth0.
- **Сессия** — кука `.CampaignManager.Auth` на 30 дней со скользящим продлением (`IsPersistent` у
  входа), не сессионная: закрытый браузер вход не теряет.
- **Автовход** (`AutoLogin`) — перенесён из v1: кука `.CampaignManager.LastLogin`
  (способ и почта, год), одна попытка на 10 минут (`.CampaignManager.AutoLogin`, `AttemptLifetime`), только
  Google. Метка — со сроком, а не до закрытия браузера: Chrome с «Продолжить с того же места» держит
  сессионные куки неделями, и одна неудачная попытка выключала автовход в браузере насовсем. Срабатывает на загрузке документа (`Sec-Fetch-Dest: document`), не на API и не на
  `/account`, `/signin-oidc`, `/signout-callback-oidc`. В WebAssembly с сервера грузится только
  первая страница — ровно там он и нужен.
- **`email_verified` обязателен**, белый список (`Authorization:AllowedEmails`/`AllowedDomains`)
  и первые администраторы (`Authorization:AdminEmails`) — те же ключи конфигурации, что в v1.
  Отказ по нашим правилам → `/login?authStatus=accessDenied`, сбой Auth0 или «Decline» на
  согласии → `?authStatus=failed`.
- **Пользователь заводится при входе** (`UserDirectory.SignInAsync`): поиск по `auth0_sub`, затем по
  почте (`citext`, без `ToLower`) — так перенесённый из v1 человек (sub пуст) получает свою строку и
  привязывается. Тот же человек другим способом входа (другой sub, та же подтверждённая почта) —
  та же строка, привязка остаётся за первым sub.
- **В куке нет роли.** Claims куки: `cm_uid` (id в `cm.users`), `NameIdentifier` (sub), `Email`,
  `Name` — последние три те же, что у кук v1, поэтому после переключения куки v1 читаются: у них
  нет `cm_uid`, и `UserDirectory.ResolveAsync` находит человека по sub, затем по почте. Роль на
  каждый запрос читает `CurrentUser` (один запрос к базе) — без `UserClaimsCache` и
  `IClaimsTransformation` v1, смена роли видна со следующего запроса.
- **Data Protection** — ключи в `cm.data_protection_keys`, имя приложения `CampaignManager`, как в
  v1: T1.3 копирует ключи, и куки входа переживают переключение. На localhost v1 и v2 делят куку
  (один хост и порт), но ключи у них пока разные — кука v1 для v2 просто анонимна. Исключение —
  v2 в Development с известным портом: его кука `.CampaignManager.Auth.8080` (см. «Порт в именах кук»).

## Тестовый вход (только Development)

`DevLogin` — вход без Auth0 для проверки страниц под ролью: dev-приложение Auth0 пускает только на
`https://localhost:8080` и на localhost всегда спрашивает согласие, а агенты его не принимают.

```
GET /dev/login?as=player|keeper|admin[&email=…][&returnUrl=…]
https://localhost:8086/dev/login?as=keeper&returnUrl=/scenarios   ← так агент входит в браузере
```

- **Граница — маппинг.** `MapIdentityApi` маппит `/dev/login` только при `IsDevelopment()`: в
  `Testing`, `Production`, `Beta` адреса нет вовсе (404, а не отказ). Кнопки «Войти как …» на `/login`
  рисуются по `UiEnvironment.IsDevelopment` — это лишь подсказка, защита на сервере. Держит
  `DevLoginTests` (404 вне Development, обычный вход не меняется, `/api/v1/me` под каждой ролью).
- **Пользователь — тот же код, что у Auth0:** `UserDirectory.DevSignInAsync` зовёт `SignInAsync`
  (поиск по sub, затем по почте, белый список в силе, `last_login_at`) и ставит роль из `as`. sub —
  `dev|<почта>`, почта по умолчанию `dev-<роль>@cm.test`, имя «Тестовый Хранитель» и т. п. С `email=`
  входит этот адрес, и его роль меняется на `as` — в базе Development это нормально.
- **Кука — та же** (`IdentityModule.CreateSessionPrincipal`, общий с `OnTokenValidated`) плюс claim
  `cm_dev`. По нему выход (`/account/logout`) гасит только куку, без `/oidc/logout`, а автовход не
  запоминает такую сессию как «вход через Google».
- **Development без настроек Auth0 стартует:** схем OIDC и JWT нет, автовход не подключается,
  `/account/login` уводит на `/login?authStatus=unavailable`. Вне Development отсутствие настроек
  по-прежнему роняет старт.
- Белый список (`Authorization:AllowedEmails/AllowedDomains`), если он задан, тестовых пользователей
  тоже не пустит (`authStatus=accessDenied`) — добавить туда `cm.test`.

### Порт в именах кук (только Development)

Агенты запускают несколько серверов на `localhost` (8083, 8084, …), а куки браузер делит по хосту, не
по порту: без мер вход на одном порту перезаписывал куку соседа, сессии выбивали друг друга, а в логе
сыпались ошибки расшифровки (кука чужого сервера, чужие ключи Data Protection).

- **Все имена — в `AppCookies`** (синглтон): вход, `LastLogin`, метка автовхода, префиксы корреляции
  и nonce OIDC; имя antiforgery задаёт ASP.NET Core (с хешем приложения), к нему порт дописывает
  `PostConfigure<AntiforgeryOptions>`. В Development имя — `.CampaignManager.Auth.8083`, префиксы —
  `.CampaignManager.Nonce.8083.`, `.CampaignManager.Correlation.8083.` (хвост обработчика не сливается
  с портом). Новая кука приложения — имя только через `AppCookies`, не строкой.
- **Вне Development `AppCookies.Port = null` и имена прежние**, даже если адрес задан: другое имя куки
  входа на проде и beta выбило бы всех. Держит `AppCookiesTests` (Testing/Production/Beta — без
  суффикса, Development на двух портах — разные имена у всех кук) и
  `DevLoginTests.Dev_logins_on_two_ports_do_not_overwrite_each_other` (обе сессии живы при общей «банке» кук).
- **Порт — из конфигурации адресов** при сборке сервисов (`AppCookies.FindPort`): `urls` (`--urls`,
  `ASPNETCORE_URLS`, `applicationUrl` из `launchSettings.json`; из нескольких — первый https), затем
  `Kestrel:Endpoints:*:Url`, затем `https_ports`/`http_ports`. Не из `IServerAddressesFeature`:
  antiforgery читает свои опции при сборке конвейера, ещё до старта Kestrel, — реальных адресов в фиче
  тогда нет, только скопированные из той же конфигурации. И не из запроса: имена кук — опции, одни на
  приложение, а за прокси порт в `Host` не тот, что слушает сервер. Порт 0 или ни одного адреса — имена
  без суффикса; сервер пишет выбранное имя (или предупреждение) в лог при старте.

## JWT для мобильного приложения

`Authentication:Auth0:Audience` — идентификатор API в тенанте, `https://cthulhu.dmnet.dev/api` (API «CampaignManager
API», заведено 2026-10-05 для токенов агентов). **Без него ни один токен не проходит** (проверка audience). Access token Auth0 почты не несёт;
её и `email_verified` (и, по желанию, `name`) кладёт Action тенанта под пространство имён
`https://cthulhu.dmnet.dev/` (`IdentityModule.ClaimsNamespace`). Токен без них годится только тому,
кто уже входил через веб (поиск по sub). Первый запрос с токеном и подтверждённой почтой — это и есть
вход: `ResolveAsync` заводит или привязывает пользователя, белый список проверяется на каждом токене.

## Токены агентов (M2M)

Агенты (Claude и скрипты) ходят в API без браузера: токен Auth0 **client credentials** приложения M2M
«CampaignManager agents», та же схема JWT. Код — `MachineAccess`.

- **Узнаётся по `gty: client-credentials`** в `OnTokenValidated`. Приложение (`azp`) должно быть в
  `Authorization:MachineClients` (`[{ "ClientId": "…", "ActAs": "почта" }]`, env —
  `Authorization__MachineClients__0__ClientId`/`__ActAs`); нет — токен отклонён (401). Сессия: claims `cm_machine`
  (client_id), `Email` = `ActAs`, `NameIdentifier` = sub токена, по `cm_scope` на каждый scope.
- **От имени человека, без входа.** `ResolveAsync` для такой сессии ищет только по почте `ActAs`: строку не заводит, sub
  не привязывает (у перенесённого из v1 sub так и остаётся пустым). Роль и права — его, автор импорта — он.
- **Запрет по умолчанию.** `UseMachineScopes` стоит сразу за `UseAuthentication`: токен агента проходит только на
  эндпоинт с метаданными `AllowMachine(scope)` и нужным scope в токене, иначе 403 — и API, и страницы. Открыто сейчас:
  `/api/v1/me` (любой токен агента), всё в модуле сценариев, кроме прохождений и броней (`scenarios:write`), загрузка,
  внешний адрес и содержимое файла (`files:write`), список справочника с картинками и обложка записи `PUT …/{id}/cover`
  (`catalogs:images`, 2026-10-09: скрипт `*-art` сам ставит рисунки; правка полей справочника токену закрыта). Scope'ы —
  `Contracts/Identity/MachineScopes`, они же — scope'ы API в тенанте. Новый адрес для агентов — `.AllowMachine(MachineScopes.…)`
  в его модуле и тест (`MachineAccessApiTests`; у справочников — `CatalogsApiTests`, им нужна своя база).
- **Тенант:** API `https://cthulhu.dmnet.dev/api` (scope'ы `scenarios:write`, `files:write`, `catalogs:images`, токен живёт
  24 ч), приложение M2M «CampaignManager agents» с грантом на все три. Секрет — только у владельца (не в репозитории и не в SealedSecret:
  серверу он не нужен); client_id и `ActAs` — в `deployment.yaml` гитопса открытым текстом.
- **Токен** — с кастомного домена входа (с канонического `cthulhu-dmnet.eu.auth0.com` в `iss` будет он, и сервер
  отклонит токен — 401); секрет из переменной окружения, не печатать:

  ```bash
  curl -s https://auth.cthulhu.dmnet.dev/oauth/token -H 'content-type: application/json' \
    -d "{\"grant_type\":\"client_credentials\",\"client_id\":\"$CM_AGENT_CLIENT_ID\",\"client_secret\":\"$CM_AGENT_CLIENT_SECRET\",\"audience\":\"https://cthulhu.dmnet.dev/api\"}"
  ```

  Потом `Authorization: Bearer <access_token>`: `GET /api/v1/me` — от чьего имени; импорт —
  `POST /api/v1/scenarios/import?dryRun=true`, замена своего — `PUT /api/v1/scenarios/{id}/import`
  (`Scenarios/CLAUDE.md`, «Импорт и экспорт файлом»). Токен кэшировать до `expires_in`: у тенанта квота на выдачу M2M.
- Тесты — `MachinePrincipalTests` (разбор токена) и `MachineAccessApiTests` (`TestAuth.AsMachine`: тот же
  `CreatePrincipal`, допущен клиент из заголовка): от чьего имени, без привязки и новых строк, 403 вне scope'ов.

Заводит API и Action владелец (решение по тенанту); код к ним готов.

## Клиент (WebAssembly)

- `Web.Client/Identity/MeAuthenticationStateProvider` — состояние из `GET /api/v1/me`, один раз на
  загрузку; 401 — аноним (в консоли браузера это видно как ошибка 401 — так и задумано). Нет ответа
  или 5xx (спящая база Neon, выкатка) — два повтора через 1 и 3 с, и только потом аноним: иначе
  вошедший видел главную гостя до ручной перезагрузки. Админ
  получает обе роли, `Admin` и `Keeper`.
- Вход и выход — `NavigateTo(…, forceLoad: true)` на серверные адреса: клиентский роутер их не знает.
- `UI/Identity`: `/login` — экран ошибок входа (`authStatus`), тестовый вход и запасной путь с кнопкой «Войти»
  (`LoginOptions`); `RedirectToLogin` — клиентский переход на `[Authorize]` без сессии сразу уводит в Auth0, а повторный
  в течение минуты (сессия Auth0 жива, а `/me` отвечает «не вошёл» — петля) — на `/login`; `UserMenu` —
  подвал рельса и листа «Ещё» оболочки; пункты меню по ролям — `NavMenu.VisibleTo` (см. `UI/CLAUDE.md`).
- **Страницы — только `[Authorize(Policy = Policies.Keeper|Admin)]`, не `Roles`.** Сервер при прямой
  загрузке проверяет атрибут страницы своей политикой (роль из базы), а ролей в его куке нет:
  `Roles = "Keeper"` закрыл бы страницу и Хранителю.

## Тесты

`Server.Tests/Identity`: поток входа и выхода до редиректа на Auth0 (метаданные Auth0 подставлены
в `TestAuth`, сеть не нужна), автовход, `ReturnUrl`, `/me` и роль из базы, привязка по почте,
белый список, первые админы, закрытость каждого эндпоинта `/api/v1` (кроме `ping`). Сессия в тестах
— заголовки `X-Test-UserId` / `X-Test-Sub` + `X-Test-Email` (`TestAuth`); ключи Data Protection —
эфемерные. Обмен кода на токен и согласие Auth0 проверяет человек. Тестовый вход — `DevLoginTests`
(сервер в Development со своей базой и `EnvironmentApp` для прочих окружений).
