# Переключение прода на 2.0 (T3.2) — инструкция владельцу

Карточка — [TASKS.md](TASKS.md), T3.2. **Делает владелец**: агенту запрещены запись в `main`, `kubectl apply` на
прод-кластер и слияние PR в `dmnet-gitops`. Составлено 2026-10-03 по `master` `3166713` и `dmnet-gitops`.

Идея: 2.0 встаёт **на место** v1 — тот же namespace `campaign-manager`, тот же домен, то же прод-приложение Auth0,
тот же бакет MinIO и тот же SealedSecret. Меняются образ, `env` Deployment и пробы. Схемы `games`/`identity` не
трогаются, поэтому откат — один revert в `dmnet-gitops`.

## Когда можно

- Открытых issue `ux` и `parity` нет; beta (`beta.cthulhu.dmnet.dev`) на последнем `master` и проверена на iPad.
- На `dev` свежий перенос, отчёт — [migration-report.md](migration-report.md): с ним сверяется прогон на `main`.
- Вечер без игры: v1 после шага 2 не пишет в `cm`, а записанное в v1 после переноса в 2.0 не попадёт.

## 0. Накануне — без простоя

1. **Ветка-страховка Neon** от `main` (бэкап на момент переключения):
   ```bash
   npx neonctl@latest branches create --project-id old-wood-199224 --parent main --name pre-v2-2026-10
   ```
2. **Секрет не меняется.** 2.0 читает те же ключи, что лежат в `campaign-manager-env`:
   `ConnectionStrings__DefaultConnection` (ветка `main`), `Authentication__Auth0__{Domain,ClientId,ClientSecret}`
   (прод-приложение Auth0), `Minio__AccessKey`/`Minio__SecretKey`. Лишние ключи v1 (`Authentication__Google__*`,
   `Community__DonateUrl`) 2.0 просто не читает — удалить при T3.3.
3. **Auth0** — ничего: домен `auth.cthulhu.dmnet.dev`, пути `/signin-oidc` и `/signout-callback-oidc` у 2.0 те же.
   Ключи Data Protection у 2.0 свои (`cm.data_protection_keys`), поэтому после переключения все один раз войдут
   заново — автовход через Google сделает это без клика.
4. **Ключ MinIO** у v1 — администраторский. Для 2.0 он работает как есть; по принципу наименьших прав лучше
   завести пользователя с политикой только на `campain-manager` (как `campaign-manager-dev-rw` у беты) и положить его
   ключи в секрет через `kubeseal --raw` (команда — [src/CLAUDE.md](../../src/CLAUDE.md), «Beta-стенд», namespace
   `campaign-manager`, имя `campaign-manager-env`). Можно сделать и после переключения.
5. **Подготовить PR в `dmnet-gitops`** (не сливать до шага 3) — `workloads/campaign-manager/deployment.yaml`
   повторяет бету, кроме бакета:
   - `image: ghcr.io/shman4ik/campaign-manager-v2`; в `kustomization.yaml` — `images[].name` то же,
     `newTag` — текущий тег беты (`workloads/campaign-manager-beta/kustomization.yaml`);
   - `env`: `ASPNETCORE_ENVIRONMENT=Production`, `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`,
     `DOTNET_EnableDiagnostics=false`, `Minio__Endpoint=s3.dmnet.dev`, `Minio__Secure=true`,
     `Minio__BucketName=campain-manager` (именно так, с опечаткой — это имя боевого бакета);
   - пробы `httpGet: {path: /health, port: http}` вместо `tcpSocket`; ресурсы — как у беты;
   - аннотация `campaign-manager/secret-revision: "1"` в шаблоне пода (для будущих смен секрета).
6. **Подготовить PR в этом репозитории** (не сливать до шага 4): один воркфлоу 2.0 катит и бету, и прод.
   - `v2-beta-deploy.yml` → `v2-deploy.yml`: после пуша образа шаг «Point dmnet-gitops» обновляет `newTag` в
     `workloads/campaign-manager-beta` **и** `workloads/campaign-manager` одним коммитом (бета остаётся на `dev`,
     прод — на `main`; образ один).
   - `docker-build-deploy.yml` (v1) удалить: иначе исправление в `CampaignManager.Web` соберёт образ v1 и
     попытается поднять тег v1 поверх 2.0 (`yq` его не найдёт, и прогон упадёт — но лучше не держать заряженным).
   - В корневом `CLAUDE.md` и `src/CLAUDE.md` — «прод на 2.0», раздел «Beta-стенд» — что прод катится тем же воркфлоу.

## 1. Окно

Договориться, что этим вечером не играют; v1 работает до шага 3 только на чтение.

## 2. Данные на `main`

Строку `main` взять из секрета прода (или консоли Neon) **в переменную**, не печатая.

```bash
export CM_DB="<строка ветки main>"
dotnet ef database update --project src/CampaignManager.Data        # создаёт схему cm; games/identity не трогает
dotnet run --project tools/CampaignManager.Migrate -- --settings <файл> \
  --report docs/v2/migration-report-prod.md --note "Прод, переключение T3.2"
unset CM_DB
```

- `--settings` — JSON вне репозитория: `Minio` с ключом v1 (`campain-manager` он читает),
  `SourceBucket` = `TargetBucket` = `campain-manager` — объекты не копируются, перенос только сверяет их.
- `--reset` **не нужен**: `cm` на `main` пуста. Повторный прогон (если первый упал) — с `--reset`: прогон
  идёт одной транзакцией, упавший ничего не оставляет.
- Сверить `migration-report-prod.md` с `migration-report.md` (`dev`): счётчики кампаний, листов, сценариев,
  файлов и «отброшено» должны совпасть с поправкой на то, что успели записать в v1 после копии `dev`.

## 3. Выкатка

Слить PR из шага 0.5 в `dmnet-gitops`. Argo CD катит за ~90 с; `RollingUpdate` держит v1, пока под 2.0 не
пройдёт `/health`.

## 4. Проверка на iPad (5 минут)

Вход (автовход Google), главная, кампания, лист сыщика (бросок, правка), сценарий в режиме игры с раздаткой,
картинка твари в бестиарии (бакет), фонотека с проигрыванием. Если всё на месте — слить PR из шага 0.6.

## 5. Откат

Revert PR из шага 0.5 в `dmnet-gitops` → Argo вернёт образ `campaign-manager:0.1.N` и пробы v1. Схемы
`games`/`identity` не тронуты — v1 поднимается как был. **Записанное в 2.0 после переключения при откате
теряется** (оно только в `cm`). Ветка `pre-v2-2026-10` нужна, только если пострадали сами данные `main`.

## 6. После

- Неделя без отката — T3.2 сделана (отметка в TASKS.md).
- Ещё две недели — T3.3: удалить v1 из репозитория, лишние ключи из секрета; схемы `games`/`identity` — отдельным
  шагом владельца после свежего бэкапа Neon.
