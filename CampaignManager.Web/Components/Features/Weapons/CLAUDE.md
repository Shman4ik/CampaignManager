# Weapons Feature

Master weapon catalog (independent entity). Источник правды по данным — таблица XVII
«Оружие» книги правил CoC 7e (стр. 399–402).

## Key Services
- `WeaponService(dbContextFactory, IMemoryCache, logger)` — CRUD + cached lookups.
  `GetAllRangeWeaponsAsync()` фильтрует уже загруженный список в памяти: `WeaponType` —
  не набор флагов, объединить типы в одно значение нельзя.

## Key Models
- `Weapon : BaseDataBaseEntity, INamedEntity`.
- `WeaponDamageInfo`, `RangeDamageEntry` — разобранный урон (`DamageInfo`, JSONB).
  Заполняется `Utilities/Services/DamageFormulaParser` из текстового поля `Damage`
  и хранит внутри `RawText` + `IsParsed`. **Это образец для любого нового разобранного
  поля**: типизированные данные рядом со строкой правил, а не вместо неё.
- `WeaponType` — разделы таблицы XVII. Значения нумеруются подряд, `[Flags]` на нём быть
  не должно: раньше он там стоял, и `Pistols | Rifles` молча давал `Shotguns`.

## Данные каталога
- `Skill` хранит **каноническое** имя навыка из `games."Skills"` («Стрельба (пистолет)»),
  а не сокращение книги («Стрельба (П)»). Сокращения были заменены миграцией
  `WeaponCatalogRulesAlignment`; связь по идентификатору пока не заведена, поэтому строка
  обязана совпадать с каталогом навыков посимвольно.
- `Malfunction` — строка, потому что в старых JSONB-листах встречается «00»
  (это 100 на процентных костях). Разбирает `CombatService.TryGetMalfunctionThreshold`.
  Пусто оно теперь только у холодного оружия (25 записей) — там в книге прочерк.
- `Is1920` / `IsModern` / `IsRare` — колонка «Встречается» книги. Редкость **ортогональна**
  эпохе: «1920-е, редко» — это `Is1920` вместе с `IsRare`. Раньше «Редкое» лежало текстом
  в `Notes` и даже в `Cost`.
- `Notes` — только настоящие примечания. Эпоха, редкость и «(пронз.)» дублировались там
  и вычищены: у них есть свои поля.
- `Damage` пишется как в книге: `1d6 + БкУ`, кости в нижнем регистре, пробелы вокруг `+`.
  Меняешь `Damage` — синхронизируй `DamageInfo.RawText` (и `RangeDamages[].Damage.RawText`
  у дробовиков), иначе в форме редактирования покажется старая формула.

## Notes
- A character's carried weapons are just `Character.Weapons` (`List<Weapon>`) — same catalog
  type, no separate character-specific wrapper.
- `WeaponsPage.ShowEditModal` копирует поля вручную. Добавил поле в `Weapon` — добавь и туда,
  иначе редактирование молча его обнулит.
