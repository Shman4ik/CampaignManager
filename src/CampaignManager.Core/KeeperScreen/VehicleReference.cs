namespace CampaignManager.Core.KeeperScreen;

/// <summary>Раздел таблицы V: от него зависит навык управления.</summary>
public enum VehicleCategory
{
    Cars,
    Aircraft,
    HeavyMachinery,
    Other,
    Watercraft,
}

/// <summary>
/// Строка таблицы V «Транспортные средства» (стр. 143): СКО, Комплекция (она же число костей d10 тарана и «прочность»),
/// броня для людей внутри, сколько мест и каким навыком управляют (код справочника навыков).
/// </summary>
public sealed record VehicleTemplate(string Name, int Speed, double Build, int Armor, string Passengers, string SkillCode, VehicleCategory Category);

/// <summary>
/// Строка таблицы VI «Столкновения транспорта» (стр. 145): потеря Комплекции транспорта (та же кость — урон каждому внутри,
/// стр. 144) и типовые случаи. Подписи — пересказ, не цитата (D5).
/// </summary>
public sealed record CrashTier(string Name, string BuildLoss, string Examples);

/// <summary>Типовая прочность преграды (стр. 136).</summary>
public sealed record BarrierPreset(string Name, int HitPoints);

/// <summary>
/// Транспорт в погоне — <b>одна</b> копия таблиц V и VI и примеров преград: их читают погоня (T2.6c) и ширма Хранителя.
/// Только числа и короткие подписи (D5). Правила урона транспорту (поломка, «вдребезги») — <c>Encounters.Chase.VehicleRules</c>.
/// </summary>
public static class VehicleReference
{
    public const string Drive = "skill.drive-auto";
    public const string Pilot = "skill.pilot";
    public const string PilotAircraft = "skill.pilot.aircraft";
    public const string PilotBoat = "skill.pilot.boat";
    public const string HeavyMachinery = "skill.operate-heavy-machinery";
    public const string Ride = "skill.ride";

    /// <summary>Таблица V, стр. 143. Значения СКО — для современной техники (для 1920-х книга советует снизить на ~20%).</summary>
    public static IReadOnlyList<VehicleTemplate> Vehicles { get; } =
    [
        new("Малолитражный автомобиль", 13, 4, 1, "3–4", Drive, VehicleCategory.Cars),
        new("Стандартный автомобиль", 14, 5, 2, "4", Drive, VehicleCategory.Cars),
        new("Дорогой автомобиль", 15, 6, 2, "4", Drive, VehicleCategory.Cars),
        new("Спортивный автомобиль", 16, 5, 2, "1", Drive, VehicleCategory.Cars),
        new("Пикап", 14, 6, 2, "≥2", Drive, VehicleCategory.Cars),
        new("Грузовик", 13, 7, 2, "≥2", Drive, VehicleCategory.Cars),
        new("Грузовая фура", 13, 9, 2, "≥3", Drive, VehicleCategory.Cars),
        new("Лёгкий мотоцикл", 13, 1, 0, "1", Drive, VehicleCategory.Cars),
        new("Тяжёлый мотоцикл", 16, 3, 0, "1", Drive, VehicleCategory.Cars),

        new("Дирижабль", 12, 10, 2, "≥112", PilotAircraft, VehicleCategory.Aircraft),
        new("Винтовой самолёт", 15, 5, 1, "≥4", PilotAircraft, VehicleCategory.Aircraft),
        new("Бомбардировщик", 17, 11, 2, "≥10", PilotAircraft, VehicleCategory.Aircraft),
        new("Реактивный самолёт", 18, 11, 3, "≥50", PilotAircraft, VehicleCategory.Aircraft),
        new("Вертолёт", 15, 5, 2, "≥15", PilotAircraft, VehicleCategory.Aircraft),

        new("Танк", 11, 20, 24, "4", HeavyMachinery, VehicleCategory.HeavyMachinery),
        new("Паровоз с вагонами", 12, 12, 1, "≥400", HeavyMachinery, VehicleCategory.HeavyMachinery),
        new("Современный поезд", 15, 14, 2, "≥400", HeavyMachinery, VehicleCategory.HeavyMachinery),

        new("Лошадь (с наездником)", 11, 4, 0, "1", Ride, VehicleCategory.Other),
        new("Экипаж четвёркой лошадей", 10, 3, 0, "≥6", Ride, VehicleCategory.Other),
        new("Велосипед", 10, 0.5, 0, "1", Ride, VehicleCategory.Other),

        new("Вёсельная лодка", 4, 2, 0, "3", PilotBoat, VehicleCategory.Watercraft),
        new("Судно на воздушной подушке", 12, 4, 0, "22", PilotBoat, VehicleCategory.Watercraft),
        new("Моторная лодка", 14, 3, 0, "6", PilotBoat, VehicleCategory.Watercraft),
        new("Круизный лайнер", 11, 32, 0, "≥2200", PilotBoat, VehicleCategory.Watercraft),
        new("Линейный корабль", 11, 65, 0, "≥1800", PilotBoat, VehicleCategory.Watercraft),
        new("Авианосец", 11, 75, 0, "≥3200", PilotBoat, VehicleCategory.Watercraft),
        new("Подводная лодка", 12, 24, 0, "≥120", PilotBoat, VehicleCategory.Watercraft),
    ];

    /// <summary>
    /// Таблица VI, стр. 145. По умолчанию (стр. 142): мелкая авария — при обычной помехе, средняя — при трудной, серьёзная —
    /// при чрезвычайной (<see cref="DefaultCrash"/>).
    /// </summary>
    public static IReadOnlyList<CrashTier> Crashes { get; } =
    [
        new("Мелкая авария", "1D3-1", "задеть машину по касательной, фонарный столб, столбик; человек или существо того же размера"),
        new("Средняя авария", "1D6", "корова или взрослый олень, тяжёлый мотоцикл, малолитражка"),
        new("Серьёзная авария", "1D10", "стандартный автомобиль, фонарный столб, дерево"),
        new("Катастрофа", "2D10", "грузовик, автобус, толстое дерево"),
        new("Мясорубка", "5D10", "многотонная фура, поезд, метеорит"),
    ];

    /// <summary>Примеры преград и их ПЗ, стр. 136.</summary>
    public static IReadOnlyList<BarrierPreset> Barriers { get; } =
    [
        new("Внутренняя дверь, хлипкий забор", 5),
        new("Дверь чёрного хода", 10),
        new("Крепкая парадная дверь", 15),
        new("Кирпичная стена 22 см", 25),
        new("Взрослое дерево", 50),
        new("Бетонная опора моста", 100),
    ];

    public static VehicleTemplate? Find(string? name) =>
        string.IsNullOrWhiteSpace(name) ? null : Vehicles.FirstOrDefault(v => v.Name == name);

    /// <summary>Авария по умолчанию для проваленной помехи этой сложности (стр. 142).</summary>
    public static CrashTier DefaultCrash(Difficulty difficulty) => difficulty switch
    {
        Difficulty.Hard => Crashes[1],
        Difficulty.Extreme => Crashes[2],
        _ => Crashes[0],
    };

    public static CrashTier? FindCrash(string? name) =>
        string.IsNullOrWhiteSpace(name) ? null : Crashes.FirstOrDefault(c => c.Name == name);

    public static string Of(VehicleCategory category) => category switch
    {
        VehicleCategory.Cars => "Автомобили",
        VehicleCategory.Aircraft => "Воздушный транспорт",
        VehicleCategory.HeavyMachinery => "Тяжёлые машины",
        VehicleCategory.Other => "Иные виды транспорта",
        VehicleCategory.Watercraft => "Водные суда",
        _ => category.ToString(),
    };
}
