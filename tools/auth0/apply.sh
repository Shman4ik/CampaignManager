#!/usr/bin/env bash
# Страница входа тенанта cthulhu-dmnet: identifier first, passkey, тема, шаблон с картинкой, тексты.
# Повторный запуск безопасен — каждый шаг приводит тенант к состоянию из файлов этой папки.
#
#   tools/auth0/apply.sh                         # картинка и знак с прода (cthulhu.dmnet.dev/img/auth)
#   ASSETS=https://…/img/auth tools/auth0/apply.sh   # предпросмотр до деплоя, например raw.githubusercontent ветки
#
# Картинка и знак лежат в wwwroot/img/auth обоих приложений (v1 и 2.0) по одному пути — страница
# Auth0 берёт их с домена прода, кто бы его ни обслуживал.
#
# Нужен auth0 CLI, вошедший с правами на промпты, брендинг и коннекшены:
#   auth0 login --scopes "read:prompts,update:prompts,read:branding,update:branding,read:connections,update:connections"
# На Windows без HOME CLI не находит свой конфиг (см. корневой CLAUDE.md, «Authentication»).
set -euo pipefail
export PYTHONUTF8=1  # иначе Python на Windows пишет stdout в cp1252 и падает на кириллице

dir="$(cd "$(dirname "$0")" && pwd)"
assets="${ASSETS:-https://cthulhu.dmnet.dev/img/auth}"
connection_name="Username-Password-Authentication"
tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT

# Все вызовы — с stdin из /dev/null: команды с --data читают stdin, если он не терминал, и виснут.
api() { auth0 api "$@" < /dev/null; }

# Python из Windows не понимает пути Git Bash вида /c/… — отдаём ему путь Windows, где он есть.
native() { if command -v cygpath > /dev/null; then cygpath -w "$1"; else echo "$1"; fi; }

echo "1/6 Identifier first: сначала почта, затем пароль или passkey"
api patch prompts --data '{"identifier_first": true}' > /dev/null

echo "2/6 Passkey в $connection_name"
api get "connections?strategy=auth0&name=$connection_name" > "$tmp/connections.json"
# PATCH заменяет options целиком — меняем только passkey, остальное отправляем как было.
python - "$(native "$tmp/connections.json")" "$(native "$tmp/connection-patch.json")" > "$tmp/connection-id" <<'EOF'
import json, sys
connection = json.load(open(sys.argv[1], encoding="utf-8"))[0]
options = connection["options"]
methods = options.setdefault("authentication_methods", {})
methods.setdefault("password", {"enabled": True})["enabled"] = True
methods["passkey"] = {"enabled": True}
options["passkey_options"] = {
    "challenge_ui": "both",                 # кнопка «Войти с ключом доступа» и автозаполнение
    "progressive_enrollment_enabled": True,  # после входа по паролю предложить создать ключ
    "local_enrollment_enabled": True,        # и на новом устройстве тоже
}
json.dump({"options": options}, open(sys.argv[2], "w", encoding="utf-8"), ensure_ascii=False)
print(connection["id"], end="")
EOF
api patch "connections/$(cat "$tmp/connection-id")" --data "$(cat "$tmp/connection-patch.json")" > /dev/null

echo "3/6 Логотип и цвета"
api patch branding --data "$(cat "$dir/branding.json")" > /dev/null

echo "4/6 Тема виджета"
if theme_id="$(api get branding/themes/default 2> /dev/null | python -c "import json,sys; print(json.load(sys.stdin).get('themeId', ''))" 2> /dev/null)" && [ -n "$theme_id" ]; then
  api patch "branding/themes/$theme_id" --data "$(cat "$dir/theme.json")" > /dev/null
else
  api post branding/themes --data "$(cat "$dir/theme.json")" > /dev/null
fi

echo "5/6 Шаблон страницы (картинки: $assets)"
python - "$(native "$dir/login-page.liquid")" "$assets" > "$tmp/template.json" <<'EOF'
import json, sys
template = open(sys.argv[1], encoding="utf-8").read().replace("__ASSETS__", sys.argv[2])
print(json.dumps({"template": template}, ensure_ascii=False))
EOF
api put branding/templates/universal-login --data "$(cat "$tmp/template.json")" > /dev/null

echo "6/6 Тексты экрана входа (ru)"
# Тело — по экранам промпта: {"login-id": {"title": …}}.
api put prompts/login-id/custom-text/ru --data "$(cat "$dir/text-login-id.ru.json")" > /dev/null

echo "Готово: https://auth.cthulhu.dmnet.dev — страница входа обновлена для dev и прода сразу."
