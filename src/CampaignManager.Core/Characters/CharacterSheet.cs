using System.Text.Json.Serialization;
using CampaignManager.Core.Documents;

namespace CampaignManager.Core.Characters;

/// <summary>
/// Лист сыщика — документ <c>characters.sheet</c> (SCHEMA, «Документы»; модель — AUDIT, «Персонажи и
/// НПС → Модель документа v2»). В документе только то, что вводит человек: половины и пятые части,
/// максимумы, БкУ, Комплекция, Скорость и Уклонение-зеркало вычисляет <see cref="DerivedAttributeRules"/>.
/// Исключение — <see cref="Overrides"/>: напечатанные в книге значения НПС.
/// <para>
/// Навыки — ссылки на справочник (<see cref="SheetSkill.SkillId"/>); имена, группы и базовые значения
/// берутся из него же (<see cref="SkillCatalog"/>), поэтому лист без справочника не читается — это
/// намеренно: в v1 навыки искали по строковым именам, и одно и то же имя жило в двух написаниях.
/// </para>
/// <para>
/// Портрет — колонка <c>portrait_file_id</c>, владелец и игрок — колонки строки; копий <c>Id</c> строки
/// внутри документа нет. UI-состояния в документе нет.
/// </para>
/// </summary>
public sealed record CharacterSheet : DocumentPart
{
    /// <summary>Текущая версия документа (<c>characters.sheet_version</c>).</summary>
    public const int CurrentVersion = 1;

    public PersonalInfo Personal { get; set; } = new();

    public Characteristics Characteristics { get; set; } = new();

    /// <summary>Текущие ПЗ, ПМ, Рассудок и Удача. Максимумы вычисляются.</summary>
    public CurrentValues Current { get; set; } = new();

    /// <summary>Значения из книги, которые побеждают вычисленные (НПС из сценария).</summary>
    public SheetOverrides Overrides { get; set; } = new();

    public List<SheetSkill> Skills { get; set; } = [];

    public List<SheetWeapon> Weapons { get; set; } = [];

    public List<SheetSpell> Spells { get; set; } = [];

    public List<EquipmentItem> Equipment { get; set; } = [];

    public Finances Finances { get; set; } = new();

    public Biography Biography { get; set; } = new();

    public SheetCondition Condition { get; set; } = new();

    public List<InsanityCondition> InsanityConditions { get; set; } = [];

    public List<MythosBookRecord> MythosBooks { get; set; } = [];

    public List<FellowInvestigator> FellowInvestigators { get; set; } = [];
}

/// <summary>Анкета. <c>name</c> и <c>occupation</c> — пути generated-колонок <c>characters.name/occupation</c>.</summary>
public sealed record PersonalInfo : DocumentPart
{
    public string Name { get; set; } = "";

    /// <summary>Род занятий словами — как на бланке; ссылка на справочник — <see cref="OccupationId"/>.</summary>
    public string Occupation { get; set; } = "";

    public Guid? OccupationId { get; set; }

    public int Age { get; set; }

    public string Gender { get; set; } = "";

    public string Birthplace { get; set; } = "";

    public string Residence { get; set; } = "";
}

/// <summary>Восемь характеристик (стр. 28–29). Половину и пятую часть считают <see cref="CharacteristicMath"/>.</summary>
public sealed record Characteristics : DocumentPart
{
    public int Str { get; set; }
    public int Con { get; set; }
    public int Siz { get; set; }
    public int Dex { get; set; }
    public int App { get; set; }
    public int Int { get; set; }
    public int Pow { get; set; }
    public int Edu { get; set; }

    public int this[Characteristic characteristic]
    {
        get => characteristic switch
        {
            Characteristic.STR => Str,
            Characteristic.CON => Con,
            Characteristic.SIZ => Siz,
            Characteristic.DEX => Dex,
            Characteristic.APP => App,
            Characteristic.INT => Int,
            Characteristic.POW => Pow,
            Characteristic.EDU => Edu,
            _ => throw new ArgumentOutOfRangeException(nameof(characteristic), characteristic, null),
        };
        set
        {
            switch (characteristic)
            {
                case Characteristic.STR: Str = value; break;
                case Characteristic.CON: Con = value; break;
                case Characteristic.SIZ: Siz = value; break;
                case Characteristic.DEX: Dex = value; break;
                case Characteristic.APP: App = value; break;
                case Characteristic.INT: Int = value; break;
                case Characteristic.POW: Pow = value; break;
                case Characteristic.EDU: Edu = value; break;
                default: throw new ArgumentOutOfRangeException(nameof(characteristic), characteristic, null);
            }
        }
    }
}

/// <summary>Текущие значения; максимумы — <see cref="DerivedAttributeRules"/>.</summary>
public sealed record CurrentValues : DocumentPart
{
    public int HitPoints { get; set; }
    public int MagicPoints { get; set; }
    public int Sanity { get; set; }
    public int Luck { get; set; }
}

/// <summary>
/// Напечатанные в книге значения НПС, которые расходятся с вычисленными. Пусто — считается по правилам.
/// Без них импорт сценария терял ПЗ и БкУ из книги (AUDIT, «Дубли»).
/// </summary>
public sealed record SheetOverrides : DocumentPart
{
    public int? MaxHitPoints { get; set; }
    public int? MaxMagicPoints { get; set; }
    public int? MaxSanity { get; set; }
    public string? DamageBonus { get; set; }
    public int? Build { get; set; }
    public int? Move { get; set; }
}

/// <summary>
/// Навык на листе. У навыка справочника заполнен <see cref="SkillId"/>, а имя берётся из справочника.
/// <see cref="Name"/> — только у своего навыка и у специализации, которой в справочнике нет: тогда это
/// уточнение в скобках («латынь»), а родитель — <see cref="ParentSkillId"/>.
/// </summary>
public sealed record SheetSkill : DocumentPart
{
    public Guid? SkillId { get; set; }

    public string? Name { get; set; }

    public Guid? ParentSkillId { get; set; }

    /// <summary>Полное значение навыка (база уже внутри).</summary>
    public int Value { get; set; }

    /// <summary>Отметка успешного применения для фазы развития (стр. 92).</summary>
    public bool Checked { get; set; }
}

/// <summary>
/// Оружие на листе: ссылка на каталог и <b>текст книги</b>. Числа (урон, дальность, патроны, осечка)
/// считаются при чтении разборщиками <c>Catalogs</c> — в v1 разобранные блоки лежали в листе, не
/// пересчитывались после правки, и бой бросал устаревший урон.
/// </summary>
public sealed record SheetWeapon : DocumentPart
{
    /// <summary>Строка на листе (для ключа в UI и ссылки из сцены); не id каталога.</summary>
    public Guid RowId { get; set; } = Guid.CreateVersion7();

    public Guid? CatalogWeaponId { get; set; }

    public string Name { get; set; } = "";

    public Guid? SkillId { get; set; }

    public string Damage { get; set; } = "";

    public string Range { get; set; } = "";

    public string Attacks { get; set; } = "";

    public string Ammo { get; set; } = "";

    public string Malfunction { get; set; } = "";

    public bool Impaling { get; set; }

    public string Notes { get; set; } = "";
}

/// <summary>Заклинание на листе — свой экземпляр, каталожный <c>Spell</c> в лист не сериализуется.</summary>
public sealed record SheetSpell : DocumentPart
{
    public Guid? CatalogSpellId { get; set; }

    public string Name { get; set; } = "";

    /// <summary>Другие названия из каталога — по ним книга Мифов узнаёт уже выученное.</summary>
    public List<string> AlternativeNames { get; set; } = [];

    public string Cost { get; set; } = "";

    public string CastingTime { get; set; } = "";

    public string Description { get; set; } = "";
}

public sealed record EquipmentItem : DocumentPart
{
    public string Name { get; set; } = "";

    public string Description { get; set; } = "";
}

/// <summary>Деньги числами, а не строкой в одном из трёх форматов v1 (rules-findings F-S06).</summary>
public sealed record Finances : DocumentPart
{
    public decimal? Cash { get; set; }

    public decimal? PocketMoney { get; set; }

    /// <summary>Активы словами: «5000», «дом в Аркхеме», «нет».</summary>
    public string Assets { get; set; } = "";

    public string Note { get; set; } = "";
}

/// <summary>
/// Биография (стр. 40–43): графы бланка, ключевая связь, предыстория и заметки. Текстовых граф «Фобии» и
/// «Магические предметы» нет (решение владельца 2026-10-02): фобии и мании — структурированные записи
/// <see cref="CharacterSheet.InsanityConditions"/>, предметы — <see cref="CharacterSheet.Equipment"/>. Текст
/// этих граф в старых листах 2.0 доживает в <see cref="DocumentPart.Extra"/>; перенос v1 его отбрасывает.
/// «Предыстория» и «Заметки» — раздельные графы (решение владельца 2026-10-02).
/// </summary>
public sealed record Biography : DocumentPart
{
    public string Appearance { get; set; } = "";
    public string Traits { get; set; } = "";
    public string IdealsAndPrinciples { get; set; } = "";
    public string SignificantPeople { get; set; } = "";
    public string ImportantPlaces { get; set; } = "";
    public string ValuablePossessions { get; set; } = "";
    public string SupernaturalEncounters { get; set; } = "";
    public string Injuries { get; set; } = "";
    public string Backstory { get; set; } = "";

    /// <summary>Ключевая связь (стр. 43); пусто — связь потеряна.</summary>
    public string KeyConnection { get; set; } = "";

    /// <summary>
    /// Эпилог (стр. 210–211): пара фраз о том, что стало с сыщиком, когда он выбыл — погиб, сошёл с ума или история кончилась.
    /// Добавлен без смены версии: в старом документе его нет — значит, пусто.
    /// </summary>
    public string Epilogue { get; set; } = "";

    public string Notes { get; set; } = "";
}

/// <summary>
/// Состояние сыщика: раны, безумие, потери рассудка, привыкание. Флаг «положен приступ» ставит и
/// снимает только <see cref="SanityRules"/>.
/// </summary>
public sealed record SheetCondition : DocumentPart
{
    public bool Unconscious { get; set; }
    public bool MajorWound { get; set; }
    public bool Dying { get; set; }

    /// <summary>
    /// Умирающий временно стабилизирован первой помощью (стр. 118): 1 ПЗ, ВЫН раз в час, ждёт Медицину. Добавлено в
    /// T2.6b без смены версии: в старом документе поля нет — значит, false.
    /// </summary>
    public bool Stabilized { get; set; }

    /// <summary>
    /// Мёртв: урон одной атаки не меньше максимума ПЗ или провал ВЫН умирающего (стр. 118, F-S02). Добавлено в T2.6b
    /// без смены версии, как и <see cref="Stabilized"/>.
    /// </summary>
    public bool Dead { get; set; }

    public bool TemporaryInsanity { get; set; }
    public bool IndefiniteInsanity { get; set; }

    public DateTimeOffset? TemporaryInsanityStartedAt { get; set; }

    public DateTimeOffset? IndefiniteInsanityStartedAt { get; set; }

    /// <summary>
    /// Потеряно за игровой день — для «≥1/5 Рассудка за день → бессрочное безумие» (стр. 154).
    /// Сбрасывается вручную («Новый день»). В v1 — <c>SanityLossEpisode</c>.
    /// </summary>
    public int SanityLostToday { get; set; }

    /// <summary>
    /// Потеря от одной причины — для «≥5 → проверка ИНТ» (стр. 153). Отдельно от дневной: два провала
    /// по 3 проверку ИНТ не дают.
    /// </summary>
    public int LastSanityLoss { get; set; }

    /// <summary>Случаи безумия, связанного с Мифами: первый даёт +5 Мифов, следующие +1 (стр. 160–161).</summary>
    public int MythosInsanityCount { get; set; }

    /// <summary>
    /// Приступ положен, но не разыгран: только что обезумел (стр. 154) или потерял рассудок в затаённом
    /// безумии (стр. 156). Флаг, а не вывод из полей: после приступа <see cref="LastSanityLoss"/> остаётся.
    /// </summary>
    public bool BoutDue { get; set; }

    public InsanityBout? LastBout { get; set; }

    /// <summary>Привыкание к ужасному (стр. 167), по видам тварей.</summary>
    public List<MythosHabituation> Habituations { get; set; } = [];
}

/// <summary>Как разыгрывается приступ (стр. 155–157). В JSON — имя члена.</summary>
public enum InsanityBoutMode
{
    /// <summary>Таблица VII: рядом другие сыщики, 1d10 раундов.</summary>
    RealTime,

    /// <summary>Таблица VIII: сыщик один или обезумели все, обычно 1d10 часов.</summary>
    Summary,
}

/// <summary>Разыгранный приступ. Текст не хранится — его по номеру отдаёт <see cref="InsanityTables"/>.</summary>
public sealed record InsanityBout : DocumentPart
{
    public InsanityBoutMode Mode { get; set; }

    /// <summary>1d10 по таблице VII или VIII.</summary>
    public int Roll { get; set; }

    /// <summary>Раунды (VII) или часы (VIII); null — длительность не бросали.</summary>
    public int? Duration { get; set; }

    public DateTimeOffset RolledAt { get; set; }
}

/// <summary>
/// Привыкание к ужасному (стр. 167): сколько рассудка уже потеряно за вид тварей. Ведётся по виду и
/// имени; дойдя до предела, сыщик перестаёт терять рассудок, фаза развития снижает накопленное на 1.
/// </summary>
public sealed record MythosHabituation : DocumentPart
{
    /// <summary>Тварь бестиария; null — запись заведена руками.</summary>
    public Guid? CreatureId { get; set; }

    public string CreatureName { get; set; } = "";

    /// <summary>Предел: максимум провальной части потери («0/1d6» → 6).</summary>
    public int MaxLoss { get; set; }

    public int LostSanity { get; set; }

    /// <summary>Запись потери из бестиария как есть — чтобы было видно, откуда предел.</summary>
    public string? SanityLossFormula { get; set; }

    [JsonIgnore]
    public bool IsHabituated => MaxLoss > 0 && LostSanity >= MaxLoss;

    [JsonIgnore]
    public int Remaining => MaxLoss > 0 ? Math.Max(0, MaxLoss - LostSanity) : int.MaxValue;
}

public enum InsanityConditionKind
{
    Phobia,
    Mania,
}

/// <summary>Фобия или мания (табл. IX/X).</summary>
public sealed record InsanityCondition : DocumentPart
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public InsanityConditionKind Kind { get; set; }

    public string Name { get; set; } = "";

    public string? Description { get; set; }

    public DateTimeOffset AcquiredAt { get; set; }

    /// <summary>false — вылечена.</summary>
    public bool Active { get; set; } = true;
}

/// <summary>Докуда дошло чтение книги Мифов (стр. 171–173).</summary>
public enum MythosBookStage
{
    NotRead,
    InitialReading,
    FullStudy,
}

/// <summary>
/// Книга Мифов на листе: ссылка на каталог и <b>свой экземпляр</b> чисел — экземпляры различаются, и
/// правка числа на листе не должна менять справочник у всех (стр. 222). Стадию двигает только
/// <see cref="MythosBookRules.Apply"/>.
/// </summary>
public sealed record MythosBookRecord : DocumentPart
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Книга каталога; null — заведена руками.</summary>
    public Guid? BookId { get; set; }

    public string Name { get; set; } = "";

    public string? Language { get; set; }

    /// <summary>Потеря рассудка от чтения — формула костей («2d4»).</summary>
    public string? SanityLoss { get; set; }

    /// <summary>МКН — прибавка к Мифам за начальное чтение.</summary>
    public int MythosInitial { get; set; }

    /// <summary>МКП — прибавка за полное изучение, пока Мифы читателя ниже ЗМ.</summary>
    public int MythosFull { get; set; }

    /// <summary>ЗМ — значение Мифов книги.</summary>
    public int MythosRating { get; set; }

    /// <summary>Первое полное изучение в неделях; каждое следующее вдвое дольше.</summary>
    public int? StudyWeeks { get; set; }

    public List<string> PossibleSpells { get; set; } = [];

    public MythosBookStage Stage { get; set; }

    public int FullStudyCount { get; set; }

    public string Note { get; set; } = "";

    public List<MythosBookReadingEntry> Readings { get; set; } = [];
}

/// <summary>Одно применённое чтение: что оно дало листу.</summary>
public sealed record MythosBookReadingEntry : DocumentPart
{
    public MythosBookStage Stage { get; set; }

    /// <summary>Игровое время словами («март 1925»).</summary>
    public string? GameDate { get; set; }

    public int SanityLost { get; set; }

    public int MythosGained { get; set; }

    /// <summary>Сыщик не верит в Мифы — навык растёт, рассудок не теряется (стр. 177).</summary>
    public bool Disbelieved { get; set; }

    public List<string> SpellsLearned { get; set; } = [];
}

/// <summary>
/// Строка «Знакомые сыщики». У соседа из состава заполнен <see cref="CharacterId"/>, и своя в строке
/// только заметка: имя и профессия — из живого листа; <see cref="Name"/> — слепок на случай ухода соседа.
/// Строка появляется в документе только с заметкой.
/// </summary>
public sealed record FellowInvestigator : DocumentPart
{
    public Guid? CharacterId { get; set; }

    public string Name { get; set; } = "";

    public string PlayerName { get; set; } = "";

    public string Note { get; set; } = "";
}
