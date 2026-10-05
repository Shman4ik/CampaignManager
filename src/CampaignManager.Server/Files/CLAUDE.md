# Модуль Files

Любая картинка, раздатка, трек или портрет — строка `cm.files` (SCHEMA.md, «Файлы»): объект в
MinIO (`storage_key`) или внешний адрес (`external_url`), ровно одно из двух. Ссылающиеся таблицы
хранят `*_file_id`. Сделан в T1.5.

## Где что

| Слой | Файлы |
|---|---|
| `Contracts/Files` | `FilesRoutes` (маршруты и `Content(id)`), DTO, `IFilesApi` |
| `ApiClient/Files` | `FilesApiClient` — multipart-загрузка, текст ProblemDetails в `HttpRequestException` |
| `Data/Files` | `StoredFile`, `FileReferences` — список FK на `files` из модели EF |
| `Server/Files` | `FilesModule`, `FileService`, `FileContentEndpoint` (отдача с Range), `FileTypes`, `Storage/` |
| `UI/Pages/Dev/FilesPage` | `/dev/files` — сквозная проверка: загрузка, картинка, плеер |
| `UI/Admin/AdminFilesPage` | `/admin/files` — сироты: отчёт и удаление (T2.9, [UI/Admin/CLAUDE.md](../../CampaignManager.UI/Admin/CLAUDE.md)) |

## API

- `POST /api/v1/files` — multipart, поле `file`. Ответ — `StoredFileDto`; его `Url` и кладут в `src`.
- `POST /api/v1/files/external` — `{ url }`, только `https://`. Тот же адрес второй раз — та же строка.
- `GET|HEAD /api/v1/files/{id}` — содержимое; у внешнего файла — 302 на адрес.
- `GET /api/v1/admin/files/orphans` — отчёт; `POST …/orphans/delete` с `ids` из отчёта — удаление.

## Загрузка

- **Тип — из расширения по списку `FileTypes`, а не из заголовка клиента.** Файл отдаётся с нашего
  origin: подсунутый `text/html` исполнился бы у нас. SVG поэтому не принимается (это документ со
  скриптами); при отдаче ещё стоят `nosniff` и `Content-Security-Policy: default-src 'none'; sandbox`.
- **sha256 — дедупликация.** Ключ объекта `<images|music>/<sha256><расширение>`. Повторный файл
  находится по `sha256` и возвращает прежнюю строку; две одновременные загрузки одного файла
  упираются в уникальный `storage_key`, и вторая получает строку первой. Отдельного индекса по
  `sha256` нет — таблица в сотни строк, миграция не понадобилась.
- Порядок: хеш → поиск → `PutObject` → строка. Упадёт вставка — останется объект без строки
  (мусор в бакете, не битая ссылка).
- **Предел — `Files:MaxUploadBytes`, 50 МБ** (как у трека в v1). Kestrel по умолчанию режет тело на
  30 МБ, поэтому на эндпоинте `RequestSizeLimitAttribute` (предел + 1 МБ на multipart): 40 МБ
  проходят, 56 МБ получают 413 ещё до сервиса.
- Антифорджери на загрузке выключен: клиенты — WebAssembly и мобильное приложение, токена у них нет.
  Межсайтовую отправку закрывает `SameSite=Lax` куки входа: к POST с чужого сайта браузер её не
  приложит, запрос придёт анонимным и получит 401. Остаётся «тот же сайт» (поддомены `dmnet.dev`) —
  их держит владелец, а JSON-эндпоинты без CORS чужой origin и так не вызовет.
- `uploaded_by_id` — из `CurrentUser`; у повторной загрузки того же файла остаётся первый загрузивший.

## Миниатюры — `GET /api/v1/files/{id}?w=480`

`Thumbnails` (ImageSharp): JPEG, PNG и WebP уменьшаются до ширины `w` (64–1600), формат тот же, ответ с `immutable` и
своим ETag `"{id}-w480"` (304 для миниатюры не принимается за оригинал). Не хранится — один расчёт на браузер. Оригинал не
шире просимого, GIF/AVIF, битая картинка, `w` вне диапазона, `Range` и `HEAD` — оригинал как есть. Сделано ради плиток
бестиария (20 плиток ≈ 6 МБ оригиналов); UI просит миниатюру через `Catalogs/ImageUrls.Thumb`.

## Отдача — не трогать

Знание из `Music/CLAUDE.md` v1 («Файлы из хранилища»), без обходов circuit:

- **Свой origin, а не presigned-ссылка на S3.** Плеер ведёт звук через Web Audio (`GainNode`, иначе
  на iOS громкость не меняется), а `createMediaElementSource` на элементе с чужого origin без CORS
  отдаёт **тишину**. Проверено в браузере: после перемотки анализатор видит сигнал.
- **Range разбирается вручную, без `enableRangeProcessing`** — тому нужен перематываемый поток, то
  есть объект целиком. Здесь отрезок запрашивается у хранилища и идёт в ответ потоком. Поддержаны
  `bytes=N-` (перемотка), `bytes=0-1` (так Safari узнаёт размер), `bytes=-N` (хвост с метаданными);
  несколько отрезков, чужие единицы и `If-Range` с чужим ETag — ответ целиком (RFC 9110 разрешает).
  За пределами файла — 416 с `Content-Range: bytes */size`. Без `Accept-Ranges: bytes` браузер не
  считает источник перематываемым.
- В v1 ответ на Range резался по 4 МиБ, чтобы не держать трек в памяти. Здесь потолка нет: ответ —
  поток, а не буфер.
- **Чтение из MinIO — HTTP по presigned-ссылке на минуту, а не `GetObjectAsync` SDK.** Тому нужен
  колбэк, внутри которого поток надо дочитать (v1 копировал его в `MemoryStream`), а с
  `WithOffsetAndLength` он делает `StatObject` с тем же Range, получает 206 и падает с
  `PartialContentException`. Presigned-ссылка подписывается локально; если `Minio:Region` не задан,
  SDK один раз спрашивает регион у бакета.
- Размер берётся из `files.size_bytes`, без `StatObject`; у строки без размера — спрашиваем хранилище.
- **`Cache-Control: private, max-age=31536000, immutable` и `ETag` по id** — содержимое строки не
  меняется никогда. `private`: ответ зависит от прав. На 404 (строка есть, объекта нет) заголовки
  успеха снимаются — иначе браузер запомнил бы 404 на год. `If-None-Match` → 304.
- Брошенный браузером запрос (перемотка обрывает прежний) — не ошибка: исключение отмены глушится.

## Клиент MinIO

`MinioObjectStorage` — синглтон за `IObjectStorage`: один клиент и один `HttpClient` на приложение
(в v1 — клиент на scope и `BucketExists` перед каждой загрузкой). У `HttpClient` **нет таймаута** —
запросы отменяет токен (`RequestAborted`); стандартный resilience-обработчик v1 с 10 секундами
обрывал бы длинный трек. Клиент собирается лениво: без настроек сервер стартует, а запрос к файлам
падает с сообщением, какой настройки нет. **Бакет создаёт администратор**, загрузка его не создаёт.

Настройки — секция `Minio` с ключами v1: `Endpoint`, `AccessKey`, `SecretKey`, `Secure`,
`BucketName`, плюс необязательный `Region`. В `appsettings.json` их нет намеренно: дефолт привёл бы разработку
в боевой бакет `campain-manager` (v1 и прод).

- **Ветка Neon `dev` (локальная разработка) — бакет `campaign-manager-dev`** на том же MinIO (`s3.dmnet.dev`), заведён в
  T1.3. Строки `files` ветки `dev` — перенос v1: объекты скопированы туда из боевого бакета под теми же ключами
  (`images/beasts/…`, `music/…`), новые загрузки ложатся туда же (`images|music/<sha256>…`). Локальная разработка
  на ветке `dev` — `"BucketName": "campaign-manager-dev"` в `Server/appsettings.Development.json` с ключом пользователя
  MinIO только на этот бакет (копировать файл скриптом, не печатая). Прод — бакет `campain-manager`, ключ — в его
  SealedSecret.
- Строка `files` переноса — `sha256` пуст (объект не скачивался, копия шла внутри MinIO): повторная загрузка того
  же файла заведёт новую строку, а не найдёт перенесённую. Размер и тип — из хранилища и расширения.
- Свой MinIO в wslc — для тестов адаптера и работы без сети:

```bash
# образ minio/minio с Docker Hub больше не публикуется — берём сборку Chainguard
wslc run -d --rm --name cm-minio -p 59000:9000 -e MINIO_ROOT_USER=cmtest -e MINIO_ROOT_PASSWORD=cmtest-secret \
  cgr.dev/chainguard/minio:latest server /data
curl -X PUT --user cmtest:cmtest-secret --aws-sigv4 "aws:amz:us-east-1:s3" http://localhost:59000/cm-dev-files
```

и `"Minio": { "Endpoint": "localhost:59000", "AccessKey": "cmtest", "SecretKey": "cmtest-secret",
"Secure": false, "BucketName": "cm-dev-files" }`.

## Сироты

- Условие — anti-join из SCHEMA.md, но **список ссылок строится по модели EF** (`FileReferences`):
  новая таблица с FK на `files` сама попадает в поиск, и забытая в запросе ссылка не превратит живую
  картинку в «сироту». Тест `Orphan_search_covers_every_reference_from_schema` перечисляет шесть
  нынешних ссылок — новую в него дописать.
- **Моложе `Files:OrphanGracePeriod` (сутки) файл не сирота**: загрузка и сохранение ссылки — два
  запроса, и между ними файл честно ни на что не ссылается.
- Удаление — только присланных id, и условие отчёта повторяется в самом `DELETE … RETURNING`:
  файл, на который сослались после отчёта, уйдёт в `skipped`. Сначала строки, потом объекты;
  неудалённый объект — в лог и в `objectsNotDeleted`.
- Узкое место: загрузка того же содержимого в момент удаления его сироты может потерять объект
  (ключ один). Удаление сирот — редкое ручное действие админа, с этим живём.

## Права

`AccessPolicy.ForFilesAsync` ([Access/CLAUDE.md](../Access/CLAUDE.md)): читать и загружать — любой
вошедший (игроку нужны портреты и раздатки), сироты — администратор. Эндпоинты под
`RequireAuthorization()` (сироты — `Policies.Admin`), методы записи `FileService` ещё и зовут `Demand`.
Права на то, *где* файл показан (лист, раздатка), — у владельца ссылки; отдача по id их не повторяет.

## Тесты

- `Server.Tests/Files/FilesApiTests` — API через `FilesApiClient` и сырой HTTP: тип, кэш, дедупликация,
  отказы, Range во всех формах, 416, 304, HEAD, внешние адреса, сироты. Хранилище —
  `InMemoryObjectStorage` (в CI только Postgres), база — своя (`SchemaDatabase`), часы — `MovableTime`.
- `MinioObjectStorageTests` — адаптер на настоящем MinIO: `CM_TEST_MINIO=localhost:59000;cmtest;cmtest-secret`,
  свой бакет на прогон; без переменной пропускаются.
