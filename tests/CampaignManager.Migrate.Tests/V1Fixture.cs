using Npgsql;
using NpgsqlTypes;

namespace CampaignManager.Migrate.Tests;

/// <summary>
/// Маленькая v1 в одноразовой базе: таблицы <c>games</c>/<c>identity</c> только с колонками, которые читает
/// перенос, и по строке на каждый случай SCHEMA и решений владельца. Листы — настоящие (Fixtures/Sheets).
/// Почты — <c>@example.test</c>.
/// </summary>
public static class V1Fixture
{
    public static readonly Guid Campaign = Guid.Parse("10000000-0000-0000-0000-000000000001");
    public static readonly Guid PlayerPlace = Guid.Parse("20000000-0000-0000-0000-000000000001");
    public static readonly Guid KeeperPlace = Guid.Parse("20000000-0000-0000-0000-000000000002");
    public static readonly Guid Template = Guid.Parse("30000000-0000-0000-0000-000000000001");
    public static readonly Guid Copy = Guid.Parse("30000000-0000-0000-0000-000000000002");
    public static readonly Guid Investigator = Guid.Parse("40000000-0000-0000-0000-000000000001");
    public static readonly Guid Npc = Guid.Parse("40000000-0000-0000-0000-000000000002");
    public static readonly Guid ReservedPregen = Guid.Parse("40000000-0000-0000-0000-000000000003");
    public static readonly Guid Firearms = Guid.Parse("50000000-0000-0000-0000-000000000006");
    public static readonly Guid Handgun = Guid.Parse("50000000-0000-0000-0000-000000000007");
    public static readonly Guid CreditRating = Guid.Parse("50000000-0000-0000-0000-000000000003");
    public static readonly Guid Revolver = Guid.Parse("60000000-0000-0000-0000-000000000001");

    private const string Schema = """
        CREATE SCHEMA identity;
        CREATE SCHEMA games;
        CREATE TABLE identity."AspNetUsers" ("Id" text PRIMARY KEY, "Email" text, "UserName" text, "Role" int);
        CREATE TABLE games."UserPreferences" ("Id" uuid PRIMARY KEY, "UserEmail" text, "Preferences" jsonb, "LastUpdated" timestamptz);
        CREATE TABLE games."KeeperApplications" ("Id" uuid PRIMARY KEY, "UserEmail" text, "Message" text, "Status" text,
            "ReviewedByEmail" text, "ReviewedAt" timestamptz, "ReviewComment" text, "CreatedAt" timestamptz, "LastUpdated" timestamptz);
        CREATE TABLE games."Campaigns" ("Id" uuid PRIMARY KEY, "Name" text, "Status" text, "KeeperEmail" text, "Era" int,
            "CreatedAt" timestamptz, "LastUpdated" timestamptz);
        CREATE TABLE games."CampaignPlayers" ("Id" uuid PRIMARY KEY, "CampaignId" uuid, "PlayerEmail" text, "PlayerName" text, "CreatedAt" timestamptz);
        CREATE TABLE games."CampaignSessions" ("Id" uuid PRIMARY KEY, "CampaignId" uuid, "SessionDate" date, "Number" int, "Title" text,
            "Summary" text, "KeeperNotes" text, "ScenarioId" uuid, "ScenarioCompleted" boolean, "CreatedAt" timestamptz, "LastUpdated" timestamptz);
        CREATE TABLE games."Scenarios" ("Id" uuid PRIMARY KEY, "Name" text, "Description" text, "Location" text, "Era" text, "Journal" text,
            "IsTemplate" boolean, "CreatorEmail" text, "CampaignId" uuid, "CreatedAt" timestamptz, "LastUpdated" timestamptz,
            "ScenarioCreatures" jsonb, "ScenarioItems" jsonb, "Handouts" jsonb, "KeyFacts" jsonb, "Locations" jsonb,
            "AnnouncementText" text, "IsPublished" boolean, "ScheduledDate" timestamp);
        CREATE TABLE games."ScenarioNpcs" ("Id" uuid PRIMARY KEY, "ScenarioId" uuid, "CharacterId" uuid, "Role" text, "Count" int, "Notes" text);
        CREATE TABLE games."Characters" ("Id" uuid PRIMARY KEY, "CharacterName" text, "Character" jsonb, "CampaignPlayerId" uuid,
            "Status" text, "ScenarioId" uuid, "Kind" text, "CampaignId" uuid, "CreatedAt" timestamptz, "LastUpdated" timestamptz);
        CREATE TABLE games."Skills" ("Id" uuid PRIMARY KEY, "Name" text, "BaseValue" int, "Description" text, "Category" text,
            "IsUncommon" boolean, "UsageExamples" jsonb, "FailureConsequences" jsonb, "TimeRequired" text, "CanRetry" boolean,
            "OpposingSkills" jsonb, "ParentSkillId" uuid, "Is1920" boolean, "IsModern" boolean, "CreatedAt" timestamptz, "LastUpdated" timestamptz);
        CREATE TABLE games."Occupations" ("Id" uuid PRIMARY KEY, "Name" text, "SkillPointFormula" text, "CreditRatingMin" int,
            "CreditRatingMax" int, "OccupationSkills" jsonb, "FreeSkillSlots" int, "SocialSkillSlots" int, "Tags" int,
            "SkillChoices" jsonb, "IsLovecraftian" boolean, "IsModern" boolean, "CreatedAt" timestamptz, "LastUpdated" timestamptz);
        CREATE TABLE games."Weapons" ("Id" uuid PRIMARY KEY, "Type" text, "Name" text, "Skill" text, "Is1920" boolean, "IsModern" boolean,
            "Damage" text, "Range" text, "Attacks" text, "Cost" text, "Notes" text, "Ammo" text, "Malfunction" text,
            "IsImpaling" boolean, "IsRare" boolean, "SkillId" uuid, "CreatedAt" timestamptz, "LastUpdated" timestamptz);
        CREATE TABLE games."Spells" ("Id" uuid PRIMARY KEY, "Name" text, "Cost" text, "CastingTime" text, "Description" text,
            "AlternativeNames" jsonb, "SpellType" text);
        CREATE TABLE games."Books" ("Id" uuid PRIMARY KEY, "Name" text, "BookType" text, "AlternativeNames" jsonb, "Language" text,
            "Year" text, "Author" text, "SanityLoss" text, "CthulhuMythosInitial" int, "CthulhuMythosFull" int, "MythosRating" int,
            "StudyWeeks" int, "OccultismBonus" int, "Description" text, "PossibleSpells" jsonb, "ImageUrl" text);
        CREATE TABLE games."Items" ("Id" uuid PRIMARY KEY, "Name" text, "Type" text, "Description" text, "ImageUrl" text, "Era" text);
        CREATE TABLE games."Creatures" ("Id" uuid PRIMARY KEY, "Name" text, "Type" text, "Description" text, "ImageUrl" text,
            "CombatDescriptions" jsonb, "CreatureCharacteristics" jsonb, "SpecialAbilities" jsonb, "Attacks" jsonb, "Skills" jsonb, "Images" jsonb);
        CREATE TABLE games."MusicTracks" ("Id" uuid PRIMARY KEY, "Name" text, "SourceType" text, "Source" text, "Tags" jsonb, "Notes" text,
            "StartSeconds" int, "Loop" boolean, "Volume" int);
        CREATE TABLE games."EditHistoryEntries" ("Id" uuid PRIMARY KEY, "EntityType" text, "EntityId" uuid, "Action" text,
            "EditorEmail" text, "SnapshotJson" jsonb, "CreatedAt" timestamptz);
        CREATE TABLE games."ChaseSessions" ("Id" uuid PRIMARY KEY);
        CREATE TABLE games."DataProtectionKeys" ("Id" int PRIMARY KEY, "FriendlyName" text, "Xml" text);
        """;

    private const string Rows = """
        INSERT INTO identity."AspNetUsers" VALUES
            ('70000000-0000-0000-0000-000000000001', 'keeper@example.test', 'Хранитель', 1),
            ('70000000-0000-0000-0000-000000000002', 'player@example.test', 'Игрок', 0),
            ('70000000-0000-0000-0000-000000000003', 'admin@example.test', 'Админ', 2);
        INSERT INTO games."UserPreferences" VALUES ('71000000-0000-0000-0000-000000000001', 'player@example.test',
            '{"ui.sidebarExpanded": "false", "ui.lastCharacterId": "40000000-0000-0000-0000-000000000001"}', now());
        INSERT INTO games."KeeperApplications" VALUES ('72000000-0000-0000-0000-000000000001', 'player@example.test', '', 'Pending',
            NULL, NULL, NULL, now(), now());
        INSERT INTO games."Campaigns" VALUES ('10000000-0000-0000-0000-000000000001', 'Ваншот на пробу', 'Completed', 'keeper@example.test', 1, now(), now());
        INSERT INTO games."CampaignPlayers" VALUES
            ('20000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000001', 'player@example.test', 'Псевдоним', now()),
            ('20000000-0000-0000-0000-000000000002', '10000000-0000-0000-0000-000000000001', 'keeper@example.test', 'Хранитель', now());
        INSERT INTO games."Skills" ("Id", "Name", "BaseValue", "Category", "ParentSkillId", "Is1920", "IsModern", "UsageExamples") VALUES
            ('50000000-0000-0000-0000-000000000001', 'Уклонение', 0, 'CombatGeneral', NULL, true, true, '["пример"]'),
            ('50000000-0000-0000-0000-000000000002', 'Мифы Ктулху', 0, 'Special', NULL, true, true, NULL),
            ('50000000-0000-0000-0000-000000000003', 'Средства', 0, 'Special', NULL, true, true, NULL),
            ('50000000-0000-0000-0000-000000000004', 'Язык, родной', 0, 'Social', NULL, true, true, NULL),
            ('50000000-0000-0000-0000-000000000005', 'Язык, иностранный', 1, 'Social', NULL, true, true, NULL),
            ('50000000-0000-0000-0000-000000000006', 'Стрельба', 20, 'CombatFirearms', NULL, true, true, NULL),
            ('50000000-0000-0000-0000-000000000007', 'Стрельба (пистолет)', 20, 'CombatFirearms', '50000000-0000-0000-0000-000000000006', true, true, NULL),
            ('50000000-0000-0000-0000-000000000008', 'Наука', 1, 'Knowledge', NULL, true, true, NULL),
            ('50000000-0000-0000-0000-000000000009', 'Наука (криминалистика)', 1, 'InformationGathering', '50000000-0000-0000-0000-000000000008', true, true, NULL),
            ('50000000-0000-0000-0000-000000000010', 'Ближний бой', 25, 'CombatGeneral', NULL, true, true, NULL),
            ('50000000-0000-0000-0000-000000000011', 'Ближний бой (драка)', 25, 'CombatGeneral', '50000000-0000-0000-0000-000000000010', true, true, NULL),
            ('50000000-0000-0000-0000-000000000012', 'Работа с компьютером', 5, 'ProblemSolving', NULL, false, true, NULL);
        INSERT INTO games."Occupations" VALUES
            ('51000000-0000-0000-0000-000000000001', 'Частный сыщик', 'Edu2DexOrStr2', 9, 30,
             '["Стрельба", "Язык, иностранный (латынь)", "Средства"]', 1, 1, 3,
             '[{"Count": 1, "Options": ["Наука", "Ближний бой (драка)"]}]', false, false, now(), now()),
            ('51000000-0000-0000-0000-000000000003', 'Свой знахарь', 'Edu4', 10, 50,
             '["Стрельба", "Средства"]', 0, 0, 3, '[]', false, false, now(), now()),
            ('51000000-0000-0000-0000-000000000002', 'Хакер', 'Edu4', 10, 70, '["Работа с компьютером"]', 2, 1, 32, '[]', false, true, now(), now());
        INSERT INTO games."Weapons" VALUES
            ('60000000-0000-0000-0000-000000000001', 'Pistols', 'Револьвер 38-го калибра (9 мм)', 'Стрельба (пистолет)', true, true,
             '1d10', '15 метров', '1 (3)', '$25/200', '1920-е, наши дни', '6', '100', false, false,
             '50000000-0000-0000-0000-000000000007', now(), now()),
            ('60000000-0000-0000-0000-000000000002', 'AssaultRifles', 'АК-74', 'Стрельба', false, true,
             '2d6+1', '110 метров', '1 (2) или очередь', '$200', '', '30', '97', true, false,
             '50000000-0000-0000-0000-000000000006', now(), now());
        INSERT INTO games."Spells" VALUES ('61000000-0000-0000-0000-000000000001', 'Знак Старших богов', '1 магия', '1 раунд', 'описание',
            '["Старший знак"]', 'Защита'),
            ('61000000-0000-0000-0000-000000000002', 'Свой напев', '10 магии, 1d4 рассудка', '1 раунд', 'напев',
            '[]', 'Атака/Проклятие'),
            ('61000000-0000-0000-0000-000000000003', 'Свой зов', 'Магия варьирует, 5 МОЩ', '1 раунд', NULL,
            '[]', 'Связь с божеством');
        INSERT INTO games."Books" ("Id", "Name", "BookType", "AlternativeNames", "PossibleSpells", "MythosRating") VALUES
            ('62000000-0000-0000-0000-000000000001', 'Некрономикон (латинский перевод Вормия)', 'MythosBook', '[]',
             '["Знак Старших богов", "Неведомое заклинание"]', 48);
        INSERT INTO games."Items" ("Id", "Name", "Type", "Description", "Era") VALUES
            ('63000000-0000-0000-0000-000000000001', '«Форд» Model T', 'Транспорт', '1 250,00 доллара', 'Classic'),
            ('63000000-0000-0000-0000-000000000002', 'Смартфон', 'Средства связи', NULL, 'Classic'),
            ('63000000-0000-0000-0000-000000000003', 'Проживание в гостинице: неплохая гостиница', 'Жильё', NULL, 'Classic'),
            ('63000000-0000-0000-0000-000000000004', 'Шип Гла''аки', 'Артефакт Мифов', 'шип из справочника', 'Classic'),
            ('63000000-0000-0000-0000-000000000005', 'Аккордеон', 'Фотоаппараты', 'дорогой, около 40 долларов', 'Classic'),
            ('63000000-0000-0000-0000-000000000007', 'Иномарка на пробу', 'Иномарки', NULL, 'Classic'),
            ('63000000-0000-0000-0000-000000000006', 'Флаг (1 метр)', 'Палатки', 'от 3,50 доллара за штуку', 'Classic');
        INSERT INTO games."Creatures" VALUES ('64000000-0000-0000-0000-000000000001', 'Глубоководный', 'MythicMonsters', 'рыбы',
            'images/beasts/deep-one.jpg', '{"Когти": "1d6"}',
            '{"Strength": {"Value": 70}, "Constitution": {"Value": 50}, "Size": {"Value": 80}, "Dexterity": {"Value": 50},
              "Intelligence": {"Value": 65}, "Power": {"Value": 50}, "HealPoint": 13, "ManaPoint": 10, "AverageDamageBonus": "+1d4",
              "AverageComplexity": 1, "Speed": 8, "SwimSpeed": 10, "AttacksPerRound": 1, "Armor": 1, "DodgeSkill": 25,
              "SanityLoss": "0/1d6", "Initiative": 0, "Luck": 50}',
            '{"Дышит под водой": "да"}',
            '[{"Name": "Когти", "SkillValue": 45, "DamageFormula": "1d6", "Kind": "Melee", "DamageBonus": "Full", "Description": "рвёт"}]',
            '[{"Name": "Слух", "Value": 50}]',
            '[{"Url": "images/beasts/deep-one.jpg", "Caption": "в воде"}, {"Url": "images/beasts/missing.jpg"}]');
        INSERT INTO games."MusicTracks" VALUES
            ('65000000-0000-0000-0000-000000000001', 'Туман', 'Storage', 'music/fog.mp3', '["атмосфера", "атмосфера"]', NULL, 0, true, 80),
            ('65000000-0000-0000-0000-000000000002', 'Погоня', 'YouTube', 'abcdefghijk', '[]', NULL, 15, false, 100);
        INSERT INTO games."Scenarios" VALUES
            ('30000000-0000-0000-0000-000000000001', 'Сценарий на пробу', 'кратко', 'Бостон', 'Июнь 1925 года', 'текст', true,
             'keeper@example.test', NULL, now(), now(),
             '[{"Name": "Глубоководный", "Location": "в подвале", "Notes": "", "Description": "рыбы",
               "CreatureCharacteristics": {"Strength": {"Value": 70}, "Constitution": {"Value": 50}, "Size": {"Value": 80},
                 "Dexterity": {"Value": 50}, "Intelligence": {"Value": 65}, "Power": {"Value": 50}, "HealPoint": 13, "ManaPoint": 10,
                 "AverageDamageBonus": "+1d4", "AverageComplexity": 1, "Speed": 8, "SwimSpeed": 10, "AttacksPerRound": 1, "Armor": 1,
                 "DodgeSkill": 25, "SanityLoss": "0/1d6", "Initiative": 0},
               "Attacks": [{"Name": "Когти", "SkillValue": 45, "DamageFormula": "1d6", "Kind": "Melee", "DamageBonus": "Full", "Description": "рвёт"}],
               "Skills": [{"Name": "Слух", "Value": 50}], "SpecialAbilities": {"Дышит под водой": "да"}},
              {"Name": "Неизвестная тварь", "CreatureCharacteristics": {"HealPoint": 5}}]',
             '[{"Name": "Шип Гла''аки", "Description": "шип из сценария", "Location": "в гробу", "Notes": "главное"},
               {"Name": "«Форд» Model T", "Description": "1 250,00 доллара", "Location": "у дома"}]',
             '[{"Id": "33000000-0000-0000-0000-000000000001", "Name": "Карта", "Order": 0, "Description": "видят игроки",
                "KeeperNote": "только Хранителю", "FileUrl": "https://example.com/map.jpg"}]',
             '[{"Id": "34000000-0000-0000-0000-000000000001", "Type": 1, "Order": 0, "Title": "Правда", "Content": "всё не так"}]',
             '[{"Id": "35000000-0000-0000-0000-000000000001", "Name": "Дом", "Order": 0, "ParentLocationId": null, "Address": "улица",
                "Description": "дом", "MusicTags": [], "MusicTrackIds": ["65000000-0000-0000-0000-000000000001"],
                "SkillChecks": [
                  {"Id": "36000000-0000-0000-0000-000000000001", "SkillName": "Удача", "Difficulty": null, "SuccessResult": "да", "FailureResult": "нет"},
                  {"Id": "36000000-0000-0000-0000-000000000002", "SkillName": "СИЛ", "Difficulty": "Hard"},
                  {"Id": "36000000-0000-0000-0000-000000000003", "SkillName": "Наука (криминалистика)", "Difficulty": "Extreme"},
                  {"Id": "36000000-0000-0000-0000-000000000004", "SkillName": "Хиромантия"}]},
               {"Id": "35000000-0000-0000-0000-000000000002", "Name": "Подвал", "Order": 1,
                "ParentLocationId": "35000000-0000-0000-0000-000000000001", "SkillChecks": []}]',
             NULL, false, NULL),
            ('30000000-0000-0000-0000-000000000002', 'Сценарий на пробу', 'кратко', 'Бостон', '1920-е', 'другой текст', false,
             'keeper@example.test', '10000000-0000-0000-0000-000000000001', now(), now(),
             '[]', '[]', '[]', '[]', '[]', 'приходите', true, '2026-04-13 19:00:00');
        INSERT INTO games."EditHistoryEntries" VALUES
            ('66000000-0000-0000-0000-000000000001', 'Weapon', '60000000-0000-0000-0000-000000000001', 'Updated', 'keeper@example.test', '{"Name": "Револьвер"}', now()),
            ('66000000-0000-0000-0000-000000000002', 'Occupation', '51000000-0000-0000-0000-000000000001', 'Updated', 'system', NULL, now());
        INSERT INTO games."DataProtectionKeys" VALUES (1, 'key-1', '<key/>');
        """;

    public static async Task CreateAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using (var schema = new NpgsqlCommand(Schema + Rows, connection))
        {
            await schema.ExecuteNonQueryAsync();
        }

        async Task Character(Guid id, string fixture, string kind, string status, Guid? place = null, Guid? scenario = null)
        {
            var row = TestCatalog.Sheet(fixture);
            await using var insert = new NpgsqlCommand("""
                INSERT INTO games."Characters" VALUES (@id, @name, @doc, @place, @status, @scenario, @kind, NULL, now(), now())
                """, connection);
            insert.Parameters.AddWithValue("id", id);
            insert.Parameters.AddWithValue("name", row["Character"]!["PersonalInfo"]!["Name"]!.GetValue<string>());
            insert.Parameters.AddWithValue("doc", NpgsqlDbType.Jsonb, row["Character"]!.ToJsonString());
            insert.Parameters.AddWithValue("place", (object?)place ?? DBNull.Value);
            insert.Parameters.AddWithValue("status", status);
            insert.Parameters.AddWithValue("scenario", (object?)scenario ?? DBNull.Value);
            insert.Parameters.AddWithValue("kind", kind);
            await insert.ExecuteNonQueryAsync();
        }

        // Сыщик ушёл на покой, а забронированный преген — активный лист того же игрока в той же кампании
        await Character(Investigator, "player-milie-mare", "PlayerCharacter", "Retired", PlayerPlace);
        await Character(Npc, "npc-rene-pierce", "Npc", "Active");
        await Character(ReservedPregen, "pregen-helen-wright", "Pregen", "Active", PlayerPlace, Template);

        await using var npc = new NpgsqlCommand($"""
            INSERT INTO games."ScenarioNpcs" VALUES ('67000000-0000-0000-0000-000000000001', '{Template}', '{Npc}', 'Enemy', 2, 'у двери')
            """, connection);
        await npc.ExecuteNonQueryAsync();
    }
}
