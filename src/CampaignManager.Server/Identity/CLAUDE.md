# Identity — вход и пользователи

Вход через Auth0, строки `cm.users`, `GET /api/v1/me`. Права — соседний модуль
[Access](../Access/CLAUDE.md). Тенант, домены, Google-клиент и коннекшены описаны в корневом
`CLAUDE.md`, «Authentication (Auth0)» — здесь только то, что относится к коду 2.0.

## Устройство

- **Две схемы под одной политикой.** Схема по умолчанию `CampaignManager` — policy scheme: запрос
  с `Authorization: Bearer …` уходит в JWT (мобильное приложение), всё остальное — в куку
  `.CampaignManager.Auth` (веб). Challenge той же схемы: API получает **401**, страница — редирект
  на `/login?returnUrl=…`; нет прав — всегда **403** (страница сама рисует «Нет доступа»).
- **Вход веба** — OIDC code flow, как в v1: `ResponseMode = Query` (код GET-ом: Lax-куки корреляции
  и nonce на кросс-сайтовый `form_post` не уедут), `MapInboundClaims = false`, userinfo не спрашиваем.
  `/account/login?method=google|email&returnUrl=…` ведёт мимо страницы Auth0 к Google или к форме
  пароля (`connection`, `login_hint` ставит `OnRedirectToIdentityProvider`); `prompt` не шлём.
- **Выход** — `/account/logout`: своя кука, кука прошлого входа и сессия Auth0 (`/oidc/logout` с
  `client_id` — без неё следующий вход молча пускает под той же учёткой). Переход с чужого сайта
  (`Sec-Fetch-Site: cross-site`) — 400. Адрес возврата — только свой хост (`ReturnUrl.Normalize`:
  `//evil`, `/\evil`, `/%5Cevil` — на `/`).
- **Автовход** (`AutoLogin`) — перенесён из v1 без изменений по смыслу: кука `.CampaignManager.LastLogin`
  (способ и почта, год), одна попытка на сессию браузера (`.CampaignManager.AutoLogin`), только
  Google. Срабатывает на загрузке документа (`Sec-Fetch-Dest: document`), не на API и не на
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
  (один хост и порт), но ключи у них пока разные — кука v1 для v2 просто анонимна.

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

## JWT для мобильного приложения

`Authentication:Auth0:Audience` — идентификатор API в тенанте. **Без него ни один токен не
проходит** (проверка audience), это нормальное состояние до M4. Access token Auth0 почты не несёт;
её и `email_verified` (и, по желанию, `name`) кладёт Action тенанта под пространство имён
`https://cthulhu.dmnet.dev/` (`IdentityModule.ClaimsNamespace`). Токен без них годится только тому,
кто уже входил через веб (поиск по sub). Первый запрос с токеном и подтверждённой почтой — это и есть
вход: `ResolveAsync` заводит или привязывает пользователя, белый список проверяется на каждом токене.

Заводит API и Action владелец (решение по тенанту); код к ним готов.

## Клиент (WebAssembly)

- `Web.Client/Identity/MeAuthenticationStateProvider` — состояние из `GET /api/v1/me`, один раз на
  загрузку; 401 — аноним (в консоли браузера это видно как ошибка 401 — так и задумано). Админ
  получает обе роли, `Admin` и `Keeper`.
- Вход и выход — `NavigateTo(…, forceLoad: true)` на серверные адреса: клиентский роутер их не знает.
- `UI/Identity`: `/login` (выбор способа, сообщение по `authStatus`), `RedirectToLogin`, `UserMenu` —
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
