# CampaignManager 2.0 — схема базы и перенос данных

Спецификация для задач `T1.2` (схема) и `T1.3` (перенос) из [TASKS.md](TASKS.md). Почему схема
именно такая — в [AUDIT.md](AUDIT.md), раздел «Данные». Схема `cm` создаётся рядом со старыми
`games` и `identity`, в той же базе. До переключения v1 о ней не знает, после переключения
старые схемы остаются только для чтения, а затем удаляются (`T3.3`).

## Правила

1. **Колонка или таблица** — всё, по чему ищут, фильтруют, соединяют, проверяют права и на что
   ссылаются из другого места. **JSONB-документ** — только то, что один владелец правит и читает
   целиком и что наружу не ссылается: лист сыщика, статблок твари, состояние сцены.
   В документах три документа и больше ничего.
2. **Инварианты — в базе.** CHECK, частичные уникальные индексы, составные FK. Правило, которое
   живёт только в комментарии, в v1 нарушено в живых данных (см. AUDIT, «Владелец листа»).
3. **Человек — `*_id → users`**, навык — `skill_id`, картинка или файл — `file_id`. Почты и имена
   строками в других таблицах не храним.
4. **Именование**: схема `cm`, `snake_case` (пакет `EFCore.NamingConventions`,
   `UseSnakeCaseNamingConvention()`), сырой SQL пишется без кавычек.
5. **Перечисления — текстом, имя члена C#** (`'OnHold'`), одинаково в колонках и в JSON.
   CHECK со списком значений генерирует хелпер из самого enum, руками не выписывается.
6. **Документы**: JSON в camelCase, enum'ы строками, рядом колонка `*_version`; старые версии 2.0
   приводятся к текущей при чтении (апкастер в Core), копий `Id` строки внутри документа нет.
   Неизвестные поля переживают чтение и запись (`[JsonExtensionData]`) — старое мобильное
   приложение не должно стирать то, чего не знает.
7. **Время** — только `timestamptz`; `created_at`/`updated_at` ставит перехватчик `SaveChanges`.
   `Npgsql.EnableLegacyTimestampBehavior` не включаем.
8. **Конкурентность**: у корней, которые правят с двух устройств (`characters`, `scenarios`,
   `encounters`, справочники), токен — системная `xmin` (`uint Version` + `IsRowVersion()`).
   API отдаёт её как ETag и принимает в `If-Match`; конфликт — 409, а не молчаливая перезапись.
9. **Идентификаторы** — uuid v7 из приложения (`Guid.CreateVersion7()`).

## DDL

DDL — спецификация, а не готовая миграция. Миграцию генерирует EF Core из конфигурации модели.
Её результат сверяется с этим текстом, а частичные индексы, `NULLS NOT DISTINCT` и
`ON DELETE SET NULL (column)` дописываются в миграцию руками (`migrationBuilder.Sql`).

Сверено с миграцией `InitialCmSchema` (T1.2). Расхождения, оставленные намеренно:

- у колонок-массивов enum'ов (`eras`) есть CHECK `eras <@ ARRAY['Classic', 'Modern']` — его генерирует
  тот же хелпер, что и CHECK обычных enum-колонок (правило 5); в тексте ниже его нет;
- CHECK названы `ck_<таблица>_<что>`, индексы и FK без имени в тексте — по конвенции EF (`ix_…`, `fk_…`);
  `unique (…)` внутри таблицы стал уникальным индексом — для базы это одно и то же;
- EF добавил обычный индекс на каждый FK, не покрытый другим индексом (`ix_<таблица>_<колонка>`):
  Postgres сам их не создаёт, а без них удаление родителя и соединения идут полным перебором;
- `xmin` (правило 8) есть и у `music_tracks` — фонотеку правят так же, как справочники;
- частичные индексы, `NULLS NOT DISTINCT` и триграммы EF выражает сам; SQL-ом в миграции — только
  `lower(name)` и составной FK `characters → campaign_members`.

```sql
create schema cm;
create extension if not exists citext;
create extension if not exists pg_trgm;

-- ═══════════════════════ Люди ═══════════════════════

create table cm.users (
    id             uuid primary key,
    auth0_sub      text unique,          -- null до первого входа после переноса: при входе ищем по email и записываем
    email          citext not null unique,
    display_name   text not null,
    role           text not null default 'Player' check (role in ('Player', 'Keeper', 'Admin')),
    last_login_at  timestamptz,
    created_at     timestamptz not null default now(),
    updated_at     timestamptz not null default now()
);

-- Строка на ключ: два устройства не затирают ключи друг друга, нет ловушки «словарь правят на месте».
create table cm.user_preferences (
    user_id     uuid not null references cm.users on delete cascade,
    key         text not null,
    value       jsonb not null,
    updated_at  timestamptz not null default now(),
    primary key (user_id, key)
);

create table cm.keeper_applications (
    id              uuid primary key,
    user_id         uuid not null references cm.users on delete cascade,
    message         text not null check (length(message) <= 1000),
    status          text not null default 'Pending' check (status in ('Pending', 'Approved', 'Rejected')),
    reviewed_by_id  uuid references cm.users on delete set null,
    reviewed_at     timestamptz,
    review_comment  text,
    created_at      timestamptz not null default now(),
    updated_at      timestamptz not null default now()
);
create unique index keeper_applications_one_pending on cm.keeper_applications (user_id) where status = 'Pending';

-- ═══════════════════════ Файлы ═══════════════════════
-- Любая картинка, раздатка, трек или портрет — строка здесь. Объект либо лежит в MinIO, либо это
-- внешний адрес. Копия листа делит файл с оригиналом; сироты ищутся одним anti-join по FK.

create table cm.files (
    id              uuid primary key,
    storage_key     text unique,         -- ключ объекта MinIO: images/beasts/…, music/…, handouts/…
    external_url    text,                -- https://… — картинка снаружи
    content_type    text,
    size_bytes      bigint,
    sha256          text,                -- повторная загрузка того же файла не плодит объект
    original_name   text,
    uploaded_by_id  uuid references cm.users on delete set null,
    created_at      timestamptz not null default now(),
    check ((storage_key is null) <> (external_url is null))
);

-- ═══════════════════════ Кампании ═══════════════════════

create table cm.campaigns (
    id             uuid primary key,
    name           text not null,
    kind           text not null default 'Campaign' check (kind in ('Campaign', 'OneShot')),
    status         text not null default 'Planning' check (status in ('Planning', 'Active', 'OnHold', 'Completed')),
    era            text not null default 'Classic' check (era in ('Classic', 'Modern')),
    created_by_id  uuid references cm.users on delete set null,
    created_at     timestamptz not null default now(),
    updated_at     timestamptz not null default now()
);

-- Хранитель — тоже участник, с ролью. «Может ли он это видеть» = «участник ли он».
create table cm.campaign_members (
    campaign_id   uuid not null references cm.campaigns on delete cascade,
    user_id       uuid not null references cm.users on delete restrict,
    role          text not null check (role in ('Keeper', 'Player')),
    display_name  text,                  -- псевдоним в этой кампании (нарочно разный); null — users.display_name
    joined_at     timestamptz not null default now(),
    primary key (campaign_id, user_id)
);
create unique index campaign_members_one_keeper on cm.campaign_members (campaign_id) where role = 'Keeper';

-- ═══════════════════════ Справочники ═══════════════════════
-- Общее у всех: code — стабильный ключ книжной записи (синхронизация с правилами идёт по нему,
-- переименование — обычный update); null у самодельных. Код — английское название записи в книгах
-- Chaosium для 7e в kebab-case с префиксом справочника: skill., occupation., weapon., spell., book.,
-- item., creature. (skill.dodge, occupation.private-investigator, weapon.thompson-submachine-gun,
-- creature.deep-one). Из русского имени код не вычисляется: соответствие — явные таблицы
-- Core/Catalogs/*Codes (SkillCodes, OccupationCodes, WeaponCodes, SpellCodes, BookCodes, ItemCodes,
-- CreatureCodes). Только у навыков есть иерархия: специализация — код родителя + «.» + английское
-- название специализации (skill.firearms.handgun). Выданный код не меняется. source — страница
-- книги; null — самодельное.
-- Имя уникально без учёта регистра (индекс по lower(name)); поиск — триграммы (pg_trgm).
-- Тексты описаний книги в публичный репозиторий не кладём (см. TASKS, T1.3, «Сиды»).

create table cm.skills (
    id                    uuid primary key,
    code                  text unique,
    name                  text not null,
    parent_id             uuid references cm.skills on delete restrict,  -- специализация → родитель
    base_value            int not null default 0,
    base_formula          text,          -- 'DEX/2', 'EDU' — когда база не число
    category              text not null check (category in ('ProblemSolving', 'InformationGathering', 'Special', 'Social',
                                                            'Healing', 'CombatGeneral', 'Knowledge', 'CombatFirearms', 'Actions')),
    is_uncommon           boolean not null default false,
    eras                  text[] not null default '{Classic,Modern}',
    description           text not null default '',
    usage_examples        text[] not null default '{}',
    failure_consequences  text[] not null default '{}',
    opposing_skills       text[] not null default '{}',
    time_required         text,
    can_retry             boolean not null default false,
    source                text,
    created_by_id         uuid references cm.users on delete set null,
    created_at            timestamptz not null default now(),
    updated_at            timestamptz not null default now()
);
create unique index skills_name on cm.skills (lower(name));

create table cm.occupations (
    id                    uuid primary key,
    code                  text unique,
    name                  text not null,
    skill_points_formula  text not null check (skill_points_formula in ('Edu4', 'Edu2Dex2', 'Edu2App2', 'Edu2Str2', 'Edu2Pow2',
                                                 'Edu2DexOrStr2', 'Edu2AppOrPow2', 'Edu2DexOrPow2', 'Edu2AppOrDexOrStr2')),
    credit_rating_min     int not null,
    credit_rating_max     int not null,
    eras                  text[] not null default '{Classic,Modern}',
    is_lovecraftian       boolean not null default false,
    tags                  text[] not null default '{}',   -- было битовой маской int
    source                text,
    created_by_id         uuid references cm.users on delete set null,
    created_at            timestamptz not null default now(),
    updated_at            timestamptz not null default now(),
    check (credit_rating_min between 0 and 99 and credit_rating_max between credit_rating_min and 99)
);
create unique index occupations_name on cm.occupations (lower(name));

-- Слоты профессии вместо четырёх полей v1 (OccupationSkills, SkillChoices, SocialSkillSlots, FreeSkillSlots).
create table cm.occupation_slots (
    id              uuid primary key,
    occupation_id   uuid not null references cm.occupations on delete cascade,
    ord             int not null,
    kind            text not null check (kind in ('Skill', 'Specialization', 'AnySpecialization', 'Choice', 'Social', 'Free')),
    skill_id        uuid references cm.skills on delete restrict,  -- Skill: навык; (Any)Specialization: родитель
    specialization  text,                                          -- Specialization: «латынь», «черчение»
    choose_count    int not null default 1 check (choose_count >= 1),
    unique (occupation_id, ord),
    check (   (kind in ('Skill', 'AnySpecialization') and skill_id is not null and specialization is null)
           or (kind = 'Specialization' and skill_id is not null and specialization is not null)
           or (kind in ('Choice', 'Social', 'Free') and skill_id is null and specialization is null))
);
create table cm.occupation_slot_options (       -- варианты слота Choice; родитель разворачивается в специализации
    slot_id   uuid not null references cm.occupation_slots on delete cascade,
    skill_id  uuid not null references cm.skills on delete restrict,
    primary key (slot_id, skill_id)
);

-- Строки книги остаются для показа; числа для боя — отдельными колонками, разобранными один раз
-- при вводе (Core.WeaponStatsParser), а не на каждом чтении. Ступени совместимости из v1 нет.
create table cm.weapons (
    id                     uuid primary key,
    code                   text unique,
    name                   text not null,
    type                   text not null check (type in ('Melee', 'Pistols', 'Rifles', 'Shotguns', 'AssaultRifles',
                                                         'SubmachineGuns', 'MachineGuns', 'ExplosivesAndHeavyWeapons', 'Other')),
    skill_id               uuid not null references cm.skills on delete restrict,
    eras                   text[] not null default '{Classic,Modern}',
    is_rare                boolean not null default false,
    is_impaling            boolean not null default false,
    damage                 text not null,        -- «1d6 + БкУ», как в книге; разбирает Core
    damage_by_range        jsonb,                -- дробовики: [{"range":"10 м","damage":"4d6"},…]
    range                  text not null default '',
    base_range_m           int,
    attacks                text not null default '',
    shots_per_round        int,
    max_shots_per_round    int,
    ammo                   text not null default '',
    ammo_capacity          int,
    ammo_capacity_options  int[],
    single_use             boolean not null default false,
    malfunction            int check (malfunction between 1 and 100),   -- «00» книги = 100
    cost                   text not null default '',
    cost_classic           numeric(12, 2),
    cost_modern            numeric(12, 2),
    notes                  text not null default '',
    source                 text,
    created_by_id          uuid references cm.users on delete set null,
    created_at             timestamptz not null default now(),
    updated_at             timestamptz not null default now()
);
create unique index weapons_name on cm.weapons (lower(name));

create table cm.spells (
    id             uuid primary key,
    code           text unique,
    name           text not null,
    alt_names      text[] not null default '{}',
    spell_type     text not null,
    cost           text,               -- текст книги; подсказку для боя разбирает Core.SpellStatsReader
    casting_time   text,
    description    text not null default '',
    source         text,
    created_by_id  uuid references cm.users on delete set null,
    created_at     timestamptz not null default now(),
    updated_at     timestamptz not null default now()
);
create unique index spells_name on cm.spells (lower(name));
create index spells_alt_names on cm.spells using gin (alt_names);

create table cm.books (
    id               uuid primary key,
    code             text unique,
    name             text not null,
    book_type        text not null check (book_type in ('MythosBook', 'OccultBook')),
    alt_names        text[] not null default '{}',
    language         text,
    year             text,
    author           text,
    sanity_loss      text,
    mythos_initial   int,
    mythos_full      int,
    mythos_rating    int,
    study_weeks      int,
    occultism_bonus  int,
    description      text not null default '',
    image_file_id    uuid references cm.files on delete set null,
    source           text,
    created_by_id    uuid references cm.users on delete set null,
    created_at       timestamptz not null default now(),
    updated_at       timestamptz not null default now()
);
create unique index books_name on cm.books (lower(name));

-- Сопоставление «заклинание книги → каталог» делается один раз, при вводе. Несопоставленное
-- остаётся строкой книги (spell_id null) — Хранитель свяжет руками.
create table cm.book_spells (
    book_id   uuid not null references cm.books on delete cascade,
    ord       int not null,
    raw_name  text not null,
    spell_id  uuid references cm.spells on delete set null,
    primary key (book_id, ord)
);

create table cm.items (
    id             uuid primary key,
    code           text unique,
    name           text not null,
    type           text,
    eras           text[] not null default '{Classic,Modern}',
    description    text,
    price          numeric(12,2),                      -- $ 1920-х; null — не указана (миграция ItemPrice)
    image_file_id  uuid references cm.files on delete set null,
    source         text,
    created_by_id  uuid references cm.users on delete set null,
    created_at     timestamptz not null default now(),
    updated_at     timestamptz not null default now()
);
create unique index items_name on cm.items (lower(name));

create table cm.creatures (
    id                 uuid primary key,
    code               text unique,
    name               text not null,
    type               text not null default 'Other' check (type in ('Other', 'MythicMonsters', 'MythicGods', 'Monsters', 'Beast')),
    description        text,
    statblock          jsonb not null,   -- см. «Статблок»
    statblock_version  int not null,
    source             text,
    created_by_id      uuid references cm.users on delete set null,
    created_at         timestamptz not null default now(),
    updated_at         timestamptz not null default now()
);
create unique index creatures_name on cm.creatures (lower(name));

create table cm.creature_images (           -- первая по ord — обложка
    creature_id  uuid not null references cm.creatures on delete cascade,
    ord          int not null,
    file_id      uuid not null references cm.files on delete restrict,
    caption      text,
    primary key (creature_id, ord)
);

-- картинки оружия (#227) и профессий — так же, как у тварей: пользовательские файлы, первая — обложка
create table cm.weapon_images (
    weapon_id    uuid not null references cm.weapons on delete cascade,
    ord          int not null,
    file_id      uuid not null references cm.files on delete restrict,
    caption      text,
    primary key (weapon_id, ord)
);

create table cm.occupation_images (
    occupation_id uuid not null references cm.occupations on delete cascade,
    ord           int not null,
    file_id       uuid not null references cm.files on delete restrict,
    caption       text,
    primary key (occupation_id, ord)
);

create table cm.spell_images (              -- картинки заклинаний — так же, первая — обложка в строке справочника
    spell_id     uuid not null references cm.spells on delete cascade,
    ord          int not null,
    file_id      uuid not null references cm.files on delete restrict,
    caption      text,
    primary key (spell_id, ord)
);

create table cm.artifacts (                 -- артефакты главы 13: оружие стреляет записью cm.weapons с тем же названием
    id           uuid primary key,
    code         text unique,                  -- artifact.shining-trapezohedron
    name         text not null,
    kind         text not null,                -- Device | Weapon | Armor | Substance | Relic | Place | Other
    used_by      text[] not null default '{}', -- «ми-го», «йитиане», «кто угодно»
    usage        text,                         -- как с ним управится сыщик
    description  text not null default '',
    source       text,
    created_by_id uuid references cm.users on delete set null,
    created_at   timestamptz not null default now(),
    updated_at   timestamptz not null default now()
);
create unique index artifacts_name on cm.artifacts (lower(name));

create table cm.artifact_images (           -- картинки артефактов — как у заклинаний
    artifact_id  uuid not null references cm.artifacts on delete cascade,
    ord          int not null,
    file_id      uuid not null references cm.files on delete restrict,
    caption      text,
    primary key (artifact_id, ord)
);

create table cm.music_tracks (
    id             uuid primary key,
    name           text not null,
    youtube_id     text,
    file_id        uuid references cm.files on delete restrict,
    start_seconds  int not null default 0,
    loop           boolean not null default true,
    volume         int not null default 100 check (volume between 0 and 100),
    tags           text[] not null default '{}',
    notes          text,
    created_by_id  uuid references cm.users on delete set null,
    created_at     timestamptz not null default now(),
    updated_at     timestamptz not null default now(),
    check ((youtube_id is null) <> (file_id is null))
);
create unique index music_tracks_name on cm.music_tracks (lower(name));
create index music_tracks_tags on cm.music_tracks using gin (tags);

-- Поиск по каталогам: триграммы по имени (ILIKE '%…%' по-русски)
create index skills_name_trgm   on cm.skills   using gin (name gin_trgm_ops);
create index weapons_name_trgm  on cm.weapons  using gin (name gin_trgm_ops);
create index spells_name_trgm   on cm.spells   using gin (name gin_trgm_ops);
create index books_name_trgm    on cm.books    using gin (name gin_trgm_ops);
create index items_name_trgm    on cm.items    using gin (name gin_trgm_ops);
create index creatures_name_trgm on cm.creatures using gin (name gin_trgm_ops);

-- ═══════════════════════ Сценарии: содержимое ═══════════════════════
-- Сценарий — это содержимое (общая библиотека Хранителей). Игра в кампании — scenario_runs ниже.
-- Шаблоны в кампании больше не копируются; переделать под свою группу — явный форк (source_scenario_id).

create table cm.scenarios (
    id                  uuid primary key,
    name                text not null,
    summary             text,            -- v1 Description
    body_md             text,            -- v1 Journal: основной текст Хранителя в Markdown (до 16 КБ)
    setting             text,            -- v1 Location: «Бостон, Массачусетс, США»
    era                 text check (era in ('Classic', 'Modern')),
    setting_date        text,            -- v1 Era как текст: «Июнь 1925 года», «1931»
    author_id           uuid references cm.users on delete set null,
    source_scenario_id  uuid references cm.scenarios on delete set null,
    created_at          timestamptz not null default now(),
    updated_at          timestamptz not null default now()
);

create table cm.scenario_locations (
    id           uuid primary key,
    scenario_id  uuid not null references cm.scenarios on delete cascade,
    parent_id    uuid references cm.scenario_locations on delete cascade,
    ord          int not null,
    name         text not null,
    address      text,
    description  text,
    music_tags   text[] not null default '{}'
);
create index scenario_locations_order on cm.scenario_locations (scenario_id, ord);

-- Проверка в локации: навык, характеристика или Удача (в данных v1 9 из 39 — «СИЛ», «Удача»…)
create table cm.scenario_checks (
    id              uuid primary key,
    location_id     uuid not null references cm.scenario_locations on delete cascade,
    ord             int not null,
    target_kind     text not null check (target_kind in ('Skill', 'Characteristic', 'Luck')),
    skill_id        uuid references cm.skills on delete restrict,
    characteristic  text check (characteristic in ('STR', 'CON', 'SIZ', 'DEX', 'APP', 'INT', 'POW', 'EDU')),
    difficulty      text not null default 'Regular' check (difficulty in ('Regular', 'Hard', 'Extreme')),
    on_success      text,
    on_failure      text,
    check ((target_kind = 'Skill') = (skill_id is not null)),
    check ((target_kind = 'Characteristic') = (characteristic is not null))
);

create table cm.scenario_key_facts (
    id           uuid primary key,
    scenario_id  uuid not null references cm.scenarios on delete cascade,
    ord          int not null,
    type         text not null check (type in ('Backstory', 'Truth', 'Timeline', 'Reward')),
    title        text not null,
    content      text
);

create table cm.scenario_handouts (
    id           uuid primary key,
    scenario_id  uuid not null references cm.scenarios on delete cascade,
    ord          int not null,
    name         text not null,          -- пометка Хранителя, игрокам не показывается
    player_text  text,                   -- то, что видят игроки (v1 Description)
    keeper_note  text,                   -- только Хранителю
    file_id      uuid references cm.files on delete set null
);

-- Тварь сценария — ссылка на бестиарий плюс необязательная своя версия статблока: исправление
-- в бестиарии доходит до сценариев, где тварь не переделывали.
create table cm.scenario_creatures (
    id                 uuid primary key,
    scenario_id        uuid not null references cm.scenarios on delete cascade,
    ord                int not null,
    creature_id        uuid references cm.creatures on delete restrict,
    name               text,             -- переопределение («Вожак культистов»)
    statblock          jsonb,            -- null — как в бестиарии
    statblock_version  int,
    count              int not null default 1 check (count >= 1),
    location_note      text,             -- «в подвале особняка» — так положение задают на деле
    notes              text,
    check (creature_id is not null or (name is not null and statblock is not null))
);
-- API отдаёт итоговый статблок (переопределение поверх бестиария): бой читает именно его.

create table cm.scenario_items (
    id           uuid primary key,
    scenario_id  uuid not null references cm.scenarios on delete cascade,
    ord          int not null,
    item_id        uuid references cm.items on delete restrict,
    name           text,
    description    text,
    location_note  text,
    notes          text,
    check (item_id is not null or name is not null)
);

-- ═══════════════════════ Персонажи ═══════════════════════
-- Лист — документ; кто он и чей — колонки и ограничения.
--   Player: owner_id — игрок; campaign_id — кампания, где он играет (null — лист ещё не в кампании).
--           Составной FK требует, чтобы игрок был участником кампании; удалили участника — лист
--           не пропадает, а остаётся у игрока без кампании (ON DELETE SET NULL (campaign_id)).
--   Pregen: scenario_id — преген сценария; null — заготовка в библиотеке.
--   Npc:    campaign_id — НПС кампании; null — общая библиотека.

create table cm.characters (
    id                   uuid primary key,
    kind                 text not null check (kind in ('Player', 'Pregen', 'Npc')),
    status               text not null default 'Active' check (status in ('Active', 'Inactive', 'Retired', 'Archived')),
    owner_id             uuid references cm.users on delete restrict,
    campaign_id          uuid references cm.campaigns on delete set null,
    scenario_id          uuid references cm.scenarios on delete set null,
    origin_character_id  uuid references cm.characters on delete set null,   -- откуда скопирован (бронь прегена)
    portrait_file_id     uuid references cm.files on delete set null,
    sheet                jsonb not null,
    sheet_version        int not null,
    name                 text generated always as (sheet #>> '{personal,name}') stored,
    occupation           text generated always as (sheet #>> '{personal,occupation}') stored,
    created_by_id        uuid references cm.users on delete set null,
    created_at           timestamptz not null default now(),
    updated_at           timestamptz not null default now(),
    foreign key (campaign_id, owner_id) references cm.campaign_members (campaign_id, user_id)
        on delete set null (campaign_id),
    check (   (kind = 'Player' and owner_id is not null and scenario_id is null)
           or (kind = 'Pregen' and owner_id is null and campaign_id is null)
           or (kind = 'Npc'    and owner_id is null and scenario_id is null))
);
create unique index characters_one_active_sheet on cm.characters (campaign_id, owner_id)
    where kind = 'Player' and status = 'Active' and campaign_id is not null;
create index characters_kind_status on cm.characters (kind, status);
create index characters_owner on cm.characters (owner_id) where owner_id is not null;
create index characters_campaign on cm.characters (campaign_id) where campaign_id is not null;
create index characters_scenario on cm.characters (scenario_id) where scenario_id is not null;

-- НПС не копируется: в сценарии он появляется связью. Роль и количество — у появления, не у листа.
create table cm.scenario_npcs (
    scenario_id   uuid not null references cm.scenarios on delete cascade,
    character_id  uuid not null references cm.characters on delete restrict,
    role          text not null default 'Neutral' check (role in ('Neutral', 'Enemy', 'Ally')),
    count         int not null default 1 check (count >= 1),
    notes         text,
    primary key (scenario_id, character_id)
);

-- Треки, прибитые к локации (настроение — music_tags в самой локации).
-- Связей «НПС/тварь/предмет/раздатка ↔ локация» в 2.0.0 нет: в v1 их не пишет ни интерфейс, ни
-- импорт (с коммита 4b6436f), в данных 0 из 52 локаций, а где стоит тварь или предмет, Хранители
-- пишут текстом (`location_note`). Понадобится редактор — добавить таблицы вида
-- location_npcs (location_id, scenario_id, character_id) с FK на scenario_npcs on delete cascade:
-- убранный из состава НПС тогда уходит и из локаций, без висячих ссылок v1.
create table cm.location_tracks (
    location_id  uuid not null references cm.scenario_locations on delete cascade,
    track_id     uuid not null references cm.music_tracks on delete cascade,
    primary key (location_id, track_id)
);

-- ═══════════════════════ Игра ═══════════════════════
-- Прохождение сценария в кампании: дата, анонс, запись на ваншот. Содержимое не копируется.
create table cm.scenario_runs (
    id            uuid primary key,
    scenario_id   uuid not null references cm.scenarios on delete restrict,
    campaign_id   uuid not null references cm.campaigns on delete cascade,
    status        text not null default 'Planned' check (status in ('Planned', 'Announced', 'Running', 'Finished')),
    scheduled_at  timestamptz,
    announcement  text,
    signup_open   boolean not null default false,   -- ваншот: игроки бронируют прегенов
    created_at    timestamptz not null default now(),
    updated_at    timestamptz not null default now()
);
create index scenario_runs_campaign on cm.scenario_runs (campaign_id);
create index scenario_runs_open on cm.scenario_runs (scheduled_at) where signup_open;

-- Бронь копирует лист прегена в лист игрока (characters.kind = 'Player', origin_character_id = прегену),
-- а сам преген остаётся в сценарии нетронутым для следующего прохождения.
create table cm.run_reservations (
    run_id        uuid not null references cm.scenario_runs on delete cascade,
    pregen_id     uuid not null references cm.characters on delete cascade,
    user_id       uuid not null references cm.users on delete cascade,
    character_id  uuid references cm.characters on delete set null,   -- копия у игрока
    created_at    timestamptz not null default now(),
    primary key (run_id, pregen_id),
    unique (run_id, user_id)
);

create table cm.campaign_sessions (
    id                  uuid primary key,
    campaign_id         uuid not null references cm.campaigns on delete cascade,
    run_id              uuid references cm.scenario_runs on delete set null,
    number              int not null,
    session_date        date not null,
    title               text,
    summary             text,            -- видно всем участникам
    keeper_notes        text,            -- только Хранителю; вырезает сервер, не разметка
    scenario_completed  boolean not null default false,
    created_at          timestamptz not null default now(),
    updated_at          timestamptz not null default now()
);
create index campaign_sessions_journal on cm.campaign_sessions (campaign_id, session_date desc, number desc);

-- Живое состояние стола: бой и погоня одной таблицей. Бой в v1 не сохранялся вовсе.
create table cm.encounters (
    id             uuid primary key,
    keeper_id      uuid not null references cm.users on delete cascade,
    campaign_id    uuid references cm.campaigns on delete cascade,
    run_id         uuid references cm.scenario_runs on delete set null,
    kind           text not null check (kind in ('Combat', 'Chase')),
    status         text not null default 'Active' check (status in ('Active', 'Finished')),
    state          jsonb not null,
    state_version  int not null,
    created_at     timestamptz not null default now(),
    updated_at     timestamptz not null default now()
);
-- Один активный бой на кампанию; погонь — сколько угодно (разделившихся ведут отдельными, 2026-10-02).
create unique index encounters_one_active_combat on cm.encounters (keeper_id, campaign_id) nulls not distinct
    where status = 'Active' and kind = 'Combat';

-- ═══════════════════════ Служебное ═══════════════════════

-- История правок справочников: только снимок «после», «до» — предыдущая строка.
create table cm.audit_log (
    id           bigint generated always as identity primary key,
    entity_type  text not null,
    entity_id    uuid not null,
    action       text not null check (action in ('Created', 'Updated', 'Deleted')),
    actor_id     uuid references cm.users on delete set null,
    snapshot     jsonb,
    created_at   timestamptz not null default now()
);
create index audit_log_entity on cm.audit_log (entity_type, entity_id, created_at desc);

-- Ключи Data Protection (EF-стандарт). Копируются из v1, чтобы куки входа пережили переключение.
create table cm.data_protection_keys (
    id             int generated by default as identity primary key,
    friendly_name  text,
    xml            text
);
```

## Документы

Типы документов живут в проекте `CampaignManager.Core` (см. [README.md](README.md),
«Архитектура»), потому что их читает и пишет и сервер, и клиент (WebAssembly, затем iOS).
Каждый документ — `record`-ы без поведения, сериализация — общие `JsonSerializerOptions` из Core
(camelCase, enum'ы строками). Апкастер `DocumentUpgrader.Upgrade(json, fromVersion)` приводит
документ старой версии 2.0 к текущей. **Листы v1 он не переводит**: это делает перенос (`T1.3`)
один раз — ему нужны справочник навыков и сопоставление по имени, которых в `Core` нет.

### Лист сыщика (`characters.sheet`, версия 1)

Полная модель — в [AUDIT.md](AUDIT.md), «Персонажи и НПС → Модель документа v2».
Обязательные правила:

- **Только то, что вводит человек.** `Half`/`Fifth`, максимумы, БкУ, Комплекция, СКО,
  Уклонение-зеркало вычисляет `Core`, в документ они не пишутся. Исключение — `overrides`: у НПС
  из книги напечатанные значения, и импорт не должен их терять.
- **Навык** — `{ skillId?, name?, parentSkillId?, value, checked }`; `name` есть только у
  самодельного навыка и у специализации. Группы не хранятся: группировка берётся из
  `skills.category`.
- **Оружие на листе** — свой тип с `catalogWeaponId` и **текстом книги**: урон, дальность, атаки,
  патроны, осечка. Числа считаются при чтении одним разборщиком `Core`. В v1 разобранные значения
  лежали в листе, не пересчитывались после правки, и бой бросал устаревший урон.
- **Заклинание и книга Мифов на листе** — тоже свои типы (`catalogSpellId`, `bookId`) со своим
  экземпляром: книги различаются от издания к изданию (стр. 222). Каталожные `Weapon`/`Spell` в
  лист не сериализуются.
- **Деньги** — `decimal?`, а не строка в одном из трёх форматов v1.
- **Портрет** — колонка `portrait_file_id`, не документ.
- **Никакого UI-состояния** (в v1 в документ уезжали поля формы `SkillGroup.NewSkillName`).
- `personal.name` и `personal.occupation` — по этим путям строятся generated-колонки.

### Статблок (`creatures.statblock`, `scenario_creatures.statblock`, версия 1)

Объединяет четыре jsonb-колонки v1 (`CreatureCharacteristics`, `Attacks`, `Skills`,
`SpecialAbilities`): характеристики `{ value, dice }`, ПЗ, ПМ, бонус к урону, Комплекция,
скорости (`move`, `swim`, `fly`, примечание), атак за раунд (+примечание), броня (+оговорка),
уклонение, потеря рассудка «успех/провал», инициатива, `attacks[]`
(`name, skillValue, damage, kind, damageBonusMode, description?`), `skills[] { skillId?, name, value, note? }`,
`specialAbilities[] { name, text }`. Наследие v1 не переносится: словарь `CombatDescriptions`,
колонка `ImageUrl`, ключи `Appearance`/`Education`/`Luck`/`Constitutions`.

### Состояние сцены (`encounters.state`, версия 1)

Снимок движка сцены (`T2.6`): участники (id участника ≠ id листа, `sourceCharacterId` /
`sourceCreatureId`), сторона, очередь, раунд, журнал, специфичная часть боя или погони.
Enum'ы строками — в v1 журнал боя хранил `CombatActionType` числом, и порядок членов
перечисления нельзя было трогать.

## Перенос данных v1 → v2

### Подход

- **Инструмент** — консольный проект `tools/CampaignManager.Migrate`: читает `games`/`identity`
  сырым SQL и `JsonNode` (зависимости от типов v1 нет), пишет в `cm` через `CmDbContext`, валидирует
  каждый документ десериализацией в типы Core. Простые таблицы переносит SQL, документы — C#.
- **Перезапускаемый**: `--reset` очищает `cm` и переносит заново. Пока идёт разработка 2.0, `cm`
  можно перезаливать сколько угодно.
- **Сначала на ветке Neon** (копия прода, `T0.1`), отчёт — `docs/v2/migration-report.md`
  (счётчики, отброшенное, предупреждения). На прод — только при переключении (`T3.1`).
- **Объекты MinIO**: строка `files` получает существующий ключ объекта v1. На ветке `dev` объект копируется
  из боевого бакета в бакет среды `campaign-manager-dev` под тем же ключом (`CopyObject` внутри MinIO, боевой
  бакет только читается); на проде (T3.2) источник и цель — один бакет, копии нет. Размер и тип — из хранилища
  (`StatObject`) и расширения; не нашедшийся объект попадает в отчёт, а ссылка на него не переносится.
- **Современная эпоха не переносится** (решение владельца 2026-10-02, T1.3): записи справочников только
  современной эпохи выбрасываются (в отчёт), у остальных `eras = {Classic}`, `cost_modern` пуст. Члены
  `Era.Modern` в enum и колонки схемы остаются (правила финансов современности в Core), данных с ними нет.
- **v1 не трогаем**: никаких записей в `games`/`identity`. Откат — переключить образ обратно.

### Соответствие таблиц

| v1 | v2 | Правила и мусор |
|---|---|---|
| `identity.AspNetUsers` (10) | `users` | Роль `int` 0/1/2 → `Player`/`Keeper`/`Admin`; `UserName` → `display_name`; `auth0_sub` пуст до входа. Таблицы ролей, claims, логинов и токенов пусты — не переносятся. |
| `UserPreferences` (2) | `user_preferences` | Строка на ключ. Мёртвые ключи `ui.sidebarExpanded`, `becomeKeeperCard.dismissed` — выбросить. |
| `KeeperApplications` (3) | `keeper_applications` | Почты → `user_id`/`reviewed_by_id`. |
| `Campaigns` (3) | `campaigns` + `campaign_members(Keeper)` | `kind = OneShot` для двух кампаний ваншотов (в имени «Ваншот»); `Era` 1/2 → `Classic`/`Modern`. |
| `CampaignPlayers` (15) | `campaign_members(Player)` | Строки самого Хранителя в своих кампаниях (3, без листов) — выбросить: он уже участник с ролью Keeper. `PlayerName` → `display_name`, только если отличается от имени пользователя (6 строк). |
| `CampaignSessions` (0) | `campaign_sessions` | `ScenarioId` → `run_id` прохождения этой кампании. |
| `Scenarios` (5) | `scenarios` (+ `scenario_runs`) | Шаблон → сценарий библиотеки. Сценарий с `CampaignId` → ещё и прохождение в этой кампании (`ScheduledDate` — `timestamp without time zone`, заполнена в одной строке: переводится в UTC из пояса Хранителя, по умолчанию `Europe/Prague` — `--keeper-time-zone`, решение владельца 2026-10-02; `AnnouncementText`; `IsPublished` → `signup_open`; статус — `Finished`, если кампания завершена, иначе `Announced`/`Planned` по публикации). Копия шаблона в кампании («Эликсир жизни», `IsTemplate = false`) — форк с `source_scenario_id` = шаблон плюс прохождение в её кампании; содержимое разошлось в обе стороны (тексты разные, у копии 2 НПС, у шаблона 0), поэтому не сливаем. `Journal` → `body_md`, `Location` → `setting`, `Era` → `setting_date` и разобранная `era`. Пустой шаблон «Дом с привидением» переносим, он в списке на удаление. |
| `Scenarios.Locations` (52) | `scenario_locations` + `scenario_checks` | Id и родители сохраняются. 39 проверок: навык ищется по имени; «СИЛ», «ЛВК», «ВЫН», «МОЩ» → `Characteristic`, «Удача» → `Luck`; `Difficulty` null → `Regular`. Списки `NpcIds`/`CreatureIds`/`ItemIds`/`HandoutIds`/`MusicTrackIds` и `MusicTags` пусты во всех 52 локациях — переносить нечего. |
| `Scenarios.KeyFacts`, `Handouts` | `scenario_key_facts`, `scenario_handouts` | `FileUrl` (2) → `files`; оба — заглушка `https://example.com/…` из примера импорта, переносятся без файла (в отчёт). `Description` → `player_text`. |
| `Scenarios.ScenarioCreatures` (4), `ScenarioItems` (5) | `scenario_creatures`, `scenario_items` | Все совпадают с каталогом по имени → `creature_id`/`item_id`; статблок сохраняется как переопределение, только если отличается от бестиария (на 2026-10-02 — у всех 4). Своё описание твари сценария (столбца нет) — в `notes`. Предмет сценария, совпавший с реквизитом из справочника (см. `Items`), — без `item_id`, с именем и описанием сценария. `Location` (текст, заполнен у 2 тварей и 5 предметов) → `location_note`. |
| `ScenarioNpcs` (25) | `scenario_npcs` | Как есть. |
| `Characters` (54) | `characters` | См. ниже. |
| `Skills` (93 → 90) | `skills` | `code` — по таблице `Core/Catalogs/SkillCodes` (`SkillCodes.FromName`): английское название книги в kebab-case (`skill.dodge`, `skill.cthulhu-mythos`), специализация — код родителя + `.` + английское название специализации (`skill.firearms.handgun`, `skill.fighting.brawl`); не транслит и не вычисляется из имени. Все 93 есть в таблице — навык без кода значит ошибку переноса. Только современные (`Is1920 = false`: «Ближний бой (бензопила)», «Работа с компьютером», «Электроника») не переносятся. Уклонение — `base_formula` `DEX/2`, родной язык — `EDU`. |
| `Occupations` (31 → 30) | `occupations` + `occupation_slots` | `code` — `OccupationCodes` (все 31 книжные; «Хакер» — только современная эпоха, не переносится). Навык-родитель в списке («Стрельба», «Язык, иностранный») → `AnySpecialization`. 193 названных навыка → слоты `Skill`; 5 названных специализаций («Язык, иностранный (латынь)» и др.) → `Specialization` с родителем; `SkillChoices` (24 варианта) → `Choice` + options; `SocialSkillSlots`/`FreeSkillSlots` → N слотов `Social`/`Free`; `Tags` (маска) → `text[]`. |
| `Weapons` (108 → 69) | `weapons` | `code` — `WeaponCodes` (все 108 книжные: таблица XVII и прейскурант 1920-х; 39 только современных, с ними «РПГ*», не переносятся). Подпись эпохи «1920-е, наши дни» в заметках — не заметка. Данные чистые: все разобраны, у всех `SkillId`. Числа — из `*Info`. Колонка `CatalogWeaponId` у каталога не нужна (нигде не заполнена). |
| `Spells` (92), `Books` (106) | `spells`, `books` + `book_spells` | `code` — `SpellCodes`, `BookCodes` (все книжные). `PossibleSpells` (165) → `book_spells`; сопоставятся ~68, остальные 97 — строкой без `spell_id`. |
| `Items` (317 → 257) | `items` | `code` — `ItemCodes` (257). Решения владельца 2026-10-02: 3 повтора книжных записей («Проживание в гостинице: …») выброшены; 4 реквизита сценария «Среди древних деревьев» — предметы этого сценария (`scenario_items` без `item_id`), не справочник; 53 предмета современного прейскуранта не переносятся; раздел «Фотоаппараты» (музыкальные инструменты) → «Развлечения»; ошибки перевода исправлены в имени (таблица кодов). `ImageUrl` → `files` (в данных пусто). |
| `Creatures` (87) | `creatures` + `creature_images` | `code` — `CreatureCodes` (86); самодельная «Гончая Шаб-Ниггурат» — без кода, в отчёт. Четыре jsonb → `statblock`. `Images` (86) → `files` + `creature_images`. Выбросить: колонку `ImageUrl` (36, уже перенесена в `Images`), словарь `CombatDescriptions` (55, исходный текст книги — источник правды давно типизированные поля), ключи наследия у одной твари. |
| `MusicTracks` (104) | `music_tracks` | `Storage` (89) → `files`; `YouTube` (15) → `youtube_id`. |
| `EditHistoryEntries` (310) | `audit_log` | Только `SnapshotJson`; почта → `actor_id`. |
| `ChaseSessions` (0) | `encounters` | Пусто. |
| `DataProtectionKeys` (3) | `data_protection_keys` | Копия: куки входа переживут переключение. |

### Персонажи

| Случай в v1 | Сколько | В v2 |
|---|---|---|
С #91 в v1 действует `CK_Characters_Owner`, и нарушителей в данных нет: перенос может на него
опираться. Состояние на 2026-10-01:

| Случай в v1 | Сколько | В v2 |
|---|---|---|
| `PlayerCharacter` (у всех есть `CampaignPlayerId`) | 9 (7 активных, 1 неактивный, 1 на покое) | `Player`, `owner_id` — игрок, `campaign_id` — его кампания, статус как был |
| `Pregen` забронирован (`CampaignPlayerId` + `ScenarioId`) | 3 | `Player` у забронировавшего в кампании ваншота. Нетронутого прегена в v1 не осталось — бронь его «израсходовала», брони в `run_reservations` не восстанавливаются |
| `Pregen` со сценарием, не забронирован | 1 | `Pregen`, `scenario_id` |
| `Pregen` без сценария (заготовки) | 6 (2 активных, 4 в архиве — перенесены туда в #91) | `Pregen`, `scenario_id` null, статус как был |
| `Npc` библиотеки | 34 (32 активных, 2 в архиве) | `Npc`, `campaign_id` null |
| `Npc` кампании | 1 | `Npc`, `campaign_id` |

Документ переводит `Migrate`, а не `Core` (см. «Документы»):
- `Id` внутри JSON выбрасывается (у 5 листов он не совпадал со строкой — берём id строки).
- Ключ наследия `CharacterType` (31 лист) и поля формы `NewSkillName`/`NewSkillBaseValue`
  выбрасываются.
- `Half`/`Fifth`, `PersonalInfo.Dodge`, `DamageBonus`, `Build`, `MoveSpeed`, максимумы — вычисляемые,
  не переносятся. У НПС, чьи значения расходятся с вычисленными (лист из книги), расхождение
  уходит в `overrides`, а не теряется.
- Навыки (2811 строк, у 1851 нет `SkillModelId`) → `skillId` по `SkillModelId`, иначе по точному
  имени, иначе «родитель + специализация» по имени в скобках, иначе — с учётом старых написаний
  («Языки (родной)» = «Язык, родной»; написания v1 знает `SkillCodes.FromName`, дальше — навык с этим
  кодом), иначе свой навык листа (`name` без `skillId`; в справочник он не попадает — на 2026-10-02 такой один).
  Несопоставленные — в отчёт. Без родителя записанные языки («Латынь», «Язык (французский)») — специализация
  «Язык, иностранный». Навык только современной эпохи на базовом значении выбрасывается, выше базы — своя
  специализация или самодельный. Отметка развития (`IsUsed`) переносится, кроме листов из импорта
  сценария, где она стоит у всех навыков сразу (ошибка v1).
  Строка навыка справочника **не выше базы** (с формулой: родной язык = ОБР, Уклонение = ½ ЛВК) и без отметки
  не заводится — лист показывает базу справочника (`SheetSkills.AddsNothing`, правило конструктора листа). В v1 0
  у такого навыка значил «не заполнено»; строкой он перекрыл бы базу. Ниже базы — в отчёт.
- Оружие (30 копий; у 14 нет `CatalogWeaponId`) → оружие листа: `catalogWeaponId` по
  `CatalogWeaponId` или имени, текст книги как есть; разобранные блоки v1 не переносятся.
- Заклинания (3) → заклинания листа. `SanityLossEpisode` → `sanityLostToday`. Деньги из строки
  (`$`-формат и без) → `decimal`.
- Портретов с адресом в данных нет — переносить нечего.
- «Пустые» значения v1 расхождением с формулой не считаются: максимум 0, Рассудок max 99 и Рассудок max, равный
  текущему (книга печатает у НПС текущий Рассудок). У сыщиков и прегенов вычисленное побеждает, разница — в отчёт.
- Одноимённые листы (НПС «Джон Фокс» ×2, «Уильям Уильямс» ×3, сыщица «Сивилия» ×2) не сливаются,
  а попадают в отчёт: это могут быть разные персонажи.

### Проверки после переноса

```sql
-- счётчики против v1 (с учётом выброшенного по правилам выше)
select 'users', count(*) from cm.users union all
select 'campaigns', count(*) from cm.campaigns union all
select 'members', count(*) from cm.campaign_members union all
select 'characters', count(*) from cm.characters union all
select 'scenarios', count(*) from cm.scenarios union all
select 'locations', count(*) from cm.scenario_locations union all
select 'checks', count(*) from cm.scenario_checks union all
select 'files', count(*) from cm.files;

-- у каждого игрока не больше одного активного листа в кампании — гарантирует индекс;
-- у каждой кампании ровно один Хранитель
select c.id from cm.campaigns c
where (select count(*) from cm.campaign_members m where m.campaign_id = c.id and m.role = 'Keeper') <> 1;

-- файлы, на которые никто не ссылается (после переноса — пусто)
select f.id from cm.files f
where not exists (select 1 from cm.characters        where portrait_file_id = f.id)
  and not exists (select 1 from cm.books             where image_file_id = f.id)
  and not exists (select 1 from cm.items             where image_file_id = f.id)
  and not exists (select 1 from cm.creature_images   where file_id = f.id)
  and not exists (select 1 from cm.weapon_images     where file_id = f.id)
  and not exists (select 1 from cm.occupation_images where file_id = f.id)
  and not exists (select 1 from cm.spell_images      where file_id = f.id)
  and not exists (select 1 from cm.artifact_images   where file_id = f.id)
  and not exists (select 1 from cm.scenario_handouts where file_id = f.id)
  and not exists (select 1 from cm.music_tracks      where file_id = f.id);
```

Остальные инварианты (владелец листа, один Хранитель на кампанию, один активный бой) держат
ограничения схемы: перенос, который их нарушит, просто упадёт на вставке — это и есть проверка.
